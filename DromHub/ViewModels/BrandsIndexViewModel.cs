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
    public partial class BrandsIndexViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;

        public ObservableCollection<Brand> Brands { get; } = new();
        public ObservableCollection<AlphaKeyGroup<Brand>> GroupedBrands { get; } = new();

        // Поиск/фильтры
        [ObservableProperty] private string searchText;
        [ObservableProperty] private bool filterIsOem;
        [ObservableProperty] private bool filterWithMarkup;
        [ObservableProperty] private bool filterMarkupZero;
        [ObservableProperty] private bool filterNoAliases;

        // Для кнопок/состояния
        [ObservableProperty] private Brand selectedBrand;

        public BrandsIndexViewModel(ApplicationDbContext db) => _db = db;

        public async Task LoadAsync()
        {
            await ReloadList();
            ApplyFilters();
        }

        public async Task ReloadList()
        {
            var query = _db.Brands.AsQueryable();

            var list = await query
                .Select(b => new Brand
                {
                    Id = b.Id,
                    Name = b.Name,
                    IsOem = b.IsOem,
                    PartsCount = _db.Parts.Count(p => p.BrandId == b.Id),
                    MarkupPercent = _db.BrandMarkups
                        .Where(m => m.BrandId == b.Id)
                        .Select(m => (decimal?)m.MarkupPct)
                        .FirstOrDefault() ?? 0m,
                    AliasesCount = _db.BrandAliases.Count(a => a.BrandId == b.Id),
                    NonPrimaryAliasesCount = _db.BrandAliases.Count(a => a.BrandId == b.Id && !a.IsPrimary)
                })
                .OrderBy(b => b.Name)
                .AsNoTracking()
                .ToListAsync();

            Brands.Clear();
            foreach (var b in list) Brands.Add(b);
        }

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

        private void UpdateGrouped(IEnumerable<Brand> items)
        {
            GroupedBrands.Clear();
            var groups = AlphaKeyGroup<Brand>.CreateGroups(items, b => b.Name, true);
            foreach (var g in groups) GroupedBrands.Add(g);
        }
    }
}