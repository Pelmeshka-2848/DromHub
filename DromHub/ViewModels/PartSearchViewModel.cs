using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DromHub.ViewModels
{
    public partial class PartSearchViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PartSearchViewModel> _logger;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Part? _selectedPart;

        [ObservableProperty]
        private Brand? _selectedBrandFilter;

        [ObservableProperty]
        private bool _isBusy;

        public ObservableCollection<Part> Parts { get; } = new();
        public ObservableCollection<Brand> Brands { get; } = new();

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SearchPartsCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }

        public PartSearchViewModel(ApplicationDbContext context, ILogger<PartSearchViewModel> logger)
        {
            _context = context;
            _logger = logger;

            SearchPartsCommand = new AsyncRelayCommand(SearchPartsAsync);
            LoadBrandsCommand = new AsyncRelayCommand(LoadBrandsAsync);
            ClearSearchCommand = new RelayCommand(ClearSearch);
            LoadBrandsCommand.ExecuteAsync(null);
        }

        private async Task LoadBrandsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                var brands = await _context.Brands
                    .OrderBy(b => b.Name)
                    .AsNoTracking()
                    .ToListAsync();

                Brands.Clear();
                Brands.Add(new Brand { Id = Guid.Empty, Name = "Все бренды" });

                foreach (var brand in brands)
                {
                    Brands.Add(brand);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке брендов");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SearchPartsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    // Если строка поиска пустая - очищаем результаты
                    Parts.Clear();
                    return;
                }

                var searchText = SearchText.Trim().ToLower();

                var query = _context.Parts
                    .Include(p => p.Brand)
                    .AsQueryable();

                // Фильтрация по бренду если выбран
                if (SelectedBrandFilter != null && SelectedBrandFilter.Id != Guid.Empty)
                {
                    query = query.Where(p => p.BrandId == SelectedBrandFilter.Id);
                }

                // Применяем поиск по тексту
                query = query.Where(p =>
                    (p.CatalogNumber != null && p.CatalogNumber.ToLower().Contains(searchText)) ||
                    (p.Name != null && p.Name.ToLower().Contains(searchText)) ||
                    (p.Article != null && p.Article.ToLower().Contains(searchText)));

                var results = await query
                    .OrderBy(p => p.CatalogNumber)
                    .AsNoTracking()
                    .ToListAsync();

                Parts.Clear();

                foreach (var part in results)
                {
                    Parts.Add(part);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка поиска: {ex.Message}");
                Parts.Clear();
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            SelectedBrandFilter = Brands.FirstOrDefault(b => b.Id == Guid.Empty);
            Parts.Clear();
        }
    }
}