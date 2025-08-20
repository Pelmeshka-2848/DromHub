using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        private Brand _brand;
        private Brand _selectedBrand;
        private BrandAlias _selectedAlias;

        public BrandViewModel(ApplicationDbContext context, ILogger<BrandViewModel> logger)
        {
            _context = context;
            _logger = logger;
            _brand = new Brand();

            Brands = new ObservableCollection<Brand>();
            Aliases = new ObservableCollection<BrandAlias>();
            GroupedBrands = new ObservableCollection<AlphaKeyGroup<Brand>>();

            LoadBrandsCommand = new AsyncRelayCommand(LoadBrandsAsync);
            SearchBrandsCommand = new AsyncRelayCommand(SearchBrandsAsync);
            LoadAliasesCommand = new AsyncRelayCommand(LoadAliasesAsync);
            SaveBrandCommand = new AsyncRelayCommand(SaveBrandAsync);
        }

        public Brand Brand
        {
            get => _brand;
            set { if (!ReferenceEquals(_brand, value)) { _brand = value; OnPropertyChanged(); } }
        }

        public void ResetBrand()
        {
            Brand = new Brand();
            SelectedBrand = null;
            SelectedAlias = null;
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
        public IAsyncRelayCommand SaveBrandCommand { get; }

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

        private async Task SaveBrandAsync()
        {
            try
            {
                var name = Brand?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    Debug.WriteLine("Введите название бренда.");
                    return;
                }

                var normalized = name.ToLowerInvariant();

                if (Brand.Id == Guid.Empty)
                {
                    var duplicate = await _context.Brands
                        .AnyAsync(b => b.NormalizedName == normalized
                                    || EF.Functions.ILike(b.Name, name));

                    if (duplicate)
                    {
                        Debug.WriteLine("Бренд с таким именем уже существует.");
                        return;
                    }

                    await using var tx = await _context.Database.BeginTransactionAsync();

                    var brand = new Brand { Name = name };
                    await _context.Brands.AddAsync(brand);
                    await _context.SaveChangesAsync();

                    var alias = new BrandAlias
                    {
                        BrandId = brand.Id,
                        Alias = name,
                        IsPrimary = true,
                        Note = "создано автоматически"
                    };
                    await _context.BrandAliases.AddAsync(alias);
                    await _context.SaveChangesAsync();

                    await tx.CommitAsync();

                    ResetBrand();
                    Debug.WriteLine("Бренд успешно добавлен (алиас создан автоматически).");
                }
                else
                {
                    var entity = await _context.Brands.FindAsync(Brand.Id);
                    if (entity == null)
                    {
                        Debug.WriteLine("Бренд не найден.");
                        return;
                    }

                    var duplicate = await _context.Brands
                        .AnyAsync(b => b.Id != Brand.Id &&
                                      (b.NormalizedName == normalized || EF.Functions.ILike(b.Name, name)));
                    if (duplicate)
                    {
                        Debug.WriteLine("Бренд с таким именем уже существует.");
                        return;
                    }

                    entity.Name = name;
                    await _context.SaveChangesAsync();
                    Debug.WriteLine("Бренд обновлён.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при сохранении бренда: {ex.Message}");
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}