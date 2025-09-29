using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{

    public class BrandShellViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;
        public BrandShellViewModel(ApplicationDbContext db) => _db = db;

        public XamlRoot XamlRoot { get; private set; }

        // Текущий бренд
        private Guid _brandId;
        public Guid BrandId
        {
            get => _brandId;
            private set => SetProperty(ref _brandId, value);
        }

        private string _brandNameUpper;
        public string BrandNameUpper
        {
            get => _brandNameUpper;
            private set => SetProperty(ref _brandNameUpper, value);
        }

        // Активный раздел
        private BrandDetailsSection _section = BrandDetailsSection.Overview;
        public BrandDetailsSection Section
        {
            get => _section;
            set => SetProperty(ref _section, value);
        }

        // --- Соседние бренды (с уведомлениями) ---
        private Guid? _prevBrandId;
        public Guid? PrevBrandId
        {
            get => _prevBrandId;
            private set
            {
                if (SetProperty(ref _prevBrandId, value))
                    OnPropertyChanged(nameof(HasPrev));
            }
        }

        private string _prevBrandNameUpper;
        public string PrevBrandNameUpper
        {
            get => _prevBrandNameUpper;
            private set => SetProperty(ref _prevBrandNameUpper, value);
        }

        private Guid? _nextBrandId;
        public Guid? NextBrandId
        {
            get => _nextBrandId;
            private set
            {
                if (SetProperty(ref _nextBrandId, value))
                    OnPropertyChanged(nameof(HasNext));
            }
        }

        private string _nextBrandNameUpper;
        public string NextBrandNameUpper
        {
            get => _nextBrandNameUpper;
            private set => SetProperty(ref _nextBrandNameUpper, value);
        }

        public bool HasPrev => PrevBrandId.HasValue;
        public bool HasNext => NextBrandId.HasValue;

        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            XamlRoot = xr;
            BrandId = id;

            var b = await _db.Brands.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            BrandNameUpper = (b?.Name ?? string.Empty).ToUpperInvariant();

            // вычисляем соседей в алфавитном порядке
            var sorted = await _db.Brands
                                  .OrderBy(x => x.Name)
                                  .Select(x => new { x.Id, x.Name })
                                  .AsNoTracking()
                                  .ToListAsync();

            var i = sorted.FindIndex(x => x.Id == id);

            if (i > 0)
            {
                PrevBrandId = sorted[i - 1].Id;
                PrevBrandNameUpper = (sorted[i - 1].Name ?? string.Empty).ToUpperInvariant();
            }
            else
            {
                PrevBrandId = null;
                PrevBrandNameUpper = null;
            }

            if (i >= 0 && i < sorted.Count - 1)
            {
                NextBrandId = sorted[i + 1].Id;
                NextBrandNameUpper = (sorted[i + 1].Name ?? string.Empty).ToUpperInvariant();
            }
            else
            {
                NextBrandId = null;
                NextBrandNameUpper = null;
            }
        }
    }
}