using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MimeKit;
using MailKit.Search;
using Windows.Storage;

namespace DromHub.ViewModels
{
    public partial class MailParserViewModel : ObservableObject
    {
        private static readonly string AppDataRoot =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DromHub");
        private static readonly string DefaultPricesRoot = Path.Combine(AppDataRoot, "Prices");

        private readonly ILogger<MailParserViewModel> _logger;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly UserSettings _userSettings;
        private string _pricesRoot = DefaultPricesRoot;
        private bool _isApplyingPresetSelection;
        private bool _isSyncingPresetFromEmail;

        private string PricesRoot => _pricesRoot;

        public MailParserViewModel(ILogger<MailParserViewModel> logger)
        {
            _logger = logger;

            _userSettings = UserSettings.Load(_logger);
            _pricesRoot = ResolvePricesRoot(_userSettings);

            if (!string.Equals(_userSettings.PricesDirectory, _pricesRoot, StringComparison.OrdinalIgnoreCase))
            {
                _userSettings.PricesDirectory = _pricesRoot;
                _userSettings.TrySave(_logger);
            }

            EnsurePricesRoot(logOnSuccess: true);

            // загрузим сохранённые учётные данные при старте
            if (SecureCreds.TryLoad(out var savedEmail, out var savedPassword))
            {
                EmailAddress = savedEmail ?? "";
                Password = savedPassword ?? "";
                RememberCredentials = true;
                AddLog("🔑 Найдены сохранённые учётные данные");
            }

            LoadCredentialPresets();
            UpdateServerSelection(SelectedMailServer);
        }

        // ===== ENUM / SERVER CONFIG =====
        public enum MailServerType { Gmail, MailRu, Yandex, Custom }

        private readonly Dictionary<MailServerType, (string Server, int Port, SecureSocketOptions Ssl)> _servers = new()
        {
            { MailServerType.Gmail, ("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.MailRu, ("imap.mail.ru", 993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.Yandex, ("imap.yandex.ru", 993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.Custom, ("imap.example.com", 993, SecureSocketOptions.Auto) }
        };

        // ===== PROPERTIES =====
        [ObservableProperty] private MailServerType selectedMailServer = MailServerType.MailRu; // по умолчанию Mail.ru
        [ObservableProperty] private string emailAddress = "";
        [ObservableProperty] private string password = "";
        [ObservableProperty] private string customServer = "imap.example.com";
        [ObservableProperty] private int customPort = 993;

        [ObservableProperty] private string statusMessage = "Готов к работе";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private Visibility customServerVisibility = Visibility.Collapsed;
        [ObservableProperty] private Visibility gmailPresetsVisibility = Visibility.Collapsed;
        [ObservableProperty] private ObservableCollection<string> logEntries = new();

        // «Запомнить меня» (локально, без БД)
        [ObservableProperty] private bool rememberCredentials = true;

        [ObservableProperty] private ObservableCollection<CredentialPreset> gmailPresets = new();
        [ObservableProperty] private CredentialPreset? selectedGmailPreset;
        [ObservableProperty] private bool canDeleteGmailPreset;

        // Прогресс парсинга
        [ObservableProperty] private double parsingProgress;          // 0..100
        [ObservableProperty] private string progressDetails = "";     // текст под прогрессом
        [ObservableProperty] private string currentSupplier = "";     // имя текущего поставщика
        [ObservableProperty] private int processedSuppliers;          // сколько обработали
        [ObservableProperty] private int totalSuppliers;              // всего в списке

        public List<string> MailServerTypes => new()
        {
            "Gmail (imap.gmail.com:993)",
            "Mail.ru (imap.mail.ru:993)",
            "Yandex (imap.yandex.ru:993)",
            "Другой сервер"
        };

        public sealed class CredentialPreset
        {
            public CredentialPreset(string id, string displayName, string email)
            {
                Id = id;
                DisplayName = displayName;
                Email = email;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string Email { get; }
        }

        // поставщики → ищем последнее письмо по From
        private readonly List<(string SupplierName, string FromEmail)> _suppliers = new()
        {
            ("AllMyParts", "eugenestulev@gmail.com"),
            ("Rossko",     "price@rossko.ru"),
            ("MXGroup",    "no_reply@mxgroup.ru"),
            ("Uniqom",     "1c_info@uniqom.ru"),
            ("Berg",       "noreply@berg.ru"),
            ("AvtoMC",     "noreply@api.avto-ms.ru"),
        };

        // просто оставляю enum на будущее, сейчас не обязателен
        private enum MailParseStage
        {
            Search,
            Fetch,
            Prepare,
            Zip,
            Xlsx,
            Csv,
            Cleanup,
            Done
        }

        // ===== LOGGING =====
        private void AddLog(string message)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                LogEntries.Add($"{DateTime.Now:HH:mm:ss} — {message}");
            });
        }

        private void UpdateStatus(string message)
        {
            _dispatcherQueue.TryEnqueue(() => StatusMessage = message);
        }

        private void ReportError(string message, Exception? ex = null)
        {
            if (ex != null)
                _logger.LogError(ex, "{Message}", message);
            else
                _logger.LogError("{Message}", message);

            AddLog($"❌ {message}");
            UpdateStatus(message);
        }

        private void LoadCredentialPresets()
        {
            GmailPresets.Clear();

            foreach (var preset in SecureCreds.LoadPresets(MailServerType.Gmail))
            {
                GmailPresets.Add(new CredentialPreset(
                    preset.Id,
                    string.IsNullOrWhiteSpace(preset.DisplayName) ? preset.Email : preset.DisplayName,
                    preset.Email));
            }

            SyncSelectedPresetToEmail();
        }

        private void UpdateServerSelection(MailServerType server)
        {
            CustomServerVisibility = server == MailServerType.Custom ? Visibility.Visible : Visibility.Collapsed;
            GmailPresetsVisibility = server == MailServerType.Gmail ? Visibility.Visible : Visibility.Collapsed;

            if (server == MailServerType.Gmail)
            {
                SyncSelectedPresetToEmail();
            }
            else
            {
                SelectedGmailPreset = null;
            }
        }

        private void SyncSelectedPresetToEmail()
        {
            if (_isApplyingPresetSelection)
                return;

            if (SelectedMailServer != MailServerType.Gmail || GmailPresets.Count == 0)
            {
                _isSyncingPresetFromEmail = true;
                SelectedGmailPreset = null;
                _isSyncingPresetFromEmail = false;
                return;
            }

            var match = GmailPresets.FirstOrDefault(p =>
                string.Equals(p.Email, EmailAddress, StringComparison.OrdinalIgnoreCase));

            _isSyncingPresetFromEmail = true;
            SelectedGmailPreset = match;
            _isSyncingPresetFromEmail = false;
        }

        partial void OnSelectedMailServerChanged(MailServerType value)
        {
            UpdateServerSelection(value);
        }

        partial void OnEmailAddressChanged(string value)
        {
            if (_isApplyingPresetSelection)
                return;

            if (SelectedMailServer == MailServerType.Gmail)
                SyncSelectedPresetToEmail();
        }

        partial void OnSelectedGmailPresetChanged(CredentialPreset? value)
        {
            CanDeleteGmailPreset = value != null;

            if (_isSyncingPresetFromEmail || value == null)
                return;

            try
            {
                _isApplyingPresetSelection = true;
                EmailAddress = value.Email;

                if (SecureCreds.TryLoadPresetPassword(value.Id, out var presetPassword))
                {
                    Password = presetPassword;
                    AddLog($"⭐ Загружен Gmail-пресет: {value.DisplayName}");
                    UpdateStatus($"Загружен пресет Gmail: {value.DisplayName}");
                }
                else
                {
                    Password = string.Empty;
                    AddLog($"⭐ Выбран Gmail-пресет (без пароля): {value.DisplayName}");
                    UpdateStatus($"Выбран пресет Gmail: {value.DisplayName}");
                }
            }
            finally
            {
                _isApplyingPresetSelection = false;
            }
        }

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
                    _logger.LogWarning("Не удалось использовать пользовательскую папку прайс-листов ({Previous}); использована папка по умолчанию ({Fallback}).", previous, DefaultPricesRoot);
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

        // перегрузка, если захочешь пользоваться стадиями
        private void SetProgress(double value, MailParseStage stage, string? supplier = null, string? details = null)
        {
            details ??= stage switch
            {
                MailParseStage.Search => "Поиск последнего письма…",
                MailParseStage.Fetch => "Получение письма…",
                MailParseStage.Prepare => "Подготовка…",
                MailParseStage.Zip => "Обработка архивов…",
                MailParseStage.Xlsx => "Обработка XLSX…",
                MailParseStage.Csv => "Конвертация CSV→XLSX…",
                MailParseStage.Cleanup => "Очистка…",
                MailParseStage.Done => "Завершено",
                _ => ProgressDetails
            };
            SetProgress(value, supplier, details);
        }

        // базовая перегрузка — обновляет прогресс/подпись/текущего поставщика
        private void SetProgress(double value, string? supplier = null, string? details = null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ParsingProgress = Math.Clamp(value, 0, 100);
                if (!string.IsNullOrWhiteSpace(supplier)) CurrentSupplier = supplier!;
                if (details != null) ProgressDetails = details;
            });
        }

        // ===== CONNECT + DOWNLOAD =====
        [RelayCommand]
        private async Task ConnectAndDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailAddress) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Введите email и пароль";
                return;
            }

            // прогресс: начало
            ParsingProgress = 0;
            SetProgress(0, details: "Подключение к серверу…");
            CurrentSupplier = "—";

            IsLoading = true;
            AddLog($"Подключение к {SelectedMailServer}…");

            if (!EnsurePricesRoot())
            {
                IsLoading = false;
                SetProgress(0, details: "Ошибка доступа к папке прайс-листов");
                return;
            }

            var (server, port, ssl) = GetServer();

            using var client = new ImapClient();
            try
            {
                await client.ConnectAsync(server, port, ssl);
                await client.AuthenticateAsync(EmailAddress, Password);

                IsConnected = true;
                AddLog("✅ Подключение успешно");
                StatusMessage = "Подключено";

                // Сохраним/удалим креды по флажку
                if (RememberCredentials)
                {
                    SecureCreds.Save(EmailAddress, Password, SelectedMailServer);
                    AddLog("🔐 Учётные данные сохранены локально");
                }
                else
                {
                    SecureCreds.Clear();
                    AddLog("🗑️ Сохранённые учётные данные удалены");
                }

                // чтение писем
                await ProcessInboxAsync(client);

                SetProgress(100, details: "Готово");
            }
            catch (Exception ex)
            {
                AddLog($"❌ Ошибка: {ex.Message}");
                StatusMessage = $"Ошибка: {ex.Message}";
                _logger.LogError(ex, "Ошибка подключения");
            }
            finally
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true);

                IsConnected = false;
                IsLoading = false;
                AddLog("📨 Сессия завершена");
            }
        }

        private (string, int, SecureSocketOptions) GetServer()
        {
            if (SelectedMailServer == MailServerType.Custom)
                return (CustomServer, CustomPort, SecureSocketOptions.Auto);
            return _servers[SelectedMailServer];
        }

        // ===== MAIN MAIL PROCESSOR =====
        private async Task ProcessInboxAsync(ImapClient client)
        {
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var any = await inbox.SearchAsync(SearchQuery.All);
            if (any.Count == 0)
            {
                AddLog("📭 Писем не найдено");
                StatusMessage = "Нет писем";
                SetProgress(0, details: "Почтовый ящик пуст");
                return;
            }

            // подготовка шкалы прогресса
            TotalSuppliers = _suppliers.Count;
            ProcessedSuppliers = 0;
            SetProgress(0, details: "Начинаю обработку…");
            await Task.Yield();

            for (int i = 0; i < _suppliers.Count; i++)
            {
                var (supplierName, fromEmail) = _suppliers[i];

                // участок прогресса, отведённый этому поставщику
                double span = 100.0 / Math.Max(1, TotalSuppliers);
                double baseStart = i * span;
                double baseEnd = (i + 1) * span;

                try
                {
                    SetProgress(baseStart + span * 0.05, supplierName, $"Поиск последнего письма от {fromEmail}…");
                    var uids = await inbox.SearchAsync(SearchQuery.FromContains(fromEmail));

                    if (uids == null || uids.Count == 0)
                    {
                        AddLog($"— Писем от {supplierName} ({fromEmail}) не найдено");
                        SetProgress(baseEnd, supplierName, "Писем не найдено");
                        ProcessedSuppliers++;
                        continue;
                    }

                    var lastUid = uids[uids.Count - 1];
                    SetProgress(baseStart + span * 0.15, supplierName, "Получаю письмо…");
                    var msg = await inbox.GetMessageAsync(lastUid);

                    var date = msg.Date.LocalDateTime;
                    var dateDir = Path.Combine(PricesRoot, date.ToString("dd-MM-yyyy"));
                    if (!TryEnsureDirectory(dateDir, $"для даты {date:dd-MM-yyyy} ({supplierName})"))
                    {
                        SetProgress(baseEnd, supplierName, "Ошибка доступа к папке");
                        ProcessedSuppliers++;
                        await Task.Yield();
                        continue;
                    }

                    AddLog($"✉️ Последнее письмо от {supplierName} ({fromEmail}) → {date:dd-MM-yyyy}");

                    // Разбиваем вложения по типам
                    var parts = msg.Attachments.OfType<MimePart>().ToList();
                    var zips = parts.Where(p => Path.GetExtension(p.FileName ?? "").Equals(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
                    var xlsxs = parts.Where(p => Path.GetExtension(p.FileName ?? "").Equals(".xlsx", StringComparison.OrdinalIgnoreCase)).ToList();
                    var csvs = parts.Where(p => Path.GetExtension(p.FileName ?? "").Equals(".csv", StringComparison.OrdinalIgnoreCase)).ToList();

                    int totalJobs = zips.Count + xlsxs.Count + csvs.Count;
                    int doneJobs = 0;

                    // чуть сдвигаем, чтобы было видно движение ещё до вложений
                    SetProgress(baseStart + span * 0.20, supplierName, "Подготовка к обработке вложений…");
                    await Task.Yield();

                    // ZIP (поддержка .xlsx и .csv внутри архива)
                    foreach (var part in zips)
                    {
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"Архив: {part.FileName}");
                        await ExtractZipAsync(part, dateDir, supplierName, date);

                        doneJobs++;
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"Готово: {part.FileName}");
                        await Task.Yield();
                    }

                    // XLSX
                    foreach (var part in xlsxs)
                    {
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"Сохранение: {part.FileName}");
                        await SaveXlsxAsync(part, dateDir, supplierName, date);

                        doneJobs++;
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"Готово: {part.FileName}");
                        await Task.Yield();
                    }

                    // CSV → XLSX → переименование
                    foreach (var part in csvs)
                    {
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"CSV→XLSX: {part.FileName}");
                        var xlsxPath = await ConvertCsvMimeToXlsxAsync(part, dateDir);
                        await RenameFileAsync(xlsxPath, dateDir, supplierName, date);

                        doneJobs++;
                        SetProgress(baseStart + span * (0.20 + 0.60 * (double)doneJobs / Math.Max(1, totalJobs)),
                                    supplierName, $"Готово: {part.FileName}");
                        await Task.Yield();
                    }

                    // очистка
                    SetProgress(baseStart + span * 0.90, supplierName, "Очистка временных файлов…");
                    Cleanup(dateDir);

                    // финал по поставщику
                    SetProgress(baseEnd, supplierName, "Завершено");
                    ProcessedSuppliers++;
                    await Task.Yield();
                }
                catch (Exception ex)
                {
                    AddLog($"❌ Ошибка для {supplierName}: {ex.Message}");
                    SetProgress(baseEnd, supplierName, "Ошибка");
                    ProcessedSuppliers++;
                }
            }

            // общий финал
            SetProgress(100, details: "Готово");
            AddLog($"✅ Обработано поставщиков: {ProcessedSuppliers}/{TotalSuppliers}");
            StatusMessage = $"Обработано: {ProcessedSuppliers}";
        }

        private static string SafeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        // ===== ZIP: поддерживаем .xlsx и .csv внутри архива =====
        private async Task ExtractZipAsync(MimePart zip, string dir, string supplier, DateTime date)
        {
            try
            {
                var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
                using (var fs = File.Create(temp))
                    await zip.Content.DecodeToAsync(fs);

                using (var archive = ZipFile.OpenRead(temp))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name)) continue;

                        var extractedPath = Path.Combine(dir, entry.Name);
                        entry.ExtractToFile(extractedPath, true);

                        var ext = Path.GetExtension(entry.Name);
                        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            await RenameFileAsync(extractedPath, dir, supplier, date);
                        }
                        else if (ext.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                        {
                            // CSV внутри архива → делаем XLSX и переименовываем, CSV потом удалится в Cleanup
                            var xlsxPath = await ConvertCsvFileToXlsxAsync(extractedPath, dir);
                            await RenameFileAsync(xlsxPath, dir, supplier, date);
                        }
                    }
                }
                File.Delete(temp);
                AddLog($"📦 Распакован архив {zip.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка распаковки {zip.FileName}: {ex.Message}");
            }
        }

        // ===== CSV helpers (общие для MIME и файлов из ZIP) =====
        private static char DetectDelimiter(string line)
        {
            if (string.IsNullOrEmpty(line)) return ';';
            int sc = line.Count(c => c == ';');
            int cc = line.Count(c => c == ',');
            return sc >= cc ? ';' : ',';
        }

        private static List<string> ParseCsvLine(string line, char delim)
        {
            var res = new List<string>();
            if (line == null) { res.Add(string.Empty); return res; }

            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // экранированные кавычки внутри поля
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delim && !inQuotes)
                {
                    res.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            res.Add(cur.ToString());
            return res;
        }

        // CSV пришёл как вложение (MimePart)
        private async Task<string> ConvertCsvMimeToXlsxAsync(MimePart csvPart, string targetDir)
        {
            var tempCsv = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
            try
            {
                using (var fs = File.Create(tempCsv))
                    await csvPart.Content.DecodeToAsync(fs);

                return await ConvertCsvFileToXlsxAsync(tempCsv, targetDir);
            }
            finally
            {
                try { if (File.Exists(tempCsv)) File.Delete(tempCsv); } catch { /* ignore */ }
            }
        }

        // CSV уже лежит на диске (например, из ZIP)
        private async Task<string> ConvertCsvFileToXlsxAsync(string csvPath, string targetDir)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(csvPath);
                var delimiter = lines.Length > 0 ? DetectDelimiter(lines[0]) : ';';

                var xlsxTempPath = Path.Combine(targetDir, $"csv_{Guid.NewGuid():N}.xlsx");
                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var ws = package.Workbook.Worksheets.Add("Sheet1");

                    for (int r = 0; r < lines.Length; r++)
                    {
                        var cells = ParseCsvLine(lines[r], delimiter);
                        for (int c = 0; c < cells.Count; c++)
                            ws.Cells[r + 1, c + 1].Value = cells[c];
                    }

                    package.SaveAs(new FileInfo(xlsxTempPath));
                }

                return xlsxTempPath;
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка CSV→XLSX ({Path.GetFileName(csvPath)}): {ex.Message}");
                throw;
            }
        }

        private async Task SaveXlsxAsync(MimePart xlsx, string dir, string supplier, DateTime date)
        {
            try
            {
                var tempName = $"{SafeName(supplier)}_{date:ddMMyyyy}_{Guid.NewGuid():N}.xlsx";
                var path = Path.Combine(dir, tempName);
                using (var fs = File.Create(path))
                    await xlsx.Content.DecodeToAsync(fs);

                await RenameFileAsync(path, dir, supplier, date);
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка сохранения {xlsx.FileName}: {ex.Message}");
            }
        }

        private async Task RenameFileAsync(string path, string dir, string supplier, DateTime date)
        {
            try
            {
                var baseName = $"{SafeName(supplier)}_{date:ddMMyyyy}";
                var target = Path.Combine(dir, baseName + ".xlsx");
                int counter = 1;

                while (File.Exists(target))
                {
                    target = Path.Combine(dir, $"{baseName}_{counter}.xlsx");
                    counter++;
                }

                File.Move(path, target, true);
                AddLog($"✅ {Path.GetFileName(path)} → {Path.GetFileName(target)}");
                await Task.Delay(20);
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка переименования: {ex.Message}");
            }
        }

        private void Cleanup(string dir)
        {
            int deleted = 0;
            foreach (var file in Directory.GetFiles(dir))
            {
                if (!file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(file); deleted++; }
                    catch { /* ignore */ }
                }
            }
            if (deleted > 0) AddLog($"🧹 Удалено не-xlsx файлов: {deleted}");
        }

        [RelayCommand]
        private void SaveGmailPreset()
        {
            if (SelectedMailServer != MailServerType.Gmail)
            {
                UpdateStatus("Пресеты доступны только для Gmail");
                return;
            }

            if (string.IsNullOrWhiteSpace(EmailAddress))
            {
                UpdateStatus("Введите адрес Gmail для сохранения пресета");
                return;
            }

            try
            {
                var presetInfo = SecureCreds.SavePreset(MailServerType.Gmail, EmailAddress, Password, EmailAddress);
                var displayName = string.IsNullOrWhiteSpace(presetInfo.DisplayName)
                    ? presetInfo.Email
                    : presetInfo.DisplayName;

                var existing = GmailPresets.FirstOrDefault(p =>
                    string.Equals(p.Id, presetInfo.Id, StringComparison.Ordinal));

                if (existing == null)
                {
                    var newPreset = new CredentialPreset(presetInfo.Id, displayName, presetInfo.Email);
                    GmailPresets.Add(newPreset);
                    SelectedGmailPreset = newPreset;
                }
                else
                {
                    var index = GmailPresets.IndexOf(existing);
                    if (index >= 0)
                    {
                        var updated = new CredentialPreset(presetInfo.Id, displayName, presetInfo.Email);
                        GmailPresets[index] = updated;
                        SelectedGmailPreset = updated;
                    }
                }

                AddLog($"💾 Сохранён пресет Gmail для {presetInfo.Email}");
                UpdateStatus($"Сохранён пресет Gmail: {displayName}");
            }
            catch (Exception ex)
            {
                ReportError($"Не удалось сохранить пресет Gmail: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private void DeleteGmailPreset()
        {
            var preset = SelectedGmailPreset;
            if (preset == null)
                return;

            try
            {
                if (SecureCreds.RemovePreset(preset.Id))
                {
                    GmailPresets.Remove(preset);
                    SelectedGmailPreset = null;
                    AddLog($"🗑️ Удалён пресет Gmail: {preset.DisplayName}");
                    UpdateStatus($"Удалён пресет Gmail: {preset.DisplayName}");
                }
                else
                {
                    ReportError($"Не удалось удалить пресет Gmail: {preset.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                ReportError($"Не удалось удалить пресет Gmail: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private void ClearCredentials()
        {
            EmailAddress = "";
            Password = "";
            StatusMessage = "Данные очищены";
            AddLog("🧹 Данные очищены (поля ввода)");
        }

        [RelayCommand]
        private void ForgetSavedCredentials()
        {
            SecureCreds.Clear();
            AddLog("🗑️ Сохранённые учётные данные удалены");
        }

        [RelayCommand]
        private void OpenPricesFolder()
        {
            if (!EnsurePricesRoot())
                return;

            try
            {
                var shellPath = GetShellAccessiblePath(PricesRoot, ensureExists: true);

                if (!Directory.Exists(shellPath))
                {
                    ReportError($"Папка не найдена: {shellPath}");
                    return;
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shellPath,
                    UseShellExecute = true,
                    Verb = "open"
                };
                System.Diagnostics.Process.Start(startInfo);
                if (!string.Equals(shellPath, PricesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"📁 Открыта папка {shellPath} (логический путь: {PricesRoot})");
                    UpdateStatus($"Открыта папка: {shellPath}");
                }
                else
                {
                    AddLog($"📁 Открыта папка {shellPath}");
                    UpdateStatus($"Открыта папка: {shellPath}");
                }
            }
            catch (Exception ex)
            {
                ReportError($"Не удалось открыть папку с прайс-листами: {ex.Message}", ex);
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

        [ObservableProperty] private int selectedMailServerIndex = 1; // Mail.ru
        partial void OnSelectedMailServerIndexChanged(int value)
            => SelectedMailServer = (MailServerType)value;

        // ===== НАСТРОЙКИ ПОЛЬЗОВАТЕЛЯ =====
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

        // ===== ЛОКАЛЬНОЕ ХРАНИЛИЩЕ КРЕДОВ (без БД) =====
        private static class SecureCreds
        {
            private static readonly string Dir =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DromHub");
            private static readonly string FilePath = Path.Combine(Dir, "creds.json");

            public sealed class CredentialPresetInfo
            {
                public CredentialPresetInfo(string id, string email, string displayName, MailServerType server)
                {
                    Id = id;
                    Email = email;
                    DisplayName = displayName;
                    Server = server;
                }

                public string Id { get; }
                public string Email { get; }
                public string DisplayName { get; }
                public MailServerType Server { get; }
            }

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
                public List<CredentialData> Presets { get; set; } = new();
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

            public static CredentialPresetInfo SavePreset(MailServerType server, string email, string password, string? displayName)
            {
                var store = LoadStore();
                store.Presets ??= new List<CredentialData>();
                var serverKey = server.ToString();
                var existing = store.Presets.FirstOrDefault(p =>
                    string.Equals(p.Server, serverKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    existing = new CredentialData();
                    store.Presets.Add(existing);
                }

                existing.Email = email ?? string.Empty;
                existing.DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName;
                existing.Server = serverKey;
                existing.Pwd = Protect(password);

                SaveOrDelete(store);

                var info = new CredentialPresetInfo(
                    existing.Id,
                    existing.Email,
                    existing.DisplayName ?? existing.Email,
                    server);
                return info;
            }

            public static IReadOnlyList<CredentialPresetInfo> LoadPresets(MailServerType? filter = null)
            {
                try
                {
                    var store = LoadStore();
                    var serverFilter = filter?.ToString();

                    var presets = store.Presets
                        .Where(p => string.IsNullOrEmpty(serverFilter) ||
                                    string.Equals(p.Server, serverFilter, StringComparison.OrdinalIgnoreCase))
                        .Select(p => new CredentialPresetInfo(
                            p.Id,
                            p.Email ?? string.Empty,
                            string.IsNullOrWhiteSpace(p.DisplayName) ? (p.Email ?? string.Empty) : p.DisplayName!,
                            ParseServer(p.Server)))
                        .ToList();

                    return presets;
                }
                catch
                {
                    return Array.Empty<CredentialPresetInfo>();
                }
            }

            public static bool TryLoadPresetPassword(string id, out string password)
            {
                password = string.Empty;
                try
                {
                    var store = LoadStore();
                    var preset = store.Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
                    if (preset == null || string.IsNullOrEmpty(preset.Pwd))
                        return false;

                    password = Unprotect(preset.Pwd);
                    return true;
                }
                catch
                {
                    password = string.Empty;
                    return false;
                }
            }

            public static bool RemovePreset(string id)
            {
                try
                {
                    var store = LoadStore();
                    var removed = store.Presets.RemoveAll(p => string.Equals(p.Id, id, StringComparison.Ordinal)) > 0;
                    if (removed)
                        SaveOrDelete(store);
                    return removed;
                }
                catch
                {
                    return false;
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
                        store.Presets ??= new List<CredentialData>();
                        if (store.Default != null && string.IsNullOrEmpty(store.Default.Id))
                            store.Default.Id = "default";

                        if (store.Default == null && store.Presets.Count == 0)
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
                            var store = new CredentialStore
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
                            return store;
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
                store.Presets ??= new List<CredentialData>();

                if (store.Default == null && store.Presets.Count == 0)
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
                    var pwdBytes = System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty);
                    var protectedBytes = ProtectedData.Protect(pwdBytes, null, DataProtectionScope.CurrentUser);
                    return Convert.ToBase64String(protectedBytes);
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static string Unprotect(string protectedBase64)
            {
                try
                {
                    if (string.IsNullOrEmpty(protectedBase64))
                        return string.Empty;

                    var prot = Convert.FromBase64String(protectedBase64);
                    var plain = ProtectedData.Unprotect(prot, null, DataProtectionScope.CurrentUser);
                    return System.Text.Encoding.UTF8.GetString(plain);
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static MailServerType ParseServer(string? value)
            {
                if (Enum.TryParse<MailServerType>(value, out var parsed))
                    return parsed;
                return MailServerType.Custom;
            }
        }
    }
}
