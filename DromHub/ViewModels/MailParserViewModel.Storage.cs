using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс MailParserViewModel отвечает за логику компонента MailParserViewModel.
    /// </summary>
    public partial class MailParserViewModel
    {
        private static readonly string AppDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DromHub");
        private static readonly string DefaultPricesRoot = Path.Combine(AppDataRoot, "Prices");

        private string _pricesRoot = DefaultPricesRoot;
        /// <summary>
        /// Свойство PricesRoot предоставляет доступ к данным PricesRoot.
        /// </summary>

        private string PricesRoot => _pricesRoot;
        /// <summary>
        /// Метод ResolvePricesRoot выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Метод EnsurePricesRoot выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Метод TryEnsureDirectory выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Метод TryEnsurePhysicalPath выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Метод GetShellAccessiblePath выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Класс UserSettings отвечает за логику компонента UserSettings.
        /// </summary>

        private sealed class UserSettings
        {
            private static readonly string SettingsFilePathInternal = Path.Combine(AppDataRoot, "settings.json");
            /// <summary>
            /// Свойство PricesDirectory предоставляет доступ к данным PricesDirectory.
            /// </summary>

            public string? PricesDirectory { get; set; }
            /// <summary>
            /// Свойство LoadedFromFile предоставляет доступ к данным LoadedFromFile.
            /// </summary>

            [JsonIgnore]
            public bool LoadedFromFile { get; set; }
            /// <summary>
            /// Метод Load выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод TrySave выполняет основную операцию класса.
            /// </summary>

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
        /// <summary>
        /// Класс SecureCreds отвечает за логику компонента SecureCreds.
        /// </summary>

        private static class SecureCreds
        {
            private static readonly string Dir = AppDataRoot;
            private static readonly string FilePath = Path.Combine(Dir, "creds.json");
            /// <summary>
            /// Класс CredentialData отвечает за логику компонента CredentialData.
            /// </summary>

            private sealed class CredentialData
            {
                /// <summary>
                /// Свойство Id предоставляет доступ к данным Id.
                /// </summary>
                public string Id { get; set; } = Guid.NewGuid().ToString("N");
                /// <summary>
                /// Свойство Email предоставляет доступ к данным Email.
                /// </summary>
                public string Email { get; set; } = string.Empty;
                /// <summary>
                /// Свойство DisplayName предоставляет доступ к данным DisplayName.
                /// </summary>
                public string? DisplayName { get; set; }
                /// <summary>
                /// Свойство Pwd предоставляет доступ к данным Pwd.
                /// </summary>
                public string Pwd { get; set; } = string.Empty;
                /// <summary>
                /// Свойство Server предоставляет доступ к данным Server.
                /// </summary>
                public string Server { get; set; } = MailServerType.Custom.ToString();
            }
            /// <summary>
            /// Класс CredentialStore отвечает за логику компонента CredentialStore.
            /// </summary>

            private sealed class CredentialStore
            {
                /// <summary>
                /// Свойство Default предоставляет доступ к данным Default.
                /// </summary>
                public CredentialData? Default { get; set; }
            }
            /// <summary>
            /// Класс LegacyModel отвечает за логику компонента LegacyModel.
            /// </summary>

            private sealed class LegacyModel
            {
                /// <summary>
                /// Свойство Email предоставляет доступ к данным Email.
                /// </summary>
                public string? Email { get; set; }
                /// <summary>
                /// Свойство Pwd предоставляет доступ к данным Pwd.
                /// </summary>
                public string? Pwd { get; set; }
            }
            /// <summary>
            /// Метод Save выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод TryLoad выполняет основную операцию класса.
            /// </summary>

            public static bool TryLoad(out string email, out string password, out MailServerType server)
            {
                email = string.Empty;
                password = string.Empty;
                server = MailServerType.MailRu;

                try
                {
                    var store = LoadStore();
                    if (store.Default == null || string.IsNullOrEmpty(store.Default.Pwd))
                        return false;

                    email = store.Default.Email ?? string.Empty;
                    password = Unprotect(store.Default.Pwd);
                    if (!Enum.TryParse(store.Default.Server, ignoreCase: true, out server))
                    {
                        server = MailServerType.MailRu;
                    }
                    return true;
                }
                catch
                {
                    email = string.Empty;
                    password = string.Empty;
                    server = MailServerType.MailRu;
                    return false;
                }
            }
            /// <summary>
            /// Метод Clear выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод LoadStore выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод SaveOrDelete выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод Protect выполняет основную операцию класса.
            /// </summary>

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
            /// <summary>
            /// Метод Unprotect выполняет основную операцию класса.
            /// </summary>

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
