using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DromHub.ViewModels
{
    public class PartViewModel : INotifyPropertyChanged
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PartViewModel> _logger;
        private Part _part;
        private Brand _selectedBrand;
        private string _searchText = string.Empty;
        private Brand _selectedBrandFilter;
        private bool _isBusy;

        public ApplicationDbContext Context => _context;

        public PartViewModel(ApplicationDbContext context, ILogger<PartViewModel> logger)
        {
            _context = context;
            _logger = logger;
            _part = new Part();

            Brands = new ObservableCollection<Brand>();
            Parts = new ObservableCollection<Part>();

            LoadBrandsCommand = new AsyncRelayCommand(LoadBrandsAsync);
            SavePartCommand = new AsyncRelayCommand(SavePartAsync);
            SearchPartsCommand = new AsyncRelayCommand(SearchPartsAsync);
            ClearSearchCommand = new RelayCommand(ClearSearch);
        }

        public PartViewModel(ApplicationDbContext context, ILogger<PartViewModel> logger, Part part) : this(context, logger)
        {
            _part = part ?? new Part();
        }

        // Properties for Part management
        public Guid Id
        {
            get => _part.Id;
            set
            {
                if (_part.Id != value)
                {
                    _part.Id = value;
                    OnPropertyChanged();
                }
            }
        }

        public Guid BrandId
        {
            get => _part.BrandId;
            set
            {
                if (_part.BrandId != value)
                {
                    _part.BrandId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CatalogNumber
        {
            get => _part.CatalogNumber;
            set
            {
                if (_part.CatalogNumber != value)
                {
                    _part.CatalogNumber = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Article));
                }
            }
        }

        public string Article => _part.Article;

        public string Name
        {
            get => _part.Name;
            set
            {
                if (_part.Name != value)
                {
                    _part.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedAt => _part.CreatedAt;
        public DateTime UpdatedAt => _part.UpdatedAt;

        public Brand SelectedBrand
        {
            get => _selectedBrand ?? _part.Brand;
            set
            {
                if (_selectedBrand != value)
                {
                    _selectedBrand = value;
                    BrandId = value?.Id ?? Guid.Empty;
                    OnPropertyChanged();
                }
            }
        }

        // Properties for Search functionality
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

        public Part? SelectedPart { get; set; }

        public Brand? SelectedBrandFilter
        {
            get => _selectedBrandFilter;
            set
            {
                if (_selectedBrandFilter != value)
                {
                    _selectedBrandFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEmpty => Parts.Count == 0;

        // Collections
        public ObservableCollection<Brand> Brands { get; }
        public ObservableCollection<Part> Parts { get; }

        // Commands
        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SavePartCommand { get; }
        public IAsyncRelayCommand SearchPartsCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }

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

        public void ResetPart()
        {
            _part = new Part();
            _selectedBrand = null;
            OnPropertyChanged(nameof(SelectedBrand));
            OnPropertyChanged(nameof(CatalogNumber));
            OnPropertyChanged(nameof(Article));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(CreatedAt));
            OnPropertyChanged(nameof(UpdatedAt));
        }

        public async Task SavePartAsync()
        {
            try
            {
                _part.CatalogNumber = _part.CatalogNumber?.Trim();

                if (_part.BrandId == Guid.Empty || string.IsNullOrWhiteSpace(_part.CatalogNumber))
                {
                    Debug.WriteLine("Заполните Бренд и Каталог.");
                    return;
                }

                if (_part.Id == Guid.Empty)
                {
                    // Проверка на существование дубликата
                    var exists = await _context.Parts
                        .AnyAsync(p => p.BrandId == _part.BrandId &&
                                      p.CatalogNumber == _part.CatalogNumber);

                    if (exists)
                    {
                        Debug.WriteLine("Запчасть с таким номером и брендом уже существует.");
                        // Не сбрасываем состояние, чтобы пользователь мог исправить данные
                        return;
                    }

                    // Добавление новой детали
                    var entity = new Part
                    {
                        BrandId = _part.BrandId,
                        CatalogNumber = _part.CatalogNumber,
                        Name = _part.Name
                    };

                    await _context.Parts.AddAsync(entity);
                    await _context.SaveChangesAsync();

                    // Сбрасываем состояние только после успешного сохранения
                    ResetPart();

                    Debug.WriteLine("Запчасть успешно добавлена.");
                }
                else
                {
                    // Редактирование существующей детали
                    var entity = await _context.Parts.FindAsync(_part.Id);
                    if (entity != null)
                    {
                        // Проверка на конфликт с другими записями
                        var conflict = await _context.Parts
                            .AnyAsync(p => p.Id != _part.Id &&
                                          p.BrandId == _part.BrandId &&
                                          p.CatalogNumber == _part.CatalogNumber);

                        if (conflict)
                        {
                            Debug.WriteLine("Запчасть с таким номером и брендом уже существует.");
                            return;
                        }

                        entity.BrandId = _part.BrandId;
                        entity.CatalogNumber = _part.CatalogNumber;
                        entity.Name = _part.Name;

                        await _context.SaveChangesAsync();
                        Debug.WriteLine("Запчасть успешно обновлена.");
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Debug.WriteLine($"Ошибка сохранения: {ex.InnerException?.Message ?? ex.Message}");
                // В случае ошибки не сбрасываем состояние, чтобы пользователь мог повторить попытку
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Неожиданная ошибка: {ex.Message}");
            }
        }

        private async Task SearchPartsAsync()
        {
            try
            {
                Parts.Clear();

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    OnPropertyChanged(nameof(IsEmpty));
                    return;
                }

                var searchText = SearchText.Trim().ToLower();

                var query = _context.Parts
                    .Include(p => p.Brand)
                    .AsQueryable();

                if (SelectedBrandFilter != null && SelectedBrandFilter.Id != Guid.Empty)
                    query = query.Where(p => p.BrandId == SelectedBrandFilter.Id);

                query = query.Where(p =>
                    (p.CatalogNumber != null && p.CatalogNumber.ToLower().Contains(searchText)) ||
                    (p.Name != null && p.Name.ToLower().Contains(searchText)));

                var results = await query.OrderBy(p => p.CatalogNumber).AsNoTracking().ToListAsync();

                foreach (var part in results)
                    Parts.Add(part);

                OnPropertyChanged(nameof(IsEmpty));
            }
            catch
            {
                Parts.Clear();
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            SelectedBrandFilter = Brands.FirstOrDefault(b => b.Id == Guid.Empty);
            Parts.Clear();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}