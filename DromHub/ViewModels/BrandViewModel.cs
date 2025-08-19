using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DromHub.ViewModels
{
    public class BrandViewModel : INotifyPropertyChanged
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BrandViewModel> _logger;
        private string _searchText = string.Empty;
        private Brand _selectedBrand;
        private BrandAlias _selectedAlias;

        public BrandViewModel(ApplicationDbContext context, ILogger<BrandViewModel> logger)
        {
            _context = context;
            _logger = logger;

            Brands = new ObservableCollection<Brand>();
            Aliases = new ObservableCollection<BrandAlias>();
            GroupedBrands = new ObservableCollection<AlphaKeyGroup<Brand>>();

            LoadBrandsCommand = new AsyncRelayCommand(LoadBrandsAsync);
            SearchBrandsCommand = new AsyncRelayCommand(SearchBrandsAsync);
            LoadAliasesCommand = new AsyncRelayCommand(LoadAliasesAsync);
            AddBrandCommand = new AsyncRelayCommand(AddBrandAsync);
            EditBrandCommand = new AsyncRelayCommand(EditBrandAsync);
            DeleteBrandCommand = new AsyncRelayCommand(DeleteBrandAsync);
            AddAliasCommand = new AsyncRelayCommand(AddAliasAsync);
            EditAliasCommand = new AsyncRelayCommand(EditAliasAsync);
            DeleteAliasCommand = new AsyncRelayCommand(DeleteAliasAsync);
        }

        public ObservableCollection<Brand> Brands { get; }
        public ObservableCollection<BrandAlias> Aliases { get; }
        public ObservableCollection<AlphaKeyGroup<Brand>> GroupedBrands { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                }
            }
        }

        public Brand SelectedBrand
        {
            get => _selectedBrand;
            set
            {
                if (_selectedBrand != value)
                {
                    _selectedBrand = value;
                    OnPropertyChanged();
                    Aliases.Clear();
                    _ = LoadAliasesCommand.ExecuteAsync(null);
                }
            }
        }

        public BrandAlias SelectedAlias
        {
            get => _selectedAlias;
            set
            {
                if (_selectedAlias != value)
                {
                    _selectedAlias = value;
                    OnPropertyChanged();
                }
            }
        }

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SearchBrandsCommand { get; }
        public IAsyncRelayCommand LoadAliasesCommand { get; }
        public IAsyncRelayCommand AddBrandCommand { get; }
        public IAsyncRelayCommand EditBrandCommand { get; }
        public IAsyncRelayCommand DeleteBrandCommand { get; }
        public IAsyncRelayCommand AddAliasCommand { get; }
        public IAsyncRelayCommand EditAliasCommand { get; }
        public IAsyncRelayCommand DeleteAliasCommand { get; }

        private async Task LoadBrandsAsync()
        {
            try
            {
                var brands = await _context.Brands
                    .OrderBy(b => b.Name)
                    .AsNoTracking()
                    .ToListAsync();

                Brands.Clear();
                foreach (var brand in brands)
                {
                    Brands.Add(brand);
                }

                UpdateGroupedBrands();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading brands");
            }
        }

        private async Task SearchBrandsAsync()
        {
            try
            {
                var text = (SearchText ?? string.Empty).Trim();
                IQueryable<Brand> query = _context.Brands;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    query = query.Where(b =>
                        EF.Functions.ILike(b.Name, $"%{text}%") ||
                        b.Aliases.Any(a => EF.Functions.ILike(a.Alias, $"%{text}%")));
                }

                var brands = await query
                    .OrderBy(b => b.Name)
                    .AsNoTracking()
                    .ToListAsync();

                Brands.Clear();
                foreach (var b in brands) Brands.Add(b);

                UpdateGroupedBrands();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching brands");
            }
        }

        private void UpdateGroupedBrands()
        {
            GroupedBrands.Clear();
            var grouped = AlphaKeyGroup<Brand>.CreateGroups(
                Brands,
                b => b.Name,
                true);

            foreach (var group in grouped)
            {
                GroupedBrands.Add(group);
            }
        }

        private async Task LoadAliasesAsync()
        {
            if (SelectedBrand == null) return;

            try
            {
                var aliases = await _context.BrandAliases
                    .Where(a => a.BrandId == SelectedBrand.Id)
                    .OrderBy(a => a.Alias)
                    .AsNoTracking()
                    .ToListAsync();

                Aliases.Clear();
                foreach (var alias in aliases)
                {
                    Aliases.Add(alias);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading aliases");
            }
        }

        private async Task AddBrandAsync()
        {
            // Реализация диалога добавления бренда
        }

        private async Task EditBrandAsync()
        {
            if (SelectedBrand == null) return;
            // Реализация диалога редактирования бренда
        }

        private async Task DeleteBrandAsync()
        {
            if (SelectedBrand == null) return;
            // Реализация подтверждения и удаления бренда
        }

        private async Task AddAliasAsync()
        {
            if (SelectedBrand == null) return;
            // Реализация диалога добавления синонима
        }

        private async Task EditAliasAsync()
        {
            if (SelectedAlias == null) return;
            // Реализация диалога редактирования синонима
        }

        private async Task DeleteAliasAsync()
        {
            if (SelectedAlias == null) return;
            // Реализация подтверждения и удаления синонима
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}