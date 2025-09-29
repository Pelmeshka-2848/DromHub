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
    public class BrandOverviewViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;
        public BrandOverviewViewModel(ApplicationDbContext db) => _db = db;

        private XamlRoot _xr;
        public Guid BrandId { get; private set; }

        private string _brandName;
        public string BrandName { get => _brandName; private set { if (SetProperty(ref _brandName, value)) BrandNameUpper = (value ?? "").ToUpperInvariant(); } }
        public string BrandNameUpper { get; private set; }
        public string Monogram { get; private set; } = "?";

        public bool IsOem { get; private set; }
        public string OemAnalogText => IsOem ? "OEM" : "Аналог";
        public string Country { get; private set; } = "Страна не указана";
        public string Website { get; private set; }
        public bool HasWebsite => Uri.TryCreate(Website, UriKind.Absolute, out _);
        public string WebsiteHost => HasWebsite ? new Uri(Website).Host : "—";
        public string Signature => $"{(IsOem ? "OEM" : "Аналог")} • {Country} • {WebsiteHost}";

        public int PartsCount { get; private set; }
        public int AliasesCount { get; private set; }
        public decimal MarkupPct { get; private set; }

        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            _xr = xr;
            BrandId = id;

            var b = await _db.Brands.Include(x => x.Parts).Include(x => x.Aliases)
                                    .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return;

            BrandName = b.Name;
            Monogram = string.IsNullOrWhiteSpace(b.Name) ? "?" : b.Name.Trim()[0].ToString().ToUpperInvariant();
            IsOem = b.IsOem;
            Country = b.Country ?? "Страна не указана";
            Website = b.Website;

            PartsCount = b.Parts?.Count ?? 0;
            AliasesCount = b.Aliases?.Count ?? 0;
            MarkupPct = await _db.BrandMarkups.Where(m => m.BrandId == id)
                                              .Select(m => (decimal?)m.MarkupPct)
                                              .FirstOrDefaultAsync() ?? 0m;

            OnPropertyChanged(string.Empty);
        }

        public async Task OpenWebsiteAsync()
        {
            if (!HasWebsite) return;
            try { _ = await Launcher.LaunchUriAsync(new Uri(Website)); }
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