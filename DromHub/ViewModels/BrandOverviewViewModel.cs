using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace DromHub.ViewModels
{
    public partial class BrandOverviewViewModel : ObservableObject
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        public BrandOverviewViewModel(IDbContextFactory<ApplicationDbContext> dbFactory) => _dbFactory = dbFactory;

        private XamlRoot _xr;
        public Guid BrandId { get; private set; }

        // ===== БАЗОВЫЕ ПОЛЯ / ЧИПСЫ =====
        [ObservableProperty] private string brandName = "";
        public string BrandNameUpper => BrandName.ToUpperInvariant();
        public string Monogram => string.IsNullOrWhiteSpace(BrandName) ? "?" : BrandName.Trim()[0].ToString().ToUpperInvariant();

        [ObservableProperty] private bool isOem;
        public string OemAnalogText => IsOem ? "OEM" : "Аналог";

        [ObservableProperty] private string? website;
        public bool HasWebsite => Uri.TryCreate(Website, UriKind.Absolute, out _);
        public string WebsiteHost => HasWebsite ? new Uri(Website!).Host : "—";

        [ObservableProperty] private int? yearFounded;
        public int? AgeYears => YearFounded is int y && y > 1800 ? Math.Max(0, DateTime.UtcNow.Year - y) : null;

        // страна
        [ObservableProperty] private string countryName = "—";
        [ObservableProperty] private string countryIso2 = "—";
        [ObservableProperty] private string countryWorldIconAsset = "/Assets/globe.svg";

        // чипсы — значения
        public string ChipYearText => YearFounded?.ToString() ?? "—";
        public string ChipMarkupText => $"{MarkupPct:F1}";
        public string ChipCountryText => CountryIso2;
        public string ChipPartsText => PartsCount.ToString();
        public string ChipAgeText => AgeYears?.ToString() ?? "—";

        // описание / заметки
        [ObservableProperty] private string? description;
        [ObservableProperty] private string? userNotes;
        public string DescriptionOrPlaceholder => string.IsNullOrWhiteSpace(Description) ? "—" : Description!;
        public string UserNotesOrPlaceholder => string.IsNullOrWhiteSpace(UserNotes) ? "—" : UserNotes!;

        // счётчики
        [ObservableProperty] private int partsCount;
        [ObservableProperty] private int aliasesCount;

        // синонимы строкой
        [ObservableProperty] private string aliasesJoinedDisplay = "—";

        // избранное (локальный флажок UI)
        [ObservableProperty] private bool isFavorite;

        // ===== МЕТРИКИ (перцентили) слева =====
        [ObservableProperty] private double assortmentValue; // 0..100
        [ObservableProperty] private double assortmentDelta; // п.п. к медианному перцентилю (50)
        [ObservableProperty] private double awarenessValue;  // 0..100
        [ObservableProperty] private double awarenessDelta;  // п.п. к медианному перцентилю (50)
        [ObservableProperty] private double marginValue;     // 0..100 (перцентиль реальной маржи)
        [ObservableProperty] private double marginDelta;     // п.п. к медиане реальной маржи

        public string AssortmentDisplay => $"{AssortmentValue:F0}% {(AssortmentDelta >= 0 ? "+" : "−")}{Math.Abs(AssortmentDelta):F1}";
        public string AwarenessDisplay => $"{AwarenessValue:F0}% {(AwarenessDelta >= 0 ? "+" : "−")}{Math.Abs(AwarenessDelta):F1}";
        public string MarginDisplay => $"{MarkupPct:F0}% {(MarginDelta >= 0 ? "+" : "−")}{Math.Abs(MarginDelta):F1}";

        // ===== ПРАВЫЕ БАРЫ: полнота/актуальность =====
        [ObservableProperty] private double barAValue;              // completeness 0..100
        [ObservableProperty] private double barBValue;              // freshness 0..100
        [ObservableProperty] private string barADeltaText = "0.0";  // ±x.x
        [ObservableProperty] private string barBDeltaText = "0.0";
        [ObservableProperty] private string barATextCenter = "0";   // число в центре
        [ObservableProperty] private string barBTextCenter = "0";

        // Маржа (в процентах для текста)
        [ObservableProperty] private decimal markupPct;

        // ===== INIT =====
        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            _xr = xr;
            BrandId = id;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var b = await db.Brands
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Name,
                    x.IsOem,
                    x.Website,
                    x.YearFounded,
                    x.Description,
                    x.UserNotes,
                    CountryName = x.Country != null ? x.Country.Name : null,
                    CountryIso2 = x.Country != null ? x.Country.Iso2 : null,
                    CountryRegionIcon = x.Country != null ? x.Country.RegionIconName : null,
                    CountryRegion = x.Country != null ? x.Country.Region : null,
                    MarkupPct = (decimal?)(x.Markup != null ? x.Markup.MarkupPct : null)
                })
                .FirstOrDefaultAsync();

            if (b is null) return;

            BrandName = b.Name;
            IsOem = b.IsOem;
            Website = b.Website;
            YearFounded = b.YearFounded;
            Description = b.Description;
            UserNotes = b.UserNotes;

            PartsCount = await db.Parts.AsNoTracking().CountAsync(p => p.BrandId == id);

            // страна
            CountryName = b.CountryName ?? "—";
            CountryIso2 = string.IsNullOrWhiteSpace(b.CountryIso2) ? "—" : b.CountryIso2!.ToUpperInvariant();
            CountryWorldIconAsset = BuildRegionIconAssetName(b.CountryRegionIcon, b.CountryRegion);

            // маржа
            MarkupPct = b.MarkupPct ?? 0m;

            // алиасы -> строка
            var aliasStrings = await db.BrandAliases
                .AsNoTracking()
                .Where(a => a.BrandId == id)
                .Select(a => a.Alias)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToListAsync();

            aliasStrings = aliasStrings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            AliasesCount = aliasStrings.Count;
            AliasesJoinedDisplay = aliasStrings.Count > 0
                ? $"- {string.Join(" | ", aliasStrings)} -"
                : "—";

            // ===== расчёты: перцентили и «правые» бары =====
            var stats = await db.Brands
                .Select(x => new BrandStat(
                    x.Id,
                    db.Parts.Count(p => p.BrandId == x.Id),
                    db.BrandAliases.Count(a => a.BrandId == x.Id),
                    (double?)db.BrandMarkups.Where(m => m.BrandId == x.Id).Select(m => m.MarkupPct).FirstOrDefault(),
                    x.Website,
                    x.Description,
                    x.UserNotes,
                    x.CountryId,
                    x.YearFounded,
                    x.UpdatedAt
                ))
                .AsNoTracking()
                .ToListAsync();

            var me = stats.First(s => s.Id == id);

            // Перцентили
            var partsAll = stats.Select(s => (double)s.Parts).OrderBy(v => v).ToList();
            var aliasAll = stats.Select(s => (double)s.Aliases).OrderBy(v => v).ToList();
            var markupAll = stats.Select(s => s.Markup ?? 0.0).OrderBy(v => v).ToList();

            double markupPctReal = (double)MarkupPct;
            double markupMedian = Median(markupAll);

            AssortmentValue = PercentileRank(partsAll, me.Parts);
            AwarenessValue = PercentileRank(aliasAll, me.Aliases);
            MarginValue = PercentileRank(markupAll, markupPctReal);

            AssortmentDelta = AssortmentValue - 50.0;           // против медианного перцентиля
            AwarenessDelta = AwarenessValue - 50.0;           // против медианного перцентиля
            MarginDelta = markupPctReal - markupMedian;   // п.п. к медиане реальной маржи

            // Полнота карточки (BarA)
            double completenessMe = ComputeCompleteness(me);
            double completenessAvg = stats.Average(ComputeCompleteness);
            BarAValue = completenessMe;
            BarADeltaText = $"{(completenessMe - completenessAvg):+0.0;-0.0;0.0}";
            BarATextCenter = $"{completenessMe:F0}";

            // Актуальность (BarB)
            double freshMe = FreshnessScore(me.UpdatedAt);
            double freshAvg = stats.Average(s => FreshnessScore(s.UpdatedAt));
            BarBValue = freshMe;
            BarBDeltaText = $"{(freshMe - freshAvg):+0.0;-0.0;0.0}";
            BarBTextCenter = $"{freshMe:F0}";

            // уведомления зависимых свойств
            OnPropertyChanged(nameof(BrandNameUpper));
            OnPropertyChanged(nameof(Monogram));
            OnPropertyChanged(nameof(WebsiteHost));
            OnPropertyChanged(nameof(HasWebsite));
            OnPropertyChanged(nameof(AgeYears));

            OnPropertyChanged(nameof(ChipYearText));
            OnPropertyChanged(nameof(ChipMarkupText));
            OnPropertyChanged(nameof(ChipCountryText));
            OnPropertyChanged(nameof(ChipPartsText));
            OnPropertyChanged(nameof(ChipAgeText));

            OnPropertyChanged(nameof(AssortmentDisplay));
            OnPropertyChanged(nameof(AwarenessDisplay));
            OnPropertyChanged(nameof(MarginDisplay));

            OnPropertyChanged(nameof(DescriptionOrPlaceholder));
            OnPropertyChanged(nameof(UserNotesOrPlaceholder));
        }

        private static string BuildFlagIconAssetName(string? flagIconName, string? iso2)
            => !string.IsNullOrWhiteSpace(flagIconName)
                ? $"/Assets/{flagIconName}.svg"
                : !string.IsNullOrWhiteSpace(iso2)
                    ? $"/Assets/flags/{iso2.ToLowerInvariant()}.svg"
                    : "/Assets/flag.slash.circle.svg";

        private static string BuildRegionIconAssetName(string? regionIconName, string? region)
        {
            if (!string.IsNullOrWhiteSpace(regionIconName))
                return $"/Assets/{regionIconName}.svg";

            return (region ?? "").ToLowerInvariant() switch
            {
                "europe" or "africa" or "emea" => "/Assets/globe.europe.africa.svg",
                "americas" or "north america" or "south america" => "/Assets/globe.americas.svg",
                "asia" or "apac" or "oceania" or "australia" => "/Assets/globe.asia.australia.svg",
                _ => "/Assets/globe.svg"
            };
        }

        // ===== helpers: перцентили / медиана / полнота / актуальность =====

        private static double PercentileRank(List<double> sortedAsc, double value)
        {
            if (sortedAsc == null || sortedAsc.Count == 0) return 0.0;
            int lo = 0, hi = sortedAsc.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sortedAsc[mid] <= value) lo = mid + 1;
                else hi = mid;
            }
            return 100.0 * lo / sortedAsc.Count;
        }

        private static double Median(List<double> sortedAsc)
        {
            if (sortedAsc == null || sortedAsc.Count == 0) return 0.0;
            int n = sortedAsc.Count;
            if (n % 2 == 1) return sortedAsc[n / 2];
            return (sortedAsc[n / 2 - 1] + sortedAsc[n / 2]) / 2.0;
        }

        private static double ComputeCompleteness(BrandStat s)
        {
            // веса суммой ~100
            const double wCountry = 15, wWebsite = 15, wYear = 10, wDesc = 15, wNotes = 5, wAliases = 20, wParts = 15, wMarkup = 5;

            double score = 0;
            if (s.CountryId != null) score += wCountry;
            if (!string.IsNullOrWhiteSpace(s.Website)) score += wWebsite;
            if (s.YearFounded is int yf && yf >= 1800) score += wYear;
            if (!string.IsNullOrWhiteSpace(s.Description)) score += wDesc;
            if (!string.IsNullOrWhiteSpace(s.UserNotes)) score += wNotes;
            if (s.Aliases > 0) score += wAliases;
            if (s.Parts > 0) score += wParts;
            if (s.Markup is double mk && mk > 0) score += wMarkup;

            return score;
        }

        private static double FreshnessScore(DateTime updatedAtUtc)
        {
            var days = Math.Max(0, (DateTime.UtcNow - updatedAtUtc).TotalDays);
            const double freshMax = 30.0;   // 100 при ≤30 дней
            const double staleMax = 365.0;  // 0 при ≥365 дней
            if (days <= freshMax) return 100.0;
            if (days >= staleMax) return 0.0;
            double t = (days - freshMax) / (staleMax - freshMax); // 0..1
            return 100.0 * (1.0 - t);
        }

        private sealed record BrandStat(
            Guid Id,
            int Parts,
            int Aliases,
            double? Markup,
            string? Website,
            string? Description,
            string? UserNotes,
            Guid? CountryId,
            int? YearFounded,
            DateTime UpdatedAt
        );

        // ---- ОТКРЫТИЕ САЙТА ----
        public async Task OpenWebsiteAsync()
        {
            if (!HasWebsite) return;
            try { _ = await Launcher.LaunchUriAsync(new Uri(Website!)); }
            catch
            {
                await new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Не удалось открыть сайт",
                    Content = Website,
                    CloseButtonText = "ОК",
                    XamlRoot = _xr
                }.ShowAsync();
            }
        }
    }
}