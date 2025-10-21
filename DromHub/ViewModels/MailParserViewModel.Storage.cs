using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace DromHub.ViewModels
{
    public partial class MailParserViewModel
    {
        private static readonly string AppDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DromHub");
        private static readonly string DefaultPricesRoot = Path.Combine(AppDataRoot, "Prices");

        private string _pricesRoot = DefaultPricesRoot;

        private string PricesRoot => _pricesRoot;

        private string ResolvePricesRoot(UserSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.PricesDirectory))
            {
                try
                {
                    var expanded = Environment.ExpandEnvironmentVariables(settings.PricesDirectory);
                    return Path.GetFullPath(expanded);
                }
                catch (Exception ex)
                {
                    var message = $"Некорректный путь к папке прайс-листов в настройках: {ex.Message}";
                    _logger.LogWarning(ex, "{Message}", message);
                    AddLog($"⚠️ {message}");
                }
            }

            return DefaultPricesRoot;
        }

        private bool EnsurePricesRoot(bool logOnSuccess = false)
        {
            if (TryEnsureDirectory(PricesRoot, "для прайс-листов"))
            {
                if (logOnSuccess)
                    AddLog($"📁 Папка для прайс-листов: {PricesRoot}");
                return true;
            }

            if (!string.Equals(PricesRoot, DefaultPricesRoot, StringComparison.OrdinalIgnoreCase))
            {
                var previous = PricesRoot;
                _pricesRoot = DefaultPricesRoot;
                AddLog($"⚠️ Возврат к стандартной папке прайс-листов: {DefaultPricesRoot}");

                if (!string.Equals(_userSettings.PricesDirectory, DefaultPricesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _userSettings.PricesDirectory = DefaultPricesRoot;
                    _userSettings.TrySave(_logger);
                }

                if (TryEnsureDirectory(PricesRoot, "для прайс-листов"))
                {
                    _logger.LogWarning(
                        "Не удалось использовать пользовательскую папку прайс-листов ({Previous}); использована папка по умолчанию ({Fallback}).",
                        previous,
                        DefaultPricesRoot);
                    if (logOnSuccess)
                        AddLog($"📁 Папка для прайс-листов: {PricesRoot}");
                    return true;
                }
            }

            return false;
        }

        private bool TryEnsureDirectory(string path, string purposeDescription)
        {
            try
            {
                var directory = Directory.CreateDirectory(path);

                if (!directory.Exists)
                    return false;

                TryEnsurePhysicalPath(path);
                return true;
            }
            catch (Exception ex)
            {
                ReportError($"Не удалось подготовить папку {purposeDescription} ({path}): {ex.Message}", ex);
                return false;
            }
        }

        private void TryEnsurePhysicalPath(string path)
        {
            try
            {
                var shellPath = GetShellAccessiblePath(path, ensureExists: true);
                if (!string.Equals(shellPath, path, StringComparison.OrdinalIgnoreCase) &&
                    !Directory.Exists(shellPath))
                {
                    Directory.CreateDirectory(shellPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось подготовить физический путь для {Path}", path);
            }
        }

        private string GetShellAccessiblePath(string originalPath, bool ensureExists)
        {
            if (string.IsNullOrWhiteSpace(originalPath))
                return originalPath;

            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData) &&
                    originalPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = Path.GetRelativePath(localAppData, originalPath);
                    if (!relative.StartsWith(".."))
                    {
                        var cachePath = ApplicationData.Current?.LocalCacheFolder?.Path;
                        if (!string.IsNullOrEmpty(cachePath))
                        {
                            var candidate = Path.Combine(cachePath, "Local", relative);
                            if (ensureExists && !Directory.Exists(candidate))
                            {
                                Directory.CreateDirectory(candidate);
                            }

                            if (Directory.Exists(candidate))
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is System.Runtime.InteropServices.COMException ||
                                       ex is UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Не удалось получить физический путь для {Path}", originalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при попытке определить физический путь для {Path}", originalPath);
            }

            return originalPath;
        }

        private sealed class UserSettings
        {
            private static readonly string SettingsFilePathInternal = Path.Combine(AppDataRoot, "settings.json");

            public string? PricesDirectory { get; set; }

            [JsonIgnore]
            public bool LoadedFromFile { get; set; }

            public static UserSettings Load(ILogger logger)
            {
                try
                {
                    if (!File.Exists(SettingsFilePathInternal))
                        return new UserSettings { LoadedFromFile = false };

                    var json = File.ReadAllText(SettingsFilePathInternal);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                    settings.LoadedFromFile = true;
                    return settings;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Не удалось загрузить настройки пользователя из {Path}", SettingsFilePathInternal);
                    return new UserSettings { LoadedFromFile = false };
                }
            }

            public void TrySave(ILogger logger)
            {
                try
                {
                    Directory.CreateDirectory(AppDataRoot);
                    var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(SettingsFilePathInternal, json);
                    LoadedFromFile = true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Не удалось сохранить настройки пользователя в {Path}", SettingsFilePathInternal);
                }
            }
        }

        private static class SecureCreds
        {
            private static readonly string Dir =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DromHub");
            private static readonly string FilePath = Path.Combine(Dir, "creds.json");

            private sealed class CredentialData
            {
                public string Id { get; set; } = Guid.NewGuid().ToString("N");
                public string Email { get; set; } = string.Empty;
                public string? DisplayName { get; set; }
                public string Pwd { get; set; } = string.Empty;
                public string Server { get; set; } = MailServerType.Custom.ToString();
            }

            private sealed class CredentialStore
            {
                public CredentialData? Default { get; set; }
            }

            private sealed class LegacyModel
            {
                public string? Email { get; set; }
                public string? Pwd { get; set; }
            }

            public static void Save(string email, string password, MailServerType server)
            {
                try
                {
                    var store = LoadStore();
                    store.Default ??= new CredentialData { Id = "default" };
                    store.Default.Email = email ?? string.Empty;
                    store.Default.DisplayName = email;
                    store.Default.Server = server.ToString();
                    store.Default.Pwd = Protect(password);
                    SaveOrDelete(store);
                }
                catch
                {
                    // молча — не критично
                }
            }

            public static bool TryLoad(out string email, out string password)
            {
                email = string.Empty;
                password = string.Empty;

                try
                {
                    var store = LoadStore();
                    if (store.Default == null || string.IsNullOrEmpty(store.Default.Pwd))
                        return false;

                    email = store.Default.Email ?? string.Empty;
                    password = Unprotect(store.Default.Pwd);
                    return true;
                }
                catch
                {
                    email = string.Empty;
                    password = string.Empty;
                    return false;
                }
            }

            public static void Clear()
            {
                try
                {
                    var store = LoadStore();
                    store.Default = null;
                    SaveOrDelete(store);
                }
                catch
                {
                    // ignore
                }
            }

            private static CredentialStore LoadStore()
            {
                try
                {
                    if (!File.Exists(FilePath))
                        return new CredentialStore();

                    var json = File.ReadAllText(FilePath);
                    if (string.IsNullOrWhiteSpace(json))
                        return new CredentialStore();

                    var store = JsonSerializer.Deserialize<CredentialStore>(json);
                    if (store != null)
                    {
                        if (store.Default != null && string.IsNullOrEmpty(store.Default.Id))
                            store.Default.Id = "default";

                        if (store.Default == null)
                        {
                            var legacy = JsonSerializer.Deserialize<LegacyModel>(json);
                            if (legacy != null && (!string.IsNullOrEmpty(legacy.Email) || !string.IsNullOrEmpty(legacy.Pwd)))
                            {
                                store.Default = new CredentialData
                                {
                                    Id = "default",
                                    Email = legacy.Email ?? string.Empty,
                                    Pwd = legacy.Pwd ?? string.Empty,
                                    DisplayName = legacy.Email,
                                    Server = MailServerType.MailRu.ToString(),
                                };
                            }
                        }

                        return store;
                    }

                    return new CredentialStore();
                }
                catch (JsonException)
                {
                    try
                    {
                        var legacyJson = File.ReadAllText(FilePath);
                        var legacy = JsonSerializer.Deserialize<LegacyModel>(legacyJson);
                        if (legacy != null)
                        {
                            return new CredentialStore
                            {
                                Default = new CredentialData
                                {
                                    Id = "default",
                                    Email = legacy.Email ?? string.Empty,
                                    Pwd = legacy.Pwd ?? string.Empty,
                                    DisplayName = legacy.Email,
                                    Server = MailServerType.MailRu.ToString(),
                                }
                            };
                        }
                    }
                    catch
                    {
                        // ignore legacy fallback errors
                    }

                    return new CredentialStore();
                }
                catch
                {
                    return new CredentialStore();
                }
            }

            private static void SaveOrDelete(CredentialStore store)
            {
                if (store.Default == null)
                {
                    if (File.Exists(FilePath))
                    {
                        try { File.Delete(FilePath); } catch { /* ignore */ }
                    }
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Dir);
                    var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(FilePath, json);
                }
                catch
                {
                    // ignore save failures
                }
            }

            private static string Protect(string password)
            {
                try
                {
                    var data = System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty);
                    var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                    return Convert.ToBase64String(encrypted);
                }
                catch
                {
                    return password ?? string.Empty;
                }
            }

            private static string Unprotect(string protectedPassword)
            {
                try
                {
                    var data = Convert.FromBase64String(protectedPassword ?? string.Empty);
                    var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                    return System.Text.Encoding.UTF8.GetString(decrypted);
                }
                catch
                {
                    return protectedPassword ?? string.Empty;
                }
            }
        }
    }
}
