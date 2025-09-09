using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using DromHub.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    public class BrandViewModel : INotifyPropertyChanged
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BrandViewModel> _logger;

        private string _searchText = string.Empty;
        private Brand _brand = new();
        private Brand _selectedBrand;
        private BrandAlias _selectedAlias;

        // Наценка (для правой панели)
        private double? _brandMarkupPercent;

        public XamlRoot XamlRoot { get; set; }

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
            SaveBrandCommand = new AsyncRelayCommand(SaveBrandAsync);

            AddAliasCommand = new AsyncRelayCommand(AddAlias);
            EditAliasCommand = new AsyncRelayCommand<XamlRoot>(EditAliasAsync, _ => CanEditOrDeleteAlias);
            DeleteAliasCommand = new AsyncRelayCommand<XamlRoot>(DeleteAliasAsync, _ => CanEditOrDeleteAlias);

            DeleteBrandCommand = new AsyncRelayCommand<XamlRoot>(DeleteBrandAsync, _ => SelectedBrand != null);

            // Команды работы с наценкой
            SaveBrandMarkupCommand = new AsyncRelayCommand(SaveBrandMarkupAsync, () => SelectedBrand != null);
            ClearBrandMarkupCommand = new AsyncRelayCommand(ClearBrandMarkupAsync, () => SelectedBrand != null);
        }

        #region Коллекции и свойства VM

        public ObservableCollection<Brand> Brands { get; }
        public ObservableCollection<BrandAlias> Aliases { get; }
        public ObservableCollection<AlphaKeyGroup<Brand>> GroupedBrands { get; }

        public Brand Brand
        {
            get => _brand;
            set { if (!ReferenceEquals(_brand, value)) { _brand = value; OnPropertyChanged(); } }
        }

        public Brand SelectedBrand
        {
            get => _selectedBrand;
            set
            {
                if (_selectedBrand != value)
                {
                    _selectedBrand = value;
                    Debug.WriteLine($"SelectedBrand: {_selectedBrand?.Name}");
                    OnPropertyChanged();

                    // подгружаем алиасы и наценку
                    Aliases.Clear();
                    _ = LoadAliasesCommand.ExecuteAsync(null);
                    _ = LoadBrandMarkupAsync();

                    // обновляем доступность команд
                    AddAliasCommand.NotifyCanExecuteChanged();
                    DeleteBrandCommand.NotifyCanExecuteChanged();
                    SaveBrandMarkupCommand.NotifyCanExecuteChanged();
                    ClearBrandMarkupCommand.NotifyCanExecuteChanged();
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
                    OnPropertyChanged(nameof(CanEditOrDeleteAlias));
                    EditAliasCommand.NotifyCanExecuteChanged();
                    DeleteAliasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool CanEditOrDeleteAlias => SelectedAlias != null && !SelectedAlias.IsPrimary;

        public string SearchText
        {
            get => _searchText;
            set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); } }
        }

        // Наценка (для правой панели)
        public double? BrandMarkupPercent
        {
            get => _brandMarkupPercent;
            set
            {
                if (_brandMarkupPercent != value)
                {
                    _brandMarkupPercent = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasBrandMarkup));
                }
            }
        }

        public bool HasBrandMarkup => BrandMarkupPercent.HasValue;

        #endregion

        #region Команды

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SearchBrandsCommand { get; }
        public IAsyncRelayCommand LoadAliasesCommand { get; }
        public IAsyncRelayCommand SaveBrandCommand { get; }

        public IAsyncRelayCommand AddAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> EditAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> DeleteAliasCommand { get; }

        public IAsyncRelayCommand<XamlRoot> DeleteBrandCommand { get; }

        public IAsyncRelayCommand SaveBrandMarkupCommand { get; }
        public IAsyncRelayCommand ClearBrandMarkupCommand { get; }

        #endregion

        #region Публичные утилиты

        public void ResetBrand()
        {
            Brand = new Brand();
            SelectedBrand = null;
            SelectedAlias = null;
            BrandMarkupPercent = null;
        }

        #endregion

        #region Загрузка данных (бренды, алиасы, наценка)

        private async Task LoadBrandsAsync()
        {
            try
            {
                var brands = await _context.Brands
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id),
                        MarkupPercent = _context.BrandMarkups
                                               .Where(m => m.BrandId == b.Id)
                                               .Select(m => (decimal?)m.MarkupPct)
                                               .FirstOrDefault()
                    })
                    .OrderBy(b => b.Name)
                    .AsNoTracking()
                    .ToListAsync();

                Brands.Clear();
                foreach (var brand in brands) Brands.Add(brand);

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
                var query = _context.Brands.AsQueryable();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    query = query.Where(b =>
                        EF.Functions.ILike(b.Name, $"%{text}%") ||
                        b.Aliases.Any(a => EF.Functions.ILike(a.Alias, $"%{text}%")));
                }

                var brands = await query
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id),
                        MarkupPercent = _context.BrandMarkups
                                               .Where(m => m.BrandId == b.Id)
                                               .Select(m => (decimal?)m.MarkupPct)
                                               .FirstOrDefault()
                    })
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
            var grouped = AlphaKeyGroup<Brand>.CreateGroups(Brands, b => b.Name, true);
            foreach (var group in grouped) GroupedBrands.Add(group);
        }

        private async Task LoadAliasesAsync()
        {
            if (SelectedBrand == null) return;

            try
            {
                var aliases = await _context.BrandAliases
                    .Where(a => a.BrandId == SelectedBrand.Id)
                    .OrderByDescending(a => a.IsPrimary)
                    .ThenBy(a => a.Alias)
                    .AsNoTracking()
                    .ToListAsync();

                Aliases.Clear();
                foreach (var alias in aliases) Aliases.Add(alias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading aliases");
            }
        }

        private async Task LoadBrandMarkupAsync()
        {
            if (SelectedBrand == null)
            {
                BrandMarkupPercent = null;
                return;
            }

            var m = await _context.BrandMarkups
                                  .AsNoTracking()
                                  .Where(x => x.BrandId == SelectedBrand.Id)
                                  .Select(x => (double?)x.MarkupPct)
                                  .FirstOrDefaultAsync();

            BrandMarkupPercent = m; // null если нет наценки
        }

        #endregion

        #region CRUD бренда

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
                        .AnyAsync(b => b.NormalizedName == normalized || EF.Functions.ILike(b.Name, name));

                    if (duplicate)
                    {
                        Debug.WriteLine("Бренд с таким именем уже существует.");
                        return;
                    }

                    await using var tx = await _context.Database.BeginTransactionAsync();

                    var brand = new Brand { Name = name };
                    await _context.Brands.AddAsync(brand);
                    await _context.SaveChangesAsync();

                    // Создаём основной алиас
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

        private async Task DeleteBrandAsync(XamlRoot xamlRoot)
        {
            if (SelectedBrand == null) return;

            // Проверим связность
            var partsCount = await _context.Parts
                .Where(p => p.BrandId == SelectedBrand.Id)
                .CountAsync();

            if (partsCount > 0)
            {
                await ShowDialogAsync("Удаление невозможно",
                    $"Нельзя удалить бренд «{SelectedBrand.Name}», т.к. с ним связаны {partsCount} запчаст(ь/и). " +
                    $"Сначала перенесите или удалите эти записи.", xamlRoot);
                return;
            }

            // Подтверждение
            var confirm = new ContentDialog
            {
                Title = "Удалить бренд?",
                Content = $"Бренд «{SelectedBrand.Name}» и его синонимы будут удалены. Продолжить?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };
            var result = await confirm.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            try
            {
                var brand = await _context.Brands
                    .Include(b => b.Aliases)
                    .FirstOrDefaultAsync(b => b.Id == SelectedBrand.Id);

                if (brand == null) return;

                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();

                Brands.Remove(SelectedBrand);
                Aliases.Clear();
                SelectedBrand = null;
                UpdateGroupedBrands();
            }
            catch (DbUpdateException ex)
            {
                await ShowDialogAsync("Ошибка", ex.InnerException?.Message ?? ex.Message, xamlRoot);
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Ошибка", ex.Message, xamlRoot);
            }
        }

        #endregion

        #region Наценка (сохранить / сбросить)

        private async Task SaveBrandMarkupAsync()
        {
            if (SelectedBrand == null) return;

            // Если поле очищено — это «сброс»
            if (BrandMarkupPercent is null)
            {
                await ClearBrandMarkupAsync();
                return;
            }

            var pct = (decimal)BrandMarkupPercent.Value;

            // Простейшая валидация (подстройте)
            if (pct < -100m || pct > 1000m)
            {
                await ShowInfoAsync("Некорректное значение", "Укажите наценку в пределах от -100% до 1000%.");
                return;
            }

            var existing = await _context.BrandMarkups
                                         .FirstOrDefaultAsync(x => x.BrandId == SelectedBrand.Id);

            if (existing == null)
            {
                var m = new BrandMarkup
                {
                    Id = Guid.NewGuid(),
                    BrandId = SelectedBrand.Id,
                    MarkupPct = pct
                };
                await _context.BrandMarkups.AddAsync(m);
            }
            else
            {
                existing.MarkupPct = pct;
            }

            await _context.SaveChangesAsync();

            // Отобразим процент у бренда в левом списке
            SelectedBrand.MarkupPercent = pct;
            UpdateGroupedBrands();

            await ShowInfoAsync("Готово", "Наценка сохранена.");
        }

        private async Task ClearBrandMarkupAsync()
        {
            if (SelectedBrand == null) return;

            var existing = await _context.BrandMarkups
                                         .FirstOrDefaultAsync(x => x.BrandId == SelectedBrand.Id);
            if (existing != null)
            {
                _context.BrandMarkups.Remove(existing);
                await _context.SaveChangesAsync();
            }

            BrandMarkupPercent = null;
            SelectedBrand.MarkupPercent = null;
            UpdateGroupedBrands();

            await ShowInfoAsync("Готово", "Наценка по бренду не применяется.");
        }

        #endregion

        #region Алиасы (добавить / редактировать / удалить)

        private bool CanAddAlias() => SelectedBrand != null;

        private async Task AddAlias()
        {
            if (SelectedBrand == null) return;

            try
            {
                var baseAlias = "Новый синоним";
                var aliasName = baseAlias;
                int counter = 1;

                while (await _context.BrandAliases.AnyAsync(a => a.Alias.ToLower() == aliasName.ToLower()))
                    aliasName = $"{baseAlias} {counter++}";

                var alias = new BrandAlias
                {
                    Id = Guid.NewGuid(),
                    BrandId = SelectedBrand.Id,
                    Alias = aliasName,
                    Note = ""
                };

                await _context.BrandAliases.AddAsync(alias);
                await _context.SaveChangesAsync();

                Aliases.Add(alias);
                SelectedAlias = alias;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "EF error while adding alias");
                Debug.WriteLine("Ошибка: " + ex.InnerException?.Message);
            }
        }

        public async Task SaveAliasAsync(string aliasName)
        {
            if (SelectedBrand == null) throw new InvalidOperationException("Бренд не выбран.");

            var name = (aliasName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Введите синоним.");

            var exists = await _context.BrandAliases
                .AnyAsync(a => a.BrandId == SelectedBrand.Id &&
                               EF.Functions.ILike(a.Alias, name));
            if (exists)
                throw new InvalidOperationException("Такой синоним уже существует у выбранного бренда.");

            var alias = new BrandAlias
            {
                BrandId = SelectedBrand.Id,
                Alias = name,
                IsPrimary = false,
                Note = ""
            };

            await _context.BrandAliases.AddAsync(alias);
            await _context.SaveChangesAsync();

            await LoadAliasesAsync();
            SelectedAlias = Aliases.FirstOrDefault(a => a.Id == alias.Id);
        }

        private async Task EditAliasAsync(XamlRoot xamlRoot)
        {
            if (SelectedAlias == null || xamlRoot == null) return;

            try
            {
                var dialog = new EditBrandDialog(SelectedAlias.Alias) { XamlRoot = xamlRoot };
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.BrandName))
                {
                    // новый scope, чтобы избежать конфликтов отслеживания
                    using var scope = App.ServiceProvider.CreateScope();
                    using var newContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    if (SelectedAlias.IsPrimary)
                    {
                        var brand = await newContext.Brands.FindAsync(SelectedBrand.Id);
                        if (brand != null) brand.Name = dialog.BrandName;

                        var alias = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                        if (alias != null) alias.Alias = dialog.BrandName;
                    }
                    else
                    {
                        var alias = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                        if (alias != null) alias.Alias = dialog.BrandName;
                    }

                    await newContext.SaveChangesAsync();

                    SelectedAlias.Alias = dialog.BrandName;
                    if (SelectedAlias.IsPrimary && SelectedBrand != null)
                        SelectedBrand.Name = dialog.BrandName;

                    await LoadAliasesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error editing alias: {ex.Message}");
            }
        }

        private async Task DeleteAliasAsync(XamlRoot xamlRoot)
        {
            if (SelectedAlias == null || xamlRoot == null) return;

            try
            {
                var dialog = new DeleteBrandDialog(SelectedAlias.Alias) { XamlRoot = xamlRoot };
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (SelectedAlias.IsPrimary)
                    {
                        Debug.WriteLine("Cannot delete primary alias");
                        return;
                    }

                    using var scope = App.ServiceProvider.CreateScope();
                    using var newContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var aliasToDelete = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                    if (aliasToDelete != null)
                    {
                        newContext.BrandAliases.Remove(aliasToDelete);
                        await newContext.SaveChangesAsync();

                        Aliases.Remove(SelectedAlias);
                        SelectedAlias = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting alias: {ex.Message}");
            }
        }

        #endregion

        #region Прочее (редактирование бренда в диалоге)

        public async Task EditBrand(Brand brand)
        {
            if (brand == null || XamlRoot == null) return;

            var dialog = new EditBrandDialog(brand.Name) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                brand.Name = dialog.BrandName;
                _context.Brands.Update(brand);
                await _context.SaveChangesAsync();
                await LoadBrandsAsync();
            }
        }

        public async Task DeleteBrand(Brand brand)
        {
            if (brand == null || XamlRoot == null) return;

            var dialog = new DeleteBrandDialog(brand.Name) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
                await LoadBrandsAsync();
            }
        }

        private async Task ShowInfoAsync(string title, string message)
        {
            var xr = GetXamlRoot();
            if (xr == null) return;

            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = xr
            };
            await dlg.ShowAsync();
        }

        private async Task ShowDialogAsync(string title, string message, XamlRoot xr)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = xr
            };
            await dlg.ShowAsync();
        }

        private XamlRoot GetXamlRoot()
        {
            if (Application.Current is App app && app.MainWindow?.Content is FrameworkElement root)
                return root.XamlRoot;
            return null;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}