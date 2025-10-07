using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.System;

namespace DromHub.ViewModels
{
    public partial class BrandOverviewViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;
        public BrandOverviewViewModel(ApplicationDbContext db) => _db = db;

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

        // ===== МЕТРИКИ (значение + дельта) =====
        [ObservableProperty] private double assortmentValue;    // 0..100
        [ObservableProperty] private double assortmentDelta;

        [ObservableProperty] private double awarenessValue;     // 0..100
        [ObservableProperty] private double awarenessDelta;

        [ObservableProperty] private double marginValue;        // 0..100
        [ObservableProperty] private double marginDelta;

        public string AssortmentDisplay => $"{AssortmentValue:F0}% {(AssortmentDelta >= 0 ? "+" : "−")}{Math.Abs(AssortmentDelta):F1}";
        public string AwarenessDisplay => $"{AwarenessValue:F0}% {(AwarenessDelta >= 0 ? "+" : "−")}{Math.Abs(AwarenessDelta):F1}";
        public string MarginDisplay => $"{MarkupPct:F0}% {(MarginDelta >= 0 ? "+" : "−")}{Math.Abs(MarginDelta):F1}";

        // Для правой колонки (ProgressBar)
        public double BarAValue => AssortmentValue;
        public double BarBValue => AwarenessValue;
        public string BarADeltaText => $"{AssortmentDelta:+0.0;-0.0;0.0}";
        public string BarBDeltaText => $"{AwarenessDelta:+0.0;-0.0;0.0}";
        public string BarATextCenter => $"{AssortmentValue:F0}";
        public string BarBTextCenter => $"{AwarenessValue:F0}";

        // Маржа (в процентах)
        [ObservableProperty] private decimal markupPct;

        // ===== INIT =====
        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            _xr = xr;
            BrandId = id;

            var b = await _db.Brands
                .Include(x => x.Country)
                .Include(x => x.Aliases)
                .Include(x => x.Parts)
                .Include(x => x.Markup)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (b is null) return;

            BrandName = b.Name;
            IsOem = b.IsOem;
            Website = b.Website;
            YearFounded = b.YearFounded;
            Description = b.Description;
            UserNotes = b.UserNotes;

            PartsCount = b.Parts?.Count ?? 0;
            AliasesCount = b.Aliases?.Count ?? 0;

            // страна
            CountryName = b.Country?.Name ?? "—";
            CountryIso2 = string.IsNullOrWhiteSpace(b.Country?.Iso2) ? "—" : b.Country!.Iso2.ToUpperInvariant();
            CountryWorldIconAsset = BuildRegionIconAssetName(b.Country?.RegionIconName, b.Country?.Region);

            // маржа
            MarkupPct = (decimal)(b.Markup?.MarkupPct ?? 0);

            // синонимы -> строка
            var aliasStrings = (b.Aliases ?? new List<BrandAlias>())
                .Select(ReadAliasString)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            AliasesCount = aliasStrings.Count;
            AliasesJoinedDisplay = aliasStrings.Count > 0
                ? $"- {string.Join(" | ", aliasStrings)} -"
                : "—";

            // ===== расчёт метрик и дельт =====
            var stats = await _db.Brands
                .Select(x => new
                {
                    Parts = _db.Parts.Count(p => p.BrandId == x.Id),
                    Aliases = _db.BrandAliases.Count(a => a.BrandId == x.Id),
                    Markup = (double?)_db.BrandMarkups.Where(m => m.BrandId == x.Id)
                                                       .Select(m => m.MarkupPct)
                                                       .FirstOrDefault()
                })
                .ToListAsync();

            var count = Math.Max(1, stats.Count);
            double maxParts = stats.Count > 0 ? Math.Max(1, stats.Max(s => s.Parts)) : 1;
            double maxAliases = stats.Count > 0 ? Math.Max(1, stats.Max(s => s.Aliases)) : 1;

            double meanPartsRatio = stats.Count > 0 ? stats.Average(s => s.Parts / maxParts) * 100.0 : 0.0;
            double meanAliasesRatio = stats.Count > 0 ? stats.Average(s => s.Aliases / maxAliases) * 100.0 : 0.0;
            double meanMarkup = stats.Count > 0 ? stats.Average(s => s.Markup ?? 0.0) : 0.0;

            AssortmentValue = PartsCount / maxParts * 100.0;
            AwarenessValue = AliasesCount / maxAliases * 100.0;
            MarginValue = Math.Clamp((double)MarkupPct, 0, 100);

            AssortmentDelta = AssortmentValue - meanPartsRatio;
            AwarenessDelta = AwarenessValue - meanAliasesRatio;
            MarginDelta = (double)MarkupPct - meanMarkup;

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

            OnPropertyChanged(nameof(BarAValue));
            OnPropertyChanged(nameof(BarBValue));
            OnPropertyChanged(nameof(BarADeltaText));
            OnPropertyChanged(nameof(BarBDeltaText));
            OnPropertyChanged(nameof(BarATextCenter));
            OnPropertyChanged(nameof(BarBTextCenter));

            OnPropertyChanged(nameof(DescriptionOrPlaceholder));
            OnPropertyChanged(nameof(UserNotesOrPlaceholder));
        }

        private static string ReadAliasString(object alias)
        {
            if (alias is null) return null;
            var t = alias.GetType();
            foreach (var prop in new[] { "Name", "Alias", "Value", "Text" })
            {
                var pi = t.GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (pi != null && pi.PropertyType == typeof(string))
                    return (pi.GetValue(alias) as string)?.Trim();
            }
            return alias.ToString();
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