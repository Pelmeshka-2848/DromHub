using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс BrandsIndexViewModel отвечает за логику компонента BrandsIndexViewModel.
    /// </summary>
    public partial class BrandsIndexViewModel : ObservableObject
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        /// <summary>
        /// Свойство Brands предоставляет доступ к данным Brands.
        /// </summary>

        public ObservableCollection<Brand> Brands { get; } = new();
        /// <summary>
        /// Свойство GroupedBrands предоставляет доступ к данным GroupedBrands.
        /// </summary>
        public ObservableCollection<AlphaKeyGroup<Brand>> GroupedBrands { get; } = new();

        // Поиск/фильтры
        [ObservableProperty] private string searchText;
        [ObservableProperty] private bool filterIsOem;
        [ObservableProperty] private bool filterWithMarkup;
        [ObservableProperty] private bool filterMarkupZero;
        [ObservableProperty] private bool filterNoAliases;

        // Для кнопок/состояния
        /// <summary>
        /// Конструктор BrandsIndexViewModel инициализирует экземпляр класса.
        /// </summary>
        [ObservableProperty] private Brand selectedBrand;

        public BrandsIndexViewModel(IDbContextFactory<ApplicationDbContext> dbFactory) => _dbFactory = dbFactory;
        /// <summary>
        /// Метод LoadAsync выполняет основную операцию класса.
        /// </summary>

        public async Task LoadAsync()
        {
            await ReloadList();
            ApplyFilters();
        }
        /// <summary>
        /// Метод ReloadList выполняет основную операцию класса.
        /// </summary>

        public async Task ReloadList()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var query = db.Brands.AsQueryable();

            var list = await query
                .Select(b => new Brand
                {
                    Id = b.Id,
                    Name = b.Name,
                    IsOem = b.IsOem,
                    PartsCount = db.Parts.Count(p => p.BrandId == b.Id),
                    MarkupPercent = db.BrandMarkups
                        .Where(m => m.BrandId == b.Id)
                        .Select(m => (decimal?)m.MarkupPct)
                        .FirstOrDefault() ?? 0m,
                    AliasesCount = db.BrandAliases.Count(a => a.BrandId == b.Id),
                    NonPrimaryAliasesCount = db.BrandAliases.Count(a => a.BrandId == b.Id && !a.IsPrimary)
                })
                .OrderBy(b => b.Name)
                .AsNoTracking()
                .ToListAsync();

            Brands.Clear();
            foreach (var b in list) Brands.Add(b);
        }
        /// <summary>
        /// Метод ApplyFilters выполняет основную операцию класса.
        /// </summary>

        public void ApplyFilters()
        {
            var filtered = Brands.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim();
                filtered = filtered.Where(b => b.Name.Contains(q, System.StringComparison.OrdinalIgnoreCase));
            }

            if (FilterIsOem) filtered = filtered.Where(b => b.IsOem);
            if (FilterWithMarkup) filtered = filtered.Where(b => b.MarkupPercent > 0m);
            if (FilterMarkupZero) filtered = filtered.Where(b => b.MarkupPercent == 0m);
            if (FilterNoAliases) filtered = filtered.Where(b => b.NonPrimaryAliasesCount == 0);

            UpdateGrouped(filtered);
        }
        /// <summary>
        /// Метод UpdateGrouped выполняет основную операцию класса.
        /// </summary>

        private void UpdateGrouped(IEnumerable<Brand> items)
        {
            GroupedBrands.Clear();
            var groups = AlphaKeyGroup<Brand>.CreateGroups(items, b => b.Name, true);
            foreach (var g in groups) GroupedBrands.Add(g);
        }
    }
}