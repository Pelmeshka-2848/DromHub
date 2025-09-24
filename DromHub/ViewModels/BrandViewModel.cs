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

        // Наценка (2 состояния)
        private double? _brandMarkupPercent;

        // Быстрые фильтры
        private bool _filterIsOem;
        private bool _filterWithMarkup;
        private bool _filterMarkupOff;
        private bool _filterNoAliases;

        // Диагностика выбранного бренда
        private bool _diagNoPrimaryAlias;
        private bool _diagZeroParts;
        private bool _diagDuplicateName;
        private string _diagnosticsText;

        // Для Undo
        public BrandAlias LastDeletedAlias { get; private set; }

        public XamlRoot XamlRoot { get; set; }

        public BrandViewModel(ApplicationDbContext context, ILogger<BrandViewModel> logger)
        {
            _context = context;
            _logger = logger;

            Brands = new ObservableCollection<Brand>();
            Aliases = new ObservableCollection<BrandAlias>();
            GroupedBrands = new ObservableCollection<AlphaKeyGroup<Brand>>();

            LoadBrandsCommand = new AsyncRelayCommand(RefreshBrandsAsync);
            SearchBrandsCommand = new AsyncRelayCommand(RefreshBrandsAsync);
            LoadAliasesCommand = new AsyncRelayCommand(LoadAliasesAsync);
            SaveBrandCommand = new AsyncRelayCommand(SaveBrandAsync);

            AddAliasCommand = new AsyncRelayCommand(AddAlias);
            EditAliasCommand = new AsyncRelayCommand<XamlRoot>(EditAliasAsync, _ => CanEditOrDeleteAlias);
            DeleteAliasCommand = new AsyncRelayCommand<XamlRoot>(DeleteAliasAsync, _ => CanEditOrDeleteAlias);
            UndoDeleteAliasCommand = new AsyncRelayCommand<BrandAlias>(UndoDeleteAliasAsync);

            DeleteBrandCommand = new AsyncRelayCommand<XamlRoot>(DeleteBrandAsync, _ => SelectedBrand != null);

            SaveBrandMarkupCommand = new AsyncRelayCommand(SaveBrandMarkupAsync, () => SelectedBrand != null);
            ClearBrandMarkupCommand = new AsyncRelayCommand(ClearBrandMarkupAsync, () => SelectedBrand != null);
        }

        #region Коллекции/свойства

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
                    OnPropertyChanged();

                    Aliases.Clear();
                    _ = LoadAliasesCommand.ExecuteAsync(null);
                    _ = LoadBrandMarkupAsync();
                    _ = UpdateDiagnosticsAsync();

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

        // Редактор значения %
        public double? BrandMarkupPercent
        {
            get => _brandMarkupPercent;
            set { if (_brandMarkupPercent != value) { _brandMarkupPercent = value; OnPropertyChanged(); } }
        }

        // Быстрые фильтры
        public bool FilterIsOem
        {
            get => _filterIsOem;
            set { if (_filterIsOem != value) { _filterIsOem = value; OnPropertyChanged(); _ = RefreshBrandsAsync(); } }
        }
        public bool FilterWithMarkup
        {
            get => _filterWithMarkup;
            set { if (_filterWithMarkup != value) { _filterWithMarkup = value; OnPropertyChanged(); _ = RefreshBrandsAsync(); } }
        }
        public bool FilterMarkupOff
        {
            get => _filterMarkupOff;
            set { if (_filterMarkupOff != value) { _filterMarkupOff = value; OnPropertyChanged(); _ = RefreshBrandsAsync(); } }
        }
        public bool FilterNoAliases
        {
            get => _filterNoAliases;
            set { if (_filterNoAliases != value) { _filterNoAliases = value; OnPropertyChanged(); _ = RefreshBrandsAsync(); } }
        }

        // Диагностика
        public bool DiagNoPrimaryAlias { get => _diagNoPrimaryAlias; private set { _diagNoPrimaryAlias = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiagnostics)); } }
        public bool DiagZeroParts { get => _diagZeroParts; private set { _diagZeroParts = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiagnostics)); } }
        public bool DiagDuplicateName { get => _diagDuplicateName; private set { _diagDuplicateName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDiagnostics)); } }

        public bool HasDiagnostics => DiagNoPrimaryAlias || DiagZeroParts || DiagDuplicateName;

        public string DiagnosticsText
        {
            get => _diagnosticsText;
            private set { if (_diagnosticsText != value) { _diagnosticsText = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Команды

        public IAsyncRelayCommand LoadBrandsCommand { get; }
        public IAsyncRelayCommand SearchBrandsCommand { get; }
        public IAsyncRelayCommand LoadAliasesCommand { get; }
        public IAsyncRelayCommand SaveBrandCommand { get; }

        public IAsyncRelayCommand AddAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> EditAliasCommand { get; }
        public IAsyncRelayCommand<XamlRoot> DeleteAliasCommand { get; }
        public IAsyncRelayCommand<BrandAlias> UndoDeleteAliasCommand { get; }

        public IAsyncRelayCommand<XamlRoot> DeleteBrandCommand { get; }

        public IAsyncRelayCommand SaveBrandMarkupCommand { get; }
        public IAsyncRelayCommand ClearBrandMarkupCommand { get; }

        #endregion

        #region Утилиты

        public void ResetBrand()
        {
            Brand = new Brand();
            SelectedBrand = null;
            SelectedAlias = null;
            BrandMarkupPercent = 0;
            DiagnosticsText = string.Empty;
            DiagNoPrimaryAlias = DiagZeroParts = DiagDuplicateName = false;
        }

        #endregion

        #region Загрузка/фильтрация

        private async Task RefreshBrandsAsync()
        {
            try
            {
                var text = (SearchText ?? string.Empty).Trim();

                var q = _context.Brands.AsQueryable();

                if (!string.IsNullOrWhiteSpace(text))
                    q = q.Where(b => EF.Functions.ILike(b.Name, $"%{text}%") ||
                                     b.Aliases.Any(a => EF.Functions.ILike(a.Alias, $"%{text}%")));

                // Проекция с полями для фильтров/бейджей
                var list = await q
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        IsOem = b.IsOem,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id),

                        // если записи нет — считаем 0% (наценка не применяется)
                        MarkupPercent = _context.BrandMarkups
                            .Where(m => m.BrandId == b.Id)
                            .Select(m => (decimal?)m.MarkupPct)
                            .FirstOrDefault() ?? 0m,

                        // Кол-ва для быстрых фильтров/диагностики
                        AliasesCount = _context.BrandAliases.Count(a => a.BrandId == b.Id),
                        NonPrimaryAliasesCount = _context.BrandAliases.Count(a => a.BrandId == b.Id && !a.IsPrimary)
                    })
                    .OrderBy(b => b.Name)
                    .AsNoTracking()
                    .ToListAsync();

                // Фильтры (двухсост. наценка: 0% = выкл, ≠0% = вкл)
                if (FilterIsOem)
                    list = list.Where(b => b.IsOem).ToList();

                if (FilterWithMarkup)
                    list = list.Where(b => (b.MarkupPercent ?? 0m) != 0m).ToList();

                if (FilterMarkupOff)
                    list = list.Where(b => (b.MarkupPercent ?? 0m) == 0m).ToList();

                // "Без алиасов": оставил критерий "нет дополнительных (неосновных) алиасов"
                if (FilterNoAliases)
                    list = list.Where(b => b.NonPrimaryAliasesCount == 0).ToList();
                // Если нужно "совсем без алиасов", замените на:
                // list = list.Where(b => b.AliasesCount == 0).ToList();

                Brands.Clear();
                foreach (var b in list) Brands.Add(b);
                UpdateGroupedBrands();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing brands");
            }
        }

        private void UpdateGroupedBrands()
        {
            GroupedBrands.Clear();
            var grouped = AlphaKeyGroup<Brand>.CreateGroups(Brands, b => b.Name, true);
            foreach (var g in grouped) GroupedBrands.Add(g);
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
                foreach (var a in aliases) Aliases.Add(a);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading aliases");
            }
        }

        private async Task LoadBrandMarkupAsync()
        {
            if (SelectedBrand == null) { BrandMarkupPercent = 0; return; }

            var m = await _context.BrandMarkups
                        .AsNoTracking()
                        .Where(x => x.BrandId == SelectedBrand.Id)
                        .Select(x => (double?)x.MarkupPct)
                        .FirstOrDefaultAsync();

            BrandMarkupPercent = m ?? 0d;
        }

        private async Task UpdateDiagnosticsAsync()
        {
            if (SelectedBrand == null)
            {
                DiagNoPrimaryAlias = DiagZeroParts = DiagDuplicateName = false;
                DiagnosticsText = string.Empty;
                return;
            }

            var brandId = SelectedBrand.Id;
            var name = SelectedBrand.Name?.Trim() ?? string.Empty;
            var normalized = name.ToLower();

            var hasPrimary = await _context.BrandAliases
                .AnyAsync(a => a.BrandId == brandId && a.IsPrimary);

            var parts = await _context.Parts
                .CountAsync(p => p.BrandId == brandId);

            var duplicate = await _context.Brands
                .AnyAsync(b => b.Id != brandId &&
                               (b.NormalizedName == normalized || EF.Functions.ILike(b.Name, name)));

            DiagNoPrimaryAlias = !hasPrimary;
            DiagZeroParts = parts == 0;
            DiagDuplicateName = duplicate;

            var items = new[]
            {
                DiagNoPrimaryAlias ? "нет основного алиаса" : null,
                DiagZeroParts      ? "0 деталей"            : null,
                DiagDuplicateName  ? "дубликаты имён"       : null
            }.Where(s => s != null);

            DiagnosticsText = string.Join(" • ", items);
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

                    // основной алиас
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
                }
                else
                {
                    var entity = await _context.Brands.FindAsync(Brand.Id);
                    if (entity == null) return;

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
                }

                await RefreshBrandsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении бренда");
            }
        }

        private async Task DeleteBrandAsync(XamlRoot xamlRoot)
        {
            if (SelectedBrand == null) return;

            var partsCount = await _context.Parts.Where(p => p.BrandId == SelectedBrand.Id).CountAsync();
            if (partsCount > 0)
            {
                await ShowDialogAsync("Удаление невозможно",
                    $"Нельзя удалить бренд «{SelectedBrand.Name}», т.к. с ним связаны {partsCount} запчаст(ь/и).",
                    xamlRoot);
                return;
            }

            var confirm = new ContentDialog
            {
                Title = "Удалить бренд?",
                Content = $"Бренд «{SelectedBrand.Name}» и его синонимы будут удалены. Продолжить?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = xamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

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
            catch (Exception ex)
            {
                await ShowDialogAsync("Ошибка", ex.Message, xamlRoot);
            }
        }

        #endregion

        #region Наценка

        private async Task SaveBrandMarkupAsync()
        {
            if (SelectedBrand == null) return;

            var newPct = (decimal)(BrandMarkupPercent ?? 0d);
            var existing = await _context.BrandMarkups.FirstOrDefaultAsync(x => x.BrandId == SelectedBrand.Id);
            var wasPct = existing?.MarkupPct ?? 0m;

            // подтверждение при выключении (было >0, сохраняем 0)
            if (wasPct > 0m && newPct == 0m)
            {
                var xr = GetXamlRoot();
                var dlg = new ContentDialog
                {
                    Title = "Отключить наценку?",
                    Content = "При 0% наценка перестанет применяться ко всем деталям этого бренда.",
                    PrimaryButtonText = "Отключить",
                    CloseButtonText = "Отмена",
                    XamlRoot = xr
                };
                var res = await dlg.ShowAsync();
                if (res != ContentDialogResult.Primary) return;
            }

            if (existing == null)
            {
                if (newPct != 0m)
                {
                    await _context.BrandMarkups.AddAsync(new BrandMarkup
                    {
                        Id = Guid.NewGuid(),
                        BrandId = SelectedBrand.Id,
                        MarkupPct = newPct
                    });
                }
            }
            else
            {
                if (newPct == 0m)
                    _context.BrandMarkups.Remove(existing); // чистим запись
                else
                    existing.MarkupPct = newPct;
            }

            await _context.SaveChangesAsync();

            // Обновим UI и левый список
            await LoadBrandMarkupAsync();
            var left = Brands.FirstOrDefault(b => b.Id == SelectedBrand.Id);
            if (left != null)
            {
                left.MarkupPercent = newPct;
                UpdateGroupedBrands();
            }

            await ShowInfoAsync("Готово", $"Наценка сохранена: {newPct:0.#}%");
        }


        private async Task ClearBrandMarkupAsync()
        {
            if (SelectedBrand == null) return;

            var existing = await _context.BrandMarkups.FirstOrDefaultAsync(x => x.BrandId == SelectedBrand.Id);
            if (existing != null)
            {
                _context.BrandMarkups.Remove(existing);
                await _context.SaveChangesAsync();
            }

            BrandMarkupPercent = 0;

            var left = Brands.FirstOrDefault(b => b.Id == SelectedBrand.Id);
            if (left != null)
            {
                left.MarkupPercent = 0;
                UpdateGroupedBrands();
            }

            await ShowInfoAsync("Готово", "Наценка сброшена до 0%.");
        }

        #endregion

        #region Алиасы (+ Undo)

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
            }
        }

        public async Task SaveAliasAsync(string aliasName)
        {
            if (SelectedBrand == null) throw new InvalidOperationException("Бренд не выбран.");

            var name = (aliasName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Введите синоним.");

            var exists = await _context.BrandAliases
                .AnyAsync(a => a.BrandId == SelectedBrand.Id && EF.Functions.ILike(a.Alias, name));
            if (exists) throw new InvalidOperationException("Такой синоним уже существует.");

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
                _logger.LogError(ex, "Error editing alias");
            }
        }

        private async Task DeleteAliasAsync(XamlRoot xamlRoot)
        {
            if (SelectedAlias == null || xamlRoot == null) return;

            try
            {
                var dialog = new DeleteBrandDialog(SelectedAlias.Alias) { XamlRoot = xamlRoot };
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                if (SelectedAlias.IsPrimary)
                {
                    await ShowInfoAsync("Нельзя удалить", "Нельзя удалить основной алиас.");
                    return;
                }

                // копия для Undo
                LastDeletedAlias = new BrandAlias
                {
                    Id = SelectedAlias.Id,
                    BrandId = SelectedBrand.Id,
                    Alias = SelectedAlias.Alias,
                    IsPrimary = SelectedAlias.IsPrimary,
                    Note = SelectedAlias.Note
                };

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting alias");
            }
        }

        public async Task UndoDeleteAliasAsync(BrandAlias alias)
        {
            if (alias == null || SelectedBrand == null) return;

            var exists = await _context.BrandAliases.AnyAsync(a => a.Id == alias.Id);
            if (!exists)
            {
                // тот же Guid можно вернуть
                await _context.BrandAliases.AddAsync(new BrandAlias
                {
                    Id = alias.Id,
                    BrandId = alias.BrandId,
                    Alias = alias.Alias,
                    IsPrimary = alias.IsPrimary,
                    Note = alias.Note
                });
                await _context.SaveChangesAsync();
            }

            await LoadAliasesAsync();
            SelectedAlias = Aliases.FirstOrDefault(a => a.Id == alias.Id);
            LastDeletedAlias = null;
        }

        #endregion

        #region Диалоги/утилиты

        public async Task EditBrand(Brand brand)
        {
            if (brand == null || XamlRoot == null) return;

            var dialog = new EditBrandDialog(brand.Name) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                brand.Name = dialog.BrandName;
                _context.Brands.Update(brand);
                await _context.SaveChangesAsync();
                await RefreshBrandsAsync();
            }
        }

        public async Task DeleteBrand(Brand brand)
        {
            if (brand == null || XamlRoot == null) return;

            var dialog = new DeleteBrandDialog(brand.Name) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _context.Brands.Remove(brand);
                await _context.SaveChangesAsync();
                await RefreshBrandsAsync();
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