using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using DromHub.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

        public XamlRoot XamlRoot { get; set; }

        public BrandViewModel(ApplicationDbContext context, ILogger<BrandViewModel> logger)
        {
            _context = context;
            _logger = logger;
            _brand = new Brand();
            XamlRoot = null;

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
        }
        private XamlRoot GetXamlRoot()
        {
            // Попробуем получить XamlRoot через Application.Current
            if (Application.Current is App app && app.MainWindow?.Content is FrameworkElement rootElement)
            {
                return rootElement.XamlRoot;
            }
            return null;
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
                    Debug.WriteLine($"SelectedBrand: {_selectedBrand?.Name}");
                    OnPropertyChanged();
                    Aliases.Clear();
                    _ = LoadAliasesCommand.ExecuteAsync(null);
                    AddAliasCommand.NotifyCanExecuteChanged();

                    DeleteBrandCommand.NotifyCanExecuteChanged(); // обновляем доступность кнопки
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
                    OnPropertyChanged(nameof(CanEditOrDeleteAlias)); // <- уведомляем UI

                    // обновим доступность команд
                    EditAliasCommand.NotifyCanExecuteChanged();
                    DeleteAliasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // Разрешать редактирование/удаление, только если выбран НЕ основной алиас
        public bool CanEditOrDeleteAlias => SelectedAlias != null && !SelectedAlias.IsPrimary;

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SearchBrandsCommand { get; }
        public IAsyncRelayCommand LoadAliasesCommand { get; }
        public IAsyncRelayCommand SaveBrandCommand { get; }
        public IAsyncRelayCommand AddAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> EditAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> DeleteAliasCommand { get; }
        // команда
        public IAsyncRelayCommand<XamlRoot> DeleteBrandCommand { get; }

        private bool CanDeleteBrand()
        {
            return SelectedBrand != null;
        }

        private async Task DeleteBrandAsync(XamlRoot xamlRoot)
        {
            if (SelectedBrand == null) return;

            // 0) Есть ли связанные запчасти?
            var partsCount = await _context.Parts
                .Where(p => p.BrandId == SelectedBrand.Id)
                .CountAsync();

            if (partsCount > 0)
            {
                // Поясняем пользователю, почему нельзя
                var blocked = new ContentDialog
                {
                    Title = "Удаление невозможно",
                    Content = $"Нельзя удалить бренд «{SelectedBrand.Name}», " +
                              $"так как с ним связаны {partsCount} запчаст(ь/и). " +
                              $"Сначала перенесите или удалите эти записи.",
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await blocked.ShowAsync();
                return;
            }

            // 1) Подтверждение
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
                // 2) Грузим синонимы (алиасы) и удаляем бренд (алиасы уйдут каскадом)
                var brand = await _context.Brands
                    .Include(b => b.Aliases)
                    .FirstOrDefaultAsync(b => b.Id == SelectedBrand.Id);

                if (brand == null) return;

                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();

                // 3) Обновляем UI
                Brands.Remove(SelectedBrand);
                Aliases.Clear();
                SelectedBrand = null;
                UpdateGroupedBrands();
            }
            catch (DbUpdateException ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Ошибка при удалении бренда");
                var errDlg = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = msg,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await errDlg.ShowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении бренда");
                var errDlg = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = xamlRoot
                };
                await errDlg.ShowAsync();
            }
        }

        private async Task LoadBrandsAsync()
        {
            try
            {
                var brands = await _context.Brands
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id)
                    })
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
                    .OrderByDescending(a => a.IsPrimary)   // сначала primary = true
                    .ThenBy(a => a.Alias)                  // потом по алфавиту
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

        public async Task SaveAliasAsync(string aliasName)
        {
            if (SelectedBrand == null) throw new InvalidOperationException("Бренд не выбран.");

            var name = (aliasName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Введите синоним.");

            // Проверка дубликата для выбранного бренда (регистронезависимо)
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
                Note = "" // или "добавлено вручную"
            };

            await _context.BrandAliases.AddAsync(alias);
            await _context.SaveChangesAsync();

            // Обновим список (первичный всегда сверху, как вы просили)
            await LoadAliasesAsync();

            // Выделим только что добавленный синоним
            SelectedAlias = Aliases.FirstOrDefault(a => a.Id == alias.Id);
        }

        private bool CanAddAlias()
        {
            return SelectedBrand != null;
        }

        private async Task AddAlias()
        {
            if (SelectedBrand == null) return;

            try
            {
                var baseAlias = "Новый синоним";
                var aliasName = baseAlias;
                int counter = 1;

                while (await _context.BrandAliases
                    .AnyAsync(a => a.Alias.ToLower() == aliasName.ToLower()))
                {
                    aliasName = $"{baseAlias} {counter++}";
                }

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
                _logger.LogError(ex, "Ошибка EF при добавлении синонима");
                Debug.WriteLine("Ошибка: " + ex.InnerException?.Message);
            }
        }

        private async Task EditAliasAsync(XamlRoot xamlRoot)
        {
            if (SelectedAlias == null || xamlRoot == null) return;

            try
            {
                var dialog = new EditBrandDialog(SelectedAlias.Alias)
                {
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.BrandName))
                {
                    // Создаем новый scope и контекст для избежания конфликтов отслеживания
                    using var scope = App.ServiceProvider.CreateScope();
                    using var newContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    if (SelectedAlias.IsPrimary)
                    {
                        // Для основного алиаса обновляем и бренд и алиас
                        var brand = await newContext.Brands.FindAsync(SelectedBrand.Id);
                        if (brand != null)
                        {
                            brand.Name = dialog.BrandName;
                        }

                        var alias = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                        if (alias != null)
                        {
                            alias.Alias = dialog.BrandName;
                        }
                    }
                    else
                    {
                        // Для обычного алиаса обновляем только алиас
                        var alias = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                        if (alias != null)
                        {
                            alias.Alias = dialog.BrandName;
                        }
                    }

                    await newContext.SaveChangesAsync();

                    // Обновляем локальные данные
                    SelectedAlias.Alias = dialog.BrandName;
                    if (SelectedAlias.IsPrimary && SelectedBrand != null)
                    {
                        SelectedBrand.Name = dialog.BrandName;
                    }

                    // Перезагружаем данные
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
                var dialog = new DeleteBrandDialog(SelectedAlias.Alias)
                {
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Нельзя удалить основной алиас
                    if (SelectedAlias.IsPrimary)
                    {
                        Debug.WriteLine("Cannot delete primary alias");
                        return;
                    }

                    // Создаем новый scope и контекст
                    using var scope = App.ServiceProvider.CreateScope();
                    using var newContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var aliasToDelete = await newContext.BrandAliases.FindAsync(SelectedAlias.Id);
                    if (aliasToDelete != null)
                    {
                        newContext.BrandAliases.Remove(aliasToDelete);
                        await newContext.SaveChangesAsync();

                        // Удаляем из локальной коллекции
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

        public async Task EditBrand(Brand brand)
        {
            if (brand == null || XamlRoot == null) return;

            var dialog = new EditBrandDialog(brand.Name)
            {
                XamlRoot = XamlRoot
            };

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

            var dialog = new DeleteBrandDialog(brand.Name)
            {
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
                await LoadBrandsAsync();
            }
        }

        private async Task UpdateBrandName(string newName)
        {
            // Обновляем название бренда и основной алиас
            if (SelectedBrand == null) return;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Обновляем бренд
                SelectedBrand.Name = newName;
                _context.Brands.Update(SelectedBrand);

                // Обновляем основной алиас
                var primaryAlias = await _context.BrandAliases
                    .FirstOrDefaultAsync(a => a.BrandId == SelectedBrand.Id && a.IsPrimary);

                if (primaryAlias != null)
                {
                    primaryAlias.Alias = newName;
                    _context.BrandAliases.Update(primaryAlias);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Обновляем локальные данные
                SelectedAlias.Alias = newName;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task UpdateAliasOnly(string newAlias)
        {
            // Обновляем только неосновной алиас
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Отсоединяем сущность чтобы избежать конфликта отслеживания
                _context.Entry(SelectedAlias).State = EntityState.Detached;

                var aliasToUpdate = await _context.BrandAliases
                    .FindAsync(SelectedAlias.Id);

                if (aliasToUpdate != null && !aliasToUpdate.IsPrimary)
                {
                    aliasToUpdate.Alias = newAlias;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Обновляем локальные данные
                    SelectedAlias.Alias = newAlias;
                }
                else
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine("Cannot update primary alias through this method");
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}