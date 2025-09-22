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
using System.Collections.Generic;
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

        // полный («сырой») набор из БД — после фильтров/сортировки перекладываем в Brands
        private List<Brand> _allBrands = new();

        private string _searchText = string.Empty;
        private Brand _brand = new();
        private Brand _selectedBrand;
        private BrandAlias _selectedAlias;

        // Правая панель (редактор наценки)
        private double? _brandMarkupPercent;   // значение %, которое редактируем
        private bool _applyBrandMarkup;        // применять наценку для выбранного бренда

        // Быстрые фильтры / сортировка / мультивыбор
        private bool _filterOemOnly, _filterMarkupEnabled, _filterMarkupDisabled, _filterNoAliases;
        private int _sortIndex;           // 0: имя, 1: кол-во деталей, 2: % наценки, 3: дата обновления
        private bool _sortDescending;
        private bool _multiSelectEnabled;

        // Превью пересчёта
        private double _basePrice;
        private string _previewPriceText = string.Empty;

        public XamlRoot XamlRoot { get; set; }

        public BrandViewModel(ApplicationDbContext context, ILogger<BrandViewModel> logger)
        {
            _context = context;
            _logger = logger;

            Brands = new ObservableCollection<Brand>();
            Aliases = new ObservableCollection<BrandAlias>();
            GroupedBrands = new ObservableCollection<AlphaKeyGroup<Brand>>();

            // команды
            LoadBrandsCommand = new AsyncRelayCommand(LoadBrandsAsync);
            SearchBrandsCommand = new AsyncRelayCommand(SearchBrandsAsync);
            LoadAliasesCommand = new AsyncRelayCommand(LoadAliasesAsync);
            SaveBrandCommand = new AsyncRelayCommand(SaveBrandAsync);

            AddAliasCommand = new AsyncRelayCommand(AddAlias);
            EditAliasCommand = new AsyncRelayCommand<XamlRoot>(EditAliasAsync, _ => CanEditOrDeleteAlias);
            DeleteAliasCommand = new AsyncRelayCommand<XamlRoot>(DeleteAliasAsync, _ => CanEditOrDeleteAlias);

            DeleteBrandCommand = new AsyncRelayCommand<XamlRoot>(DeleteBrandAsync, _ => SelectedBrand != null);

            SaveBrandMarkupCommand = new AsyncRelayCommand(SaveBrandMarkupAsync, () => SelectedBrand != null);
            ClearBrandMarkupCommand = new AsyncRelayCommand(ClearBrandMarkupAsync, () => SelectedBrand != null);
        }

        #region Коллекции / публичные свойства

        public ObservableCollection<Brand> Brands { get; }
        public ObservableCollection<BrandAlias> Aliases { get; }
        public ObservableCollection<AlphaKeyGroup<Brand>> GroupedBrands { get; }

        public Brand Brand
        {
            get => _brand;
            set
            {
                if (!ReferenceEquals(_brand, value))
                {
                    _brand = value;
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
                    _ = LoadBrandMarkupAsync();
                    UpdatePreview();
                    // обновляем диагностику
                    OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
                    OnPropertyChanged(nameof(ShowDiagZeroParts));
                    OnPropertyChanged(nameof(ShowDiagDuplicateName));

                    // обновляем доступности команд
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

        // Разрешать редактирование/удаление алиаса только если выбран и он не основной
        public bool CanEditOrDeleteAlias => SelectedAlias != null && !SelectedAlias.IsPrimary;

        public string SearchText
        {
            get => _searchText;
            set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); } }
        }

        // UI: число %
        public double? BrandMarkupPercent
        {
            get => _brandMarkupPercent;
            set
            {
                if (_brandMarkupPercent != value)
                {
                    _brandMarkupPercent = value;
                    OnPropertyChanged();
                    UpdatePreview();
                }
            }
        }

        // UI: применяем/не применяем наценку
        public bool ApplyBrandMarkup
        {
            get => _applyBrandMarkup;
            set
            {
                if (_applyBrandMarkup == value) return;

                // если выключаем — спросим подтверждение
                if (_applyBrandMarkup && !value && SelectedBrand != null)
                {
                    _ = ConfirmDisableMarkupAsync();
                    return; // дождёмся асинхронного подтверждения
                }

                _applyBrandMarkup = value;
                OnPropertyChanged();
                UpdatePreview();
            }
        }

        // Фильтры
        public bool FilterOemOnly
        {
            get => _filterOemOnly;
            set { if (_filterOemOnly != value) { _filterOemOnly = value; OnPropertyChanged(); ApplyFilters(); } }
        }
        public bool FilterMarkupEnabled
        {
            get => _filterMarkupEnabled;
            set { if (_filterMarkupEnabled != value) { _filterMarkupEnabled = value; OnPropertyChanged(); ApplyFilters(); } }
        }
        public bool FilterMarkupDisabled
        {
            get => _filterMarkupDisabled;
            set { if (_filterMarkupDisabled != value) { _filterMarkupDisabled = value; OnPropertyChanged(); ApplyFilters(); } }
        }
        public bool FilterNoAliases
        {
            get => _filterNoAliases;
            set { if (_filterNoAliases != value) { _filterNoAliases = value; OnPropertyChanged(); ApplyFilters(); } }
        }

        // Сортировка
        public int SortIndex
        {
            get => _sortIndex;
            set { if (_sortIndex != value) { _sortIndex = value; OnPropertyChanged(); ApplyFilters(); } }
        }
        public bool SortDescending
        {
            get => _sortDescending;
            set { if (_sortDescending != value) { _sortDescending = value; OnPropertyChanged(); OnPropertyChanged(nameof(SortDirText)); ApplyFilters(); } }
        }
        public string SortDirText => SortDescending ? "↓ убыв." : "↑ возр.";

        // Мультивыбор
        public bool MultiSelectEnabled
        {
            get => _multiSelectEnabled;
            set { if (_multiSelectEnabled != value) { _multiSelectEnabled = value; OnPropertyChanged(); } }
        }

        // Превью цены
        public double BasePrice
        {
            get => _basePrice;
            set { if (Math.Abs(_basePrice - value) > double.Epsilon) { _basePrice = value; OnPropertyChanged(); UpdatePreview(); } }
        }
        public string PreviewPriceText
        {
            get => _previewPriceText;
            private set { if (_previewPriceText != value) { _previewPriceText = value; OnPropertyChanged(); } }
        }

        // Диагностика — возвращаем non-null => видно, null => скрыто (исп. NullToVisibilityConverter)
        public string ShowDiagNoPrimaryAlias =>
            SelectedBrand == null ? null :
            (Aliases.Any(a => a.IsPrimary) ? null : "NO_PRIMARY");
        public string ShowDiagZeroParts =>
            SelectedBrand == null ? null :
            (SelectedBrand.PartsCount == 0 ? "ZERO_PARTS" : null);
        public string ShowDiagDuplicateName =>
            SelectedBrand == null ? null :
            (Brands.Count(b => string.Equals(b.Name, SelectedBrand.Name, StringComparison.OrdinalIgnoreCase)) > 1 ? "DUP" : null);

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

        #region Утилиты

        public void ResetBrand()
        {
            Brand = new Brand();
            SelectedBrand = null;
            SelectedAlias = null;
            BrandMarkupPercent = null;
            ApplyBrandMarkup = false;
            BasePrice = 0;
            PreviewPriceText = string.Empty;
        }

        private void UpdatePreview()
        {
            if (SelectedBrand == null)
            {
                PreviewPriceText = string.Empty;
                return;
            }

            var pct = (ApplyBrandMarkup && BrandMarkupPercent.HasValue) ? BrandMarkupPercent.Value : 0.0;
            if (BasePrice <= 0)
            {
                PreviewPriceText = string.Empty;
                return;
            }

            var after = BasePrice * (1.0 + pct / 100.0);
            PreviewPriceText = $"→ {after:0.##}";
        }

        private async Task ConfirmDisableMarkupAsync()
        {
            var xr = GetXamlRoot();
            if (xr != null)
            {
                var dlg = new ContentDialog
                {
                    Title = "Выключить наценку?",
                    Content = "Цены по бренду перестанут пересчитываться с наценкой.",
                    PrimaryButtonText = "Выключить",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xr
                };
                var res = await dlg.ShowAsync();
                if (res != ContentDialogResult.Primary)
                {
                    // откат
                    OnPropertyChanged(nameof(ApplyBrandMarkup));
                    return;
                }
            }
            _applyBrandMarkup = false;
            OnPropertyChanged(nameof(ApplyBrandMarkup));
            UpdatePreview();
        }

        #endregion

        #region Загрузка данных

        private async Task LoadBrandsAsync()
        {
            try
            {
                var list = await _context.Brands
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        IsOem = b.IsOem,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id),
                        AliasesCount = _context.BrandAliases.Count(a => a.BrandId == b.Id),
                        MarkupPercent = _context.BrandMarkups.Where(m => m.BrandId == b.Id).Select(m => (decimal?)m.MarkupPct).FirstOrDefault(),
                        MarkupEnabled = _context.BrandMarkups.Where(m => m.BrandId == b.Id).Select(m => (bool?)m.IsEnabled).FirstOrDefault(),
                        UpdatedAt = b.UpdatedAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                _allBrands = list;
                ApplyFilters();
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
                var q = _context.Brands.AsQueryable();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    q = q.Where(b =>
                        EF.Functions.ILike(b.Name, $"%{text}%") ||
                        b.Aliases.Any(a => EF.Functions.ILike(a.Alias, $"%{text}%")));
                }

                _allBrands = await q
                    .Select(b => new Brand
                    {
                        Id = b.Id,
                        Name = b.Name,
                        IsOem = b.IsOem,
                        PartsCount = _context.Parts.Count(p => p.BrandId == b.Id),
                        AliasesCount = _context.BrandAliases.Count(a => a.BrandId == b.Id),
                        MarkupPercent = _context.BrandMarkups.Where(m => m.BrandId == b.Id).Select(m => (decimal?)m.MarkupPct).FirstOrDefault(),
                        MarkupEnabled = _context.BrandMarkups.Where(m => m.BrandId == b.Id).Select(m => (bool?)m.IsEnabled).FirstOrDefault(),
                        UpdatedAt = b.UpdatedAt
                    })
                    .AsNoTracking()
                    .ToListAsync();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching brands");
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<Brand> q = _allBrands;

            if (FilterOemOnly)        q = q.Where(b => b.IsOem);
            if (FilterMarkupEnabled)  q = q.Where(b => b.MarkupEnabled == true);
            if (FilterMarkupDisabled) q = q.Where(b => b.MarkupEnabled == false);
            if (FilterNoAliases)      q = q.Where(b => b.AliasesCount == 0);

            // сортировка
            q = SortIndex switch
            {
                1 => (SortDescending ? q.OrderByDescending(b => b.PartsCount) : q.OrderBy(b => b.PartsCount)),
                2 => (SortDescending ? q.OrderByDescending(b => b.MarkupPercent ?? decimal.MinValue)
                                     : q.OrderBy(b => b.MarkupPercent ?? decimal.MinValue)),
                3 => (SortDescending ? q.OrderByDescending(b => b.UpdatedAt) : q.OrderBy(b => b.UpdatedAt)),
                _ => (SortDescending ? q.OrderByDescending(b => b.Name) : q.OrderBy(b => b.Name))
            };

            // переложим в публичную коллекцию
            Brands.Clear();
            foreach (var b in q) Brands.Add(b);

            // и пересоздадим группы для левого списка/индекса
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
                foreach (var alias in aliases) Aliases.Add(alias);

                // обновим диагностику «нет основного алиаса»
                OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
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
                ApplyBrandMarkup = false;
                return;
            }

            var m = await _context.BrandMarkups
                .AsNoTracking()
                .Where(x => x.BrandId == SelectedBrand.Id)
                .Select(x => new { x.MarkupPct, x.IsEnabled })
                .FirstOrDefaultAsync();

            if (m == null)
            {
                BrandMarkupPercent = null;
                ApplyBrandMarkup = false;
            }
            else
            {
                BrandMarkupPercent = (double)m.MarkupPct;
                _applyBrandMarkup = m.IsEnabled; // внутреннее поле напрямую, чтобы не запускать подтверждение
                OnPropertyChanged(nameof(ApplyBrandMarkup));
            }

            UpdatePreview();
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

                    // создаём основной алиас
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
                    await LoadBrandsAsync();
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
                    await LoadBrandsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении бренда");
            }
        }

        private async Task DeleteBrandAsync(XamlRoot xamlRoot)
        {
            if (SelectedBrand == null) return;

            var partsCount = await _context.Parts.CountAsync(p => p.BrandId == SelectedBrand.Id);
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
                ApplyFilters();
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

        #region Наценка (сохранить / сбросить / массовое применение)

        private async Task SaveBrandMarkupAsync()
        {
            if (SelectedBrand == null) return;

            var existing = await _context.BrandMarkups
                .FirstOrDefaultAsync(x => x.BrandId == SelectedBrand.Id);

            var pctToSave = (decimal)(BrandMarkupPercent ?? 0d);

            if (existing == null)
            {
                var m = new BrandMarkup
                {
                    Id = Guid.NewGuid(),
                    BrandId = SelectedBrand.Id,
                    MarkupPct = pctToSave,
                    IsEnabled = ApplyBrandMarkup
                };
                await _context.BrandMarkups.AddAsync(m);
            }
            else
            {
                existing.MarkupPct = pctToSave;
                existing.IsEnabled = ApplyBrandMarkup;
            }

            await _context.SaveChangesAsync();

            // обновим карточку и левый список
            await LoadBrandMarkupAsync();
            var left = Brands.FirstOrDefault(b => b.Id == SelectedBrand.Id);
            if (left != null)
            {
                left.MarkupPercent = pctToSave;
                left.MarkupEnabled = ApplyBrandMarkup;
            }
            ApplyFilters(); // чтобы обновить группировку/сортировку

            await ShowInfoAsync("Готово", ApplyBrandMarkup ? "Наценка сохранена." : "Наценка отключена.");
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
            _applyBrandMarkup = false;
            OnPropertyChanged(nameof(ApplyBrandMarkup));
            UpdatePreview();

            var left = Brands.FirstOrDefault(b => b.Id == SelectedBrand.Id);
            if (left != null)
            {
                left.MarkupPercent = null;
                left.MarkupEnabled = null;
            }
            ApplyFilters();

            await ShowInfoAsync("Готово", "Запись наценки удалена (не задана).");
        }

        // публичный метод — вызывается из кода страницы (массовые действия)
        public async Task ApplyMarkupToBrandsAsync(IList<Brand> brands, decimal pct, bool enable)
        {
            if (brands == null || brands.Count == 0) return;

            foreach (var b in brands)
            {
                var m = await _context.BrandMarkups.FirstOrDefaultAsync(x => x.BrandId == b.Id);
                if (m == null)
                {
                    m = new BrandMarkup { Id = Guid.NewGuid(), BrandId = b.Id, MarkupPct = pct, IsEnabled = enable };
                    await _context.BrandMarkups.AddAsync(m);
                }
                else
                {
                    m.MarkupPct = pct;
                    m.IsEnabled = enable;
                }

                // сразу обновим в UI
                b.MarkupPercent = pct;
                b.MarkupEnabled = enable;
            }

            await _context.SaveChangesAsync();
            ApplyFilters();
            await ShowInfoAsync("Готово", $"Наценка {(enable ? "включена" : "выключена")} ({pct:0.##}%) для {brands.Count} брендов.");
        }

        #endregion

        #region Алиасы

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

                OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
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
            OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
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
                    OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
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

                if (result == ContentDialogResult.Primary)
                {
                    if (SelectedAlias.IsPrimary)
                    {
                        await ShowInfoAsync("Нельзя удалить", "Нельзя удалить основной синоним.");
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
                        OnPropertyChanged(nameof(ShowDiagNoPrimaryAlias));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting alias");
            }
        }

        #endregion

        #region Диалоги/утилиты

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