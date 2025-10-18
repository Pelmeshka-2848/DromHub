using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
using OfficeOpenXml;                 // EPPlus
using Windows.Storage.Pickers;       // для кнопки "Добавить прайс-лист" (по желанию)
using WinRT.Interop;                 // InitializeWithWindow

namespace DromHub.ViewModels
{
    public partial class MailParserViewModel : ObservableObject
    {
        private readonly ILogger<MailParserViewModel> _logger;
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

        public MailParserViewModel(ILogger<MailParserViewModel> logger)
        {
            _logger = logger;

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedMailServer))
                    CustomServerVisibility = SelectedMailServer == MailServerType.Custom
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            };
        }

        // ========== ENUM / SERVER CONFIG ==========
        public enum MailServerType { Gmail, MailRu, Yandex, Custom }

        private readonly Dictionary<MailServerType, (string Server, int Port, SecureSocketOptions Ssl)> _servers = new()
        {
            { MailServerType.Gmail,  ("imap.gmail.com",  993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.MailRu, ("imap.mail.ru",    993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.Yandex, ("imap.yandex.ru",  993, SecureSocketOptions.SslOnConnect) },
            { MailServerType.Custom, ("imap.example.com",993, SecureSocketOptions.Auto) }
        };

        // ========== ПАРАМЕТРЫ UI ==========
        [ObservableProperty] private MailServerType selectedMailServer = MailServerType.Gmail;
        [ObservableProperty] private string emailAddress = "";
        [ObservableProperty] private string password = "";
        [ObservableProperty] private string customServer = "imap.example.com";
        [ObservableProperty] private int customPort = 993;
        [ObservableProperty] private string statusMessage = "Готов к работе";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private Visibility customServerVisibility = Visibility.Collapsed;
        [ObservableProperty] private ObservableCollection<string> logEntries = new();

        // Корневая папка для прайсов
        private static readonly string PricesRoot = @"E:\CSharp\DromHub\DromHub\Prices";

        // ========== СПИСОК ПОСТАВЩИКОВ (ловим по адресу) ==========
        private readonly Supplier[] _suppliers =
        {
            new Supplier("AllMyParts", "eugenestulev@gmail.com"),
            new Supplier("Rossko",     "price@rossko.ru"),
            new Supplier("MXGroup",    "no_reply@mxgroup.ru"),
            new Supplier("Uniqom",     "1c_info@uniqom.ru"),
            new Supplier("Berg",       "noreply@berg.ru"),
            new Supplier("AvtoMC",     "noreply@api.avto-ms.ru"),
        };

        private record Supplier(string Name, string Email);

        public List<string> MailServerTypes => new()
        {
            "Gmail (imap.gmail.com:993)",
            "Mail.ru (imap.mail.ru:993)",
            "Yandex (imap.yandex.ru:993)",
            "Другой сервер"
        };

        // ========== ЛОГ ==========
        private void AddLog(string message)
        {
            _dispatcher.TryEnqueue(() =>
            {
                LogEntries.Add($"{DateTime.Now:HH:mm:ss} — {message}");
            });
        }

        // ========== КОМАНДЫ ==========
        [RelayCommand]
        private async Task ConnectAndDownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailAddress) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Введите email и пароль";
                return;
            }

            IsLoading = true;
            AddLog($"Подключение к {SelectedMailServer}...");

            var (server, port, ssl) = GetServer();

            using var client = new ImapClient();
            try
            {
                await client.ConnectAsync(server, port, ssl);
                await client.AuthenticateAsync(EmailAddress, Password);

                IsConnected = true;
                AddLog("✅ Подключение успешно");
                StatusMessage = "Подключено";

                // СТАРАЯ ЛОГИКА + новая выборка: по одному последнему письму на поставщика
                await ProcessInboxPerSupplierAsync(client);
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

        [ObservableProperty] private int selectedMailServerIndex;
        partial void OnSelectedMailServerIndexChanged(int value)
            => SelectedMailServer = (MailServerType)value;

        [RelayCommand]
        private void ClearCredentials()
        {
            EmailAddress = "";
            Password = "";
            StatusMessage = "Данные очищены";
            AddLog("🧹 Данные очищены");
        }

        [RelayCommand]
        private void OpenPricesFolder()
        {
            try
            {
                Directory.CreateDirectory(PricesRoot);
                System.Diagnostics.Process.Start("explorer.exe", PricesRoot);
                AddLog($"📁 Открыта папка {PricesRoot}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка открытия папки: {ex.Message}");
            }
        }

        // (по желанию) Кнопка «Добавить прайс-лист» — выбрать файл и обработать его, как из почты
        [RelayCommand]
        private async Task AddPriceListAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                InitPickerWithMainWindow(picker);
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".csv");
                picker.FileTypeFilter.Add(".zip");

                var file = await picker.PickSingleFileAsync();
                if (file == null) { AddLog("Отмена выбора файла"); return; }

                Directory.CreateDirectory(PricesRoot);
                var date = DateTime.Now;
                var dateDir = Path.Combine(PricesRoot, date.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(dateDir);

                var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                var supplier = "Manual";

                if (ext == ".zip")
                {
                    // Скопировать во временный zip и распаковать по старой логике
                    var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
                    using (var src = await file.OpenStreamForReadAsync())
                    using (var dst = File.Create(tempZip))
                        await src.CopyToAsync(dst);

                    await ExtractZipFileAsync(tempZip, dateDir, supplier, date);
                    File.Delete(tempZip);
                }
                else if (ext == ".xlsx")
                {
                    var tempX = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
                    using (var src = await file.OpenStreamForReadAsync())
                    using (var dst = File.Create(tempX))
                        await src.CopyToAsync(dst);

                    await RenameFileAsync(tempX, dateDir, supplier, date);
                }
                else if (ext == ".csv")
                {
                    var tempCsv = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
                    using (var src = await file.OpenStreamForReadAsync())
                    using (var dst = File.Create(tempCsv))
                        await src.CopyToAsync(dst);

                    var tempXlsx = await ConvertCsvFileToXlsxAsync(tempCsv);
                    await RenameFileAsync(tempXlsx, dateDir, supplier, date);
                    SafeDelete(tempCsv);
                }

                Cleanup(dateDir);
                AddLog("✅ Прайс добавлен вручную");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка добавления прайса: {ex.Message}\nConsider WindowNative, InitializeWithWindow");
            }
        }

        // ========== ОСНОВНАЯ ЛОГИКА: по одному последнему письму от каждого поставщика ==========
        private async Task ProcessInboxPerSupplierAsync(ImapClient client)
        {
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            AddLog($"INBOX открыт. Поставщиков для обхода: {_suppliers.Length}");

            int processedSuppliers = 0;
            foreach (var s in _suppliers)
            {
                try
                {
                    // Ищем письма от адреса поставщика
                    var uids = await inbox.SearchAsync(MailKit.Search.SearchQuery.FromContains(s.Email));
                    if (uids == null || uids.Count == 0)
                    {
                        AddLog($"📭 Нет писем от {s.Name} <{s.Email}>");
                        continue;
                    }

                    // Берём самое позднее
                    var lastUid = uids.Last(); // сервер обычно отдаёт по возрастанию, но не гарантия — этого достаточно
                    var msg = await inbox.GetMessageAsync(lastUid);
                    var date = msg.Date.LocalDateTime;

                    var dateDir = Path.Combine(PricesRoot, date.ToString("yyyy-MM-dd"));
                    Directory.CreateDirectory(dateDir);

                    AddLog($"✉️ {s.Name}: письмо от {date:dd.MM.yyyy HH:mm}, тема: {msg.Subject}");

                    // Собираем вложения
                    var parts = msg.Attachments.OfType<MimePart>().ToList();
                    if (parts.Count == 0)
                    {
                        AddLog($"⚠️ У {s.Name} нет вложений");
                        continue;
                    }

                    // Сначала .zip, затем .xlsx, затем .csv
                    foreach (var part in parts)
                    {
                        var ext = Path.GetExtension(part.FileName ?? "").ToLowerInvariant();
                        if (ext == ".zip")
                        {
                            await ExtractZipAttachmentAsync(part, dateDir, s.Name, date);
                        }
                    }

                    foreach (var part in parts)
                    {
                        var ext = Path.GetExtension(part.FileName ?? "").ToLowerInvariant();
                        if (ext == ".xlsx")
                        {
                            await SaveXlsxAttachmentAsync(part, dateDir, s.Name, date);
                        }
                    }

                    foreach (var part in parts)
                    {
                        var ext = Path.GetExtension(part.FileName ?? "").ToLowerInvariant();
                        if (ext == ".csv")
                        {
                            await SaveCsvAttachmentAsXlsxAsync(part, dateDir, s.Name, date);
                        }
                    }

                    Cleanup(dateDir);
                    processedSuppliers++;
                }
                catch (Exception ex)
                {
                    AddLog($"❌ Ошибка {s.Name}: {ex.Message}");
                    _logger.LogError(ex, "Ошибка обработки поставщика {Supplier}", s.Name);
                }
            }

            AddLog($"✅ Обработано поставщиков: {processedSuppliers}");
            StatusMessage = $"Обработано поставщиков: {processedSuppliers}";
        }

        // ========== ZIP / XLSX / CSV ==========
        private async Task ExtractZipAttachmentAsync(MimePart zipPart, string dateDir, string supplier, DateTime date)
        {
            try
            {
                var tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip");
                using (var fs = File.Create(tempZip))
                    await zipPart.Content.DecodeToAsync(fs);

                await ExtractZipFileAsync(tempZip, dateDir, supplier, date);
                File.Delete(tempZip);
                AddLog($"🗜️ Архив распакован: {zipPart.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка распаковки {zipPart.FileName}: {ex.Message}");
            }
        }

        private async Task ExtractZipFileAsync(string zipPath, string dateDir, string supplier, DateTime date)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                var extracted = Path.Combine(dateDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(extracted)!);
                entry.ExtractToFile(extracted, true);

                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (ext == ".xlsx")
                    await RenameFileAsync(extracted, dateDir, supplier, date);
                else if (ext == ".csv")
                {
                    // конвертим и тоже переименовываем
                    var xlsxTemp = await ConvertCsvFileToXlsxAsync(extracted);
                    await RenameFileAsync(xlsxTemp, dateDir, supplier, date);
                    SafeDelete(extracted);
                }
            }
        }

        private async Task SaveXlsxAttachmentAsync(MimePart xlsxPart, string dateDir, string supplier, DateTime date)
        {
            try
            {
                var temp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
                using (var fs = File.Create(temp))
                    await xlsxPart.Content.DecodeToAsync(fs);

                await RenameFileAsync(temp, dateDir, supplier, date);
                AddLog($"📄 XLSX сохранён: {xlsxPart.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка сохранения {xlsxPart.FileName}: {ex.Message}");
            }
        }

        private async Task SaveCsvAttachmentAsXlsxAsync(MimePart csvPart, string dateDir, string supplier, DateTime date)
        {
            try
            {
                var tempCsv = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
                using (var fs = File.Create(tempCsv))
                    await csvPart.Content.DecodeToAsync(fs);

                var tempXlsx = await ConvertCsvFileToXlsxAsync(tempCsv);
                await RenameFileAsync(tempXlsx, dateDir, supplier, date);
                SafeDelete(tempCsv);
                AddLog($"📄 CSV→XLSX: {csvPart.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка обработки CSV {csvPart.FileName}: {ex.Message}");
            }
        }

        private async Task<string> ConvertCsvFileToXlsxAsync(string csvPath)
        {
            // Преобразуем CSV в XLSX (EPPlus). Файл возвращаем как временный .xlsx
            var outXlsx = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

            // EPPlus: лицензия должна быть установлена в App.xaml.cs (см. наши правки ранее)
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Sheet1");
                var lines = await File.ReadAllLinesAsync(csvPath, DetectEncoding(csvPath));

                for (int r = 0; r < lines.Length; r++)
                {
                    var cells = SplitCsvLineSmart(lines[r]);
                    for (int c = 0; c < cells.Count; c++)
                        ws.Cells[r + 1, c + 1].Value = cells[c];
                }

                package.SaveAs(new FileInfo(outXlsx));
            }

            return outXlsx;
        }

        private static Encoding DetectEncoding(string path)
        {
            // На коленке: если есть BOM — используем его, иначе UTF8 без BOM
            using var sr = new StreamReader(path, true);
            sr.Peek(); // заставим определить Encoding
            return sr.CurrentEncoding ?? new UTF8Encoding(false);
        }

        private static List<string> SplitCsvLineSmart(string line)
        {
            // Быстрый CSV-парсер: учитывает кавычки; разделитель ',' или ';'
            // Если встречаются оба — приоритет ';'
            char sep = line.Contains(';') && !line.Contains(",") ? ';' :
                       line.Contains(';') && line.Contains(",") ? ';' : ',';

            var res = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"'); i++; // escaped quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == sep && !inQuotes)
                {
                    res.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
            res.Add(sb.ToString());
            return res;
        }

        // ========== ПЕРЕИМЕНОВАНИЕ / ОЧИСТКА ==========
        private async Task RenameFileAsync(string path, string dir, string supplier, DateTime date)
        {
            try
            {
                var baseName = $"{SafeName(supplier)}_{date:yyyyMMdd}";
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
            foreach (var file in Directory.GetFiles(dir))
            {
                if (!file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    SafeDelete(file);
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* noop */ }
        }

        private static string SafeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        // ========== УТИЛИТЫ ==========
        private (string, int, SecureSocketOptions) GetServer()
        {
            if (SelectedMailServer == MailServerType.Custom)
                return (CustomServer, CustomPort, SecureSocketOptions.Auto);
            return _servers[SelectedMailServer];
        }

        private static void InitPickerWithMainWindow(object picker)
        {
            // Требуется, если используешь кнопку "Добавить прайс-лист" в WinUI 3
            var hwnd = DromHub.App.MainHwnd;
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Main window handle is not initialized yet.");
            InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
