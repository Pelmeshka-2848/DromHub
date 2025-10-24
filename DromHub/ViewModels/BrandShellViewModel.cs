using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс BrandShellViewModel отвечает за логику компонента BrandShellViewModel.
    /// </summary>

    public class BrandShellViewModel : ObservableObject
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        /// <summary>
        /// Конструктор BrandShellViewModel инициализирует экземпляр класса.
        /// </summary>
        public BrandShellViewModel(IDbContextFactory<ApplicationDbContext> dbFactory) => _dbFactory = dbFactory;
        /// <summary>
        /// Свойство XamlRoot предоставляет доступ к данным XamlRoot.
        /// </summary>

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
        /// <summary>
        /// Свойство HasPrev предоставляет доступ к данным HasPrev.
        /// </summary>

        public bool HasPrev => PrevBrandId.HasValue;
        /// <summary>
        /// Свойство HasNext предоставляет доступ к данным HasNext.
        /// </summary>
        public bool HasNext => NextBrandId.HasValue;
        /// <summary>
        /// Метод InitializeAsync выполняет основную операцию класса.
        /// </summary>

        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            XamlRoot = xr;
            BrandId = id;

            await using var db = await _dbFactory.CreateDbContextAsync();

            var b = await db.Brands.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            BrandNameUpper = (b?.Name ?? string.Empty).ToUpperInvariant();

            // вычисляем соседей в алфавитном порядке
            var sorted = await db.Brands
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