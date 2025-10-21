using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
using OfficeOpenXml;

namespace DromHub.ViewModels
{
    public partial class MailParserViewModel : ObservableObject
    {
        private readonly ILogger<MailParserViewModel> _logger;
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private readonly UserSettings _userSettings;

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
            if (SecureCreds.TryLoad(out var savedEmail, out var savedPassword, out var savedServer))
            {
                EmailAddress = savedEmail ?? "";
                Password = savedPassword ?? "";
                RememberCredentials = true;
                AddLog("🔑 Найдены сохранённые учётные данные");
                SelectedMailServer = savedServer;
                SelectedMailServerIndex = (int)savedServer;
            }

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

        private string GetServerLabel(MailServerType server)
        {
            return server switch
            {
                MailServerType.Gmail => "Gmail",
                MailServerType.MailRu => "Mail.ru",
                MailServerType.Yandex => "Yandex",
                MailServerType.Custom => string.IsNullOrWhiteSpace(CustomServer)
                    ? "пользовательский сервер"
                    : CustomServer,
                _ => server.ToString()
            };
        }

        // ===== PROPERTIES =====
        [ObservableProperty] private MailServerType selectedMailServer = MailServerType.MailRu; // по умолчанию Mail.ru
        [ObservableProperty] private string emailAddress = "";
        [ObservableProperty] private string password = "";

        public void UpdatePassword(string? password)
        {
            Password = password ?? string.Empty;
        }

        [ObservableProperty] private string customServer = "imap.example.com";
        [ObservableProperty] private int customPort = 993;

        [ObservableProperty] private string statusMessage = "Готов к работе";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private Visibility customServerVisibility = Visibility.Collapsed;
        [ObservableProperty] private ObservableCollection<string> logEntries = new();

        // «Запомнить меня» (локально, без БД)
        [ObservableProperty] private bool rememberCredentials = true;

        // Прогресс парсинга
        [ObservableProperty] private double parsingProgress;          // 0..100
        [ObservableProperty] private string progressDetails = "";     // текст под прогрессом
        [ObservableProperty] private string currentSupplier = "";     // имя текущего поставщика
        [ObservableProperty] private int processedSuppliers;          // сколько обработали
        [ObservableProperty] private int totalSuppliers;              // всего в списке

        public IReadOnlyList<string> MailServerTypes { get; } = new[]
        {
            "Gmail (imap.gmail.com:993)",
            "Mail.ru (imap.mail.ru:993)",
            "Yandex (imap.yandex.ru:993)",
            "Другой сервер"
        };

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

        private void UpdateServerSelection(MailServerType server)
        {
            CustomServerVisibility = server == MailServerType.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        partial void OnSelectedMailServerChanged(MailServerType value)
        {
            UpdateServerSelection(value);
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

            using var client = CreateImapClient();
            try
            {
                await ConnectAsync(client, server, port, ssl);
                await AuthenticateAsync(client, EmailAddress, Password);

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
                    await DisconnectAsync(client);

                IsConnected = false;
                IsLoading = false;
                AddLog("📨 Сессия завершена");
            }
        }

        protected virtual ImapClient CreateImapClient() => new ImapClient();

        protected virtual Task ConnectAsync(ImapClient client, string server, int port, SecureSocketOptions options)
        {
            return client.ConnectAsync(server, port, options);
        }

        protected virtual Task AuthenticateAsync(ImapClient client, string email, string password)
        {
            return client.AuthenticateAsync(email, password);
        }

        protected virtual Task DisconnectAsync(ImapClient client)
        {
            return client.DisconnectAsync(true);
        }

        private (string, int, SecureSocketOptions) GetServer()
        {
            if (SelectedMailServer == MailServerType.Custom)
                return (CustomServer, CustomPort, SecureSocketOptions.Auto);
            return _servers[SelectedMailServer];
        }

        // ===== MAIN MAIL PROCESSOR =====
        protected virtual async Task ProcessInboxAsync(ImapClient client)
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

        [ObservableProperty] private int selectedMailServerIndex = 1; // Mail.ru
        partial void OnSelectedMailServerIndexChanged(int value)
            => SelectedMailServer = (MailServerType)value;
    }
}
