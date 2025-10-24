using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Models;
using DromHub.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace DromHub.ViewModels
{
    /// <summary>
    /// ViewModel страницы с историей изменений бренда.
    /// </summary>
    public partial class BrandChangeLogViewModel : ObservableObject
    {
        private readonly ChangeLogService _changeLogService;
        private CancellationTokenSource? _loadCts;

        /// <summary>
        /// Создаёт экземпляр view-model.
        /// </summary>
        public BrandChangeLogViewModel(ChangeLogService changeLogService)
        {
            _changeLogService = changeLogService;
            Patches = new ObservableCollection<ChangeLogPatchGroup>();
            LoadCommand = new AsyncRelayCommand<Guid>(LoadAsync);
        }

        /// <summary>
        /// Коллекция патчей для отображения.
        /// </summary>
        public ObservableCollection<ChangeLogPatchGroup> Patches { get; }

        /// <summary>
        /// Команда асинхронной загрузки истории.
        /// </summary>
        public IAsyncRelayCommand<Guid> LoadCommand { get; }

        [ObservableProperty]
        private Guid brandId;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool hasHistory;

        [ObservableProperty]
        private string emptyStateMessage = "Для бренда пока нет зафиксированных изменений.";

        /// <summary>
        /// Загружает историю изменений для указанного бренда.
        /// </summary>
        public async Task LoadAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                Reset();
                return;
            }

            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();

            BrandId = id;
            IsBusy = true;

            try
            {
                var history = await _changeLogService.GetBrandHistoryAsync(id, _loadCts.Token);
                ApplyHistory(history);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                IsBusy = false;
                _loadCts?.Dispose();
                _loadCts = null;
            }
        }

        private void ApplyHistory(IReadOnlyList<ChangeLogPatchResult> patches)
        {
            Patches.Clear();

            foreach (var patch in patches)
            {
                var patchVm = new ChangeLogPatchGroup(
                    patch.PatchId,
                    patch.Version,
                    patch.Title,
                    patch.ReleaseDate,
                    patch.Sections
                        .Select(section => new ChangeLogSectionGroup(
                            section.SectionId,
                            section.Title,
                            section.Category,
                            section.Entries
                                .Select(entry => new ChangeLogEntryItem(
                                    entry.EntryId,
                                    entry.Headline,
                                    entry.Description,
                                    entry.ImpactLevel,
                                    entry.IconAsset,
                                    entry.BrandName,
                                    entry.PartName,
                                    entry.PartCatalogNumber))
                                .ToList()))
                        .ToList());

                Patches.Add(patchVm);
            }

            HasHistory = Patches.Count > 0;
        }

        /// <summary>
        /// Сбрасывает состояние view-model.
        /// </summary>
        public void Reset()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;

            BrandId = Guid.Empty;
            Patches.Clear();
            HasHistory = false;
        }
    }

    /// <summary>
    /// Представление патча для UI.
    /// </summary>
    public sealed class ChangeLogPatchGroup
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        public ChangeLogPatchGroup(Guid id, string version, string? title, DateTime releaseDate, IList<ChangeLogSectionGroup> sections)
        {
            PatchId = id;
            Version = version;
            Title = title;
            ReleaseDate = releaseDate;
            Sections = new ReadOnlyCollection<ChangeLogSectionGroup>(sections);
        }

        public Guid PatchId { get; }
        public string Version { get; }
        public string? Title { get; }
        public DateTime ReleaseDate { get; }
        public IReadOnlyList<ChangeLogSectionGroup> Sections { get; }

        public string HeaderDisplay => string.IsNullOrWhiteSpace(Title) ? Version : $"{Version} — {Title}";

        public string ReleaseDateDisplay => ReleaseDate.ToString("dd MMMM yyyy", RuCulture);
    }

    /// <summary>
    /// Представление раздела патча.
    /// </summary>
    public sealed class ChangeLogSectionGroup
    {
        private static readonly IReadOnlyDictionary<ChangeLogCategory, (string Name, SolidColorBrush Brush)> CategoryMap =
            new Dictionary<ChangeLogCategory, (string, SolidColorBrush)>
            {
                [ChangeLogCategory.Brand] = ("Бренд", CreateBrush("#FF4C7CF3")),
                [ChangeLogCategory.Parts] = ("Детали", CreateBrush("#FF53C678")),
                [ChangeLogCategory.Pricing] = ("Цены", CreateBrush("#FFE5A323")),
                [ChangeLogCategory.General] = ("Общее", CreateBrush("#FF7F8C8D")),
                [ChangeLogCategory.Logistics] = ("Логистика", CreateBrush("#FF36C2D8"))
            };

        public ChangeLogSectionGroup(Guid id, string title, ChangeLogCategory category, IList<ChangeLogEntryItem> entries)
        {
            SectionId = id;
            Title = title;
            Category = category;
            Entries = new ReadOnlyCollection<ChangeLogEntryItem>(entries);

            if (!CategoryMap.TryGetValue(category, out var info))
            {
                info = (category.ToString(), CreateBrush("#FF7F8C8D"));
            }

            CategoryDisplay = info.Name;
            AccentBrush = info.Brush;
        }

        public Guid SectionId { get; }
        public string Title { get; }
        public ChangeLogCategory Category { get; }
        public IReadOnlyList<ChangeLogEntryItem> Entries { get; }
        public string CategoryDisplay { get; }
        public SolidColorBrush AccentBrush { get; }

        private static SolidColorBrush CreateBrush(string hex)
        {
            var color = ParseColor(hex);
            return new SolidColorBrush(color);
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Colors.Gray;
            }

            var span = hex.AsSpan().TrimStart('#');
            byte a = 255;
            int idx = 0;

            if (span.Length == 8)
            {
                a = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
                idx += 2;
            }

            var r = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var g = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var b = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);

            return ColorHelper.FromArgb(a, r, g, b);
        }
    }

    /// <summary>
    /// Представление отдельной записи патча.
    /// </summary>
    public sealed class ChangeLogEntryItem
    {
        private static readonly IReadOnlyDictionary<ChangeLogImpactLevel, (string Label, SolidColorBrush Brush)> ImpactMap =
            new Dictionary<ChangeLogImpactLevel, (string, SolidColorBrush)>
            {
                [ChangeLogImpactLevel.Low] = ("Низкий", CreateBrush("#FF5DADE2")),
                [ChangeLogImpactLevel.Medium] = ("Средний", CreateBrush("#FFF1C40F")),
                [ChangeLogImpactLevel.High] = ("Высокий", CreateBrush("#FFE74C3C")),
                [ChangeLogImpactLevel.Critical] = ("Критичный", CreateBrush("#FF922B21"))
            };

        public ChangeLogEntryItem(
            Guid id,
            string? headline,
            string description,
            ChangeLogImpactLevel impactLevel,
            string? iconAsset,
            string? brandName,
            string? partName,
            string? partCatalog)
        {
            EntryId = id;
            Headline = string.IsNullOrWhiteSpace(headline) ? "Обновление" : headline;
            Description = description;
            ImpactLevel = impactLevel;
            IconAsset = string.IsNullOrWhiteSpace(iconAsset) ? "/Assets/info.svg" : iconAsset;
            BrandName = brandName;
            PartName = partName;
            PartCatalogNumber = partCatalog;

            if (!ImpactMap.TryGetValue(impactLevel, out var info))
            {
                info = (impactLevel.ToString(), CreateBrush("#FF5DADE2"));
            }

            ImpactLabel = info.Label;
            ImpactBrush = info.Brush;
        }

        public Guid EntryId { get; }
        public string Headline { get; }
        public string Description { get; }
        public ChangeLogImpactLevel ImpactLevel { get; }
        public string IconAsset { get; }
        public string? BrandName { get; }
        public string? PartName { get; }
        public string? PartCatalogNumber { get; }
        public string ImpactLabel { get; }
        public SolidColorBrush ImpactBrush { get; }

        public string IconAssetUri => IconAsset.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase)
            ? IconAsset
            : $"ms-appx:///{IconAsset.TrimStart('/')}";

        public string? TargetSummary
        {
            get
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(BrandName))
                {
                    parts.Add(BrandName);
                }

                if (!string.IsNullOrWhiteSpace(PartName))
                {
                    var catalog = string.IsNullOrWhiteSpace(PartCatalogNumber) ? string.Empty : $" ({PartCatalogNumber})";
                    parts.Add($"{PartName}{catalog}");
                }

                return parts.Count == 0 ? null : string.Join(" • ", parts);
            }
        }

        private static SolidColorBrush CreateBrush(string hex) => new SolidColorBrush(ParseColor(hex));

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return Colors.Gray;
            }

            var span = hex.AsSpan().TrimStart('#');
            byte a = 255;
            int idx = 0;

            if (span.Length == 8)
            {
                a = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
                idx += 2;
            }

            var r = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var g = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);
            idx += 2;
            var b = Convert.ToByte(span.Slice(idx, 2).ToString(), 16);

            return ColorHelper.FromArgb(a, r, g, b);
        }
    }
}
