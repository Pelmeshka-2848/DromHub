using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Linq;
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

        // ---- БАЗА ----
        private string _brandName;
        public string BrandName
        {
            get => _brandName;
            private set
            {
                if (SetProperty(ref _brandName, value))
                {
                    OnPropertyChanged(nameof(BrandNameUpper));
                    OnPropertyChanged(nameof(Monogram));
                    OnPropertyChanged(nameof(Signature));
                }
            }
        }
        public string BrandNameUpper => (BrandName ?? "").ToUpperInvariant();
        public string Monogram => string.IsNullOrWhiteSpace(BrandName) ? "?" : BrandName.Trim()[0].ToString().ToUpperInvariant();

        private bool _isOem;
        public bool IsOem
        {
            get => _isOem;
            private set { if (SetProperty(ref _isOem, value)) OnPropertyChanged(nameof(OemAnalogText)); }
        }
        public string OemAnalogText => IsOem ? "OEM" : "Аналог";

        private string _country = "Страна не указана";
        public string Country
        {
            get => _country;
            private set
            {
                if (SetProperty(ref _country, value))
                {
                    OnPropertyChanged(nameof(Signature));
                    RecalcCountryProgress();
                }
            }
        }

        private string _website;
        public string Website
        {
            get => _website;
            private set
            {
                if (SetProperty(ref _website, value))
                {
                    OnPropertyChanged(nameof(HasWebsite));
                    OnPropertyChanged(nameof(WebsiteHost));
                    OnPropertyChanged(nameof(Signature));
                }
            }
        }
        public bool HasWebsite => Uri.TryCreate(Website, UriKind.Absolute, out _);
        public string WebsiteHost => HasWebsite ? new Uri(Website).Host : "—";
        public string Signature => $"{(IsOem ? "OEM" : "Аналог")} • {Country} • {WebsiteHost}";

        private int _partsCount;
        public int PartsCount { get => _partsCount; private set => SetProperty(ref _partsCount, value); }

        private int _aliasesCount;
        public int AliasesCount { get => _aliasesCount; private set => SetProperty(ref _aliasesCount, value); }

        private decimal _markupPct;
        public decimal MarkupPct
        {
            get => _markupPct;
            private set
            {
                if (SetProperty(ref _markupPct, value))
                    MarkupPctCapped = (double)Math.Clamp(value, 0m, 100m);
            }
        }
        public string MarkupPctDisplay => $"Текущее значение: {MarkupPct:F2}%";

        // ---- UI-флажок ----
        private bool _isFavorite;
        public bool IsFavorite { get => _isFavorite; set => SetProperty(ref _isFavorite, value); }

        // ---- МЕТРИКИ ДЛЯ ПОЛОС ----
        private double _coveragePct; public double CoveragePct { get => _coveragePct; private set => SetProperty(ref _coveragePct, value); }
        private double _synonymsPct; public double SynonymsPct { get => _synonymsPct; private set => SetProperty(ref _synonymsPct, value); }
        private double _partsPct; public double PartsPct { get => _partsPct; private set => SetProperty(ref _partsPct, value); }
        private double _markupPctCapped; public double MarkupPctCapped { get => _markupPctCapped; private set => SetProperty(ref _markupPctCapped, value); }
        private double _countryProgress; public double CountryProgress { get => _countryProgress; private set => SetProperty(ref _countryProgress, value); }

        private void RecalcCountryProgress() =>
            CountryProgress = (!string.IsNullOrWhiteSpace(Country) && Country != "Страна не указана") ? 100 : 0;

        // ---- ИНИЦИАЛИЗАЦИЯ ----
        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            _xr = xr;
            BrandId = id;

            var b = await _db.Brands
                             .Include(x => x.Parts)
                             .Include(x => x.Aliases)
                             .AsNoTracking()
                             .FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return;

            BrandName = b.Name;
            IsOem = b.IsOem;
            Country = string.IsNullOrWhiteSpace(b.Country) ? "Страна не указана" : b.Country;
            Website = string.IsNullOrWhiteSpace(b.Website) ? "" : b.Website;

            PartsCount = b.Parts?.Count ?? 0;
            AliasesCount = b.Aliases?.Count ?? 0;

            var m = await _db.BrandMarkups
                             .Where(x => x.BrandId == id)
                             .Select(x => (decimal?)x.MarkupPct)
                             .FirstOrDefaultAsync();
            MarkupPct = m ?? 0m;

            // Перцентили по всем брендам
            var stats = await _db.Brands
                .Select(x => new
                {
                    Parts = _db.Parts.Count(p => p.BrandId == x.Id),
                    Aliases = _db.BrandAliases.Count(a => a.BrandId == x.Id)
                })
                .ToListAsync();

            var total = Math.Max(1, stats.Count);
            var partsRank = stats.Count(s => s.Parts <= PartsCount);
            var aliasRank = stats.Count(s => s.Aliases <= AliasesCount);

            CoveragePct = 100.0 * partsRank / total;
            PartsPct = CoveragePct;
            SynonymsPct = 100.0 * aliasRank / total;

            RecalcCountryProgress();
        }

        // ---- ОТКРЫТИЕ САЙТА ----
        public async Task OpenWebsiteAsync()
        {
            if (!HasWebsite) return;
            try
            {
                _ = await Launcher.LaunchUriAsync(new Uri(Website));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
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