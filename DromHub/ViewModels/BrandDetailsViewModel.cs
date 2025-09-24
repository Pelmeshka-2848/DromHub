using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace DromHub.ViewModels
{
    public enum BrandDetailsSection { Overview, Parts, Aliases, About, Changes }

    public class BrandDetailsViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;
        private XamlRoot _xr;

        // --- Identity / title ---
        private Guid _brandId;
        public Guid BrandId { get => _brandId; set => SetProperty(ref _brandId, value); }

        private string _brandName;
        public string BrandName { get => _brandName; set { if (SetProperty(ref _brandName, value)) BrandNameUpper = (value ?? "").ToUpperInvariant(); } }

        private string _brandNameUpper;
        public string BrandNameUpper { get => _brandNameUpper; set => SetProperty(ref _brandNameUpper, value); }

        // --- Meta ---
        private string _country = "Страна не указана";
        public string Country { get => _country; set => SetProperty(ref _country, value); }

        private string _ownerCompany;
        public string OwnerCompany { get => _ownerCompany; set => SetProperty(ref _ownerCompany, value); }

        private string _website;
        public string Website { get => _website; set { if (SetProperty(ref _website, value)) OnPropertyChanged(nameof(HasWebsite)); } }

        public bool HasWebsite => Uri.TryCreate(Website, UriKind.Absolute, out _);
        public string WebsiteDisplay => Website;

        // --- Neighbors ---
        private Guid? _prevBrandId;
        public Guid? PrevBrandId { get => _prevBrandId; set { if (SetProperty(ref _prevBrandId, value)) OnPropertyChanged(nameof(HasPrev)); } }

        private string _prevBrandNameUpper;
        public string PrevBrandNameUpper { get => _prevBrandNameUpper; set => SetProperty(ref _prevBrandNameUpper, value); }

        private Guid? _nextBrandId;
        public Guid? NextBrandId { get => _nextBrandId; set { if (SetProperty(ref _nextBrandId, value)) OnPropertyChanged(nameof(HasNext)); } }

        private string _nextBrandNameUpper;
        public string NextBrandNameUpper { get => _nextBrandNameUpper; set => SetProperty(ref _nextBrandNameUpper, value); }

        public bool HasPrev => PrevBrandId.HasValue;
        public bool HasNext => NextBrandId.HasValue;

        // --- Section ---
        private BrandDetailsSection _section = BrandDetailsSection.Overview;
        public BrandDetailsSection Section { get => _section; set => SetProperty(ref _section, value); }

        // --- Markup ---
        private double _markupEditor; // 0..1000
        public double MarkupEditor { get => _markupEditor; set => SetProperty(ref _markupEditor, value); }

        private bool _showDisableConfirm;
        public bool ShowDisableConfirm { get => _showDisableConfirm; set => SetProperty(ref _showDisableConfirm, value); }

        // --- Collections ---
        public ObservableCollection<Part> Parts { get; } = new();
        public ObservableCollection<BrandAlias> Aliases { get; } = new();

        // --- Aliases selection + undo ---
        private BrandAlias _selectedAlias;
        public BrandAlias SelectedAlias
        {
            get => _selectedAlias;
            set
            {
                if (SetProperty(ref _selectedAlias, value))
                {
                    OnPropertyChanged(nameof(CanEditAlias));
                    OnPropertyChanged(nameof(CanDeleteAlias));
                }
            }
        }

        private bool _undoIsOpen;
        public bool UndoIsOpen { get => _undoIsOpen; set => SetProperty(ref _undoIsOpen, value); }

        private string _undoMessage;
        public string UndoMessage { get => _undoMessage; set => SetProperty(ref _undoMessage, value); }

        private BrandAlias _lastDeleted;

        public bool CanEditAlias => SelectedAlias != null;
        public bool CanDeleteAlias => SelectedAlias != null && !SelectedAlias.IsPrimary;

        // --- Diagnostics ---
        private bool _diagNoPrimaryAlias;
        public bool DiagNoPrimaryAlias { get => _diagNoPrimaryAlias; set => SetProperty(ref _diagNoPrimaryAlias, value); }

        private bool _diagNoParts;
        public bool DiagNoParts { get => _diagNoParts; set => SetProperty(ref _diagNoParts, value); }

        private bool _diagDuplicateNames;
        public bool DiagDuplicateNames { get => _diagDuplicateNames; set => SetProperty(ref _diagDuplicateNames, value); }

        // --- Commands ---
        public IAsyncRelayCommand SaveMarkupCommand { get; }
        public IAsyncRelayCommand ResetMarkupCommand { get; }
        public IRelayCommand UndoDeleteAliasCommand { get; }

        public BrandDetailsViewModel(ApplicationDbContext db)
        {
            _db = db;
            SaveMarkupCommand = new AsyncRelayCommand(SaveMarkupAsync);
            ResetMarkupCommand = new AsyncRelayCommand(ResetMarkupAsync);
            UndoDeleteAliasCommand = new RelayCommand(async () => await UndoDeleteAsync());
        }

        public async Task InitializeAsync(Guid id, XamlRoot xr)
        {
            _xr = xr;
            BrandId = id;

            var brand = await _db.Brands
                .Include(b => b.Aliases)
                .Include(b => b.Parts)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (brand == null) return;

            BrandName = brand.Name;

            // TODO: когда появятся поля в БД — заполнить Country/OwnerCompany/Website из brand.*
            // Пока оставляем дефолты, чтобы не было ложных присваиваний.

            // markup
            var markup = await _db.BrandMarkups
                .Where(m => m.BrandId == id)
                .Select(m => (double?)m.MarkupPct)
                .FirstOrDefaultAsync();
            MarkupEditor = markup ?? 0;

            // parts
            Parts.Clear();
            var parts = await _db.Parts
                .Where(p => p.BrandId == id)
                .OrderBy(p => p.Name)
                .Take(1000)
                .AsNoTracking()
                .ToListAsync();
            foreach (var p in parts) Parts.Add(p);

            // aliases
            Aliases.Clear();
            foreach (var a in brand.Aliases.OrderByDescending(a => a.IsPrimary).ThenBy(a => a.Alias))
                Aliases.Add(a);

            // diagnostics
            DiagNoPrimaryAlias = !brand.Aliases.Any(a => a.IsPrimary);
            DiagNoParts = brand.Parts == null || brand.Parts.Count == 0;
            DiagDuplicateNames = await _db.Brands.AnyAsync(b => b.Id != brand.Id && b.NormalizedName == brand.NormalizedName);

            // neighbors
            await LoadNeighborsAsync(id);
        }

        private async Task LoadNeighborsAsync(Guid id)
        {
            var sorted = await _db.Brands
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name })
                .AsNoTracking()
                .ToListAsync();

            var index = sorted.FindIndex(x => x.Id == id);

            if (index > 0)
            {
                PrevBrandId = sorted[index - 1].Id;
                PrevBrandNameUpper = (sorted[index - 1].Name ?? "").ToUpperInvariant();
            }
            else
            {
                PrevBrandId = null;
                PrevBrandNameUpper = null;
            }

            if (index >= 0 && index < sorted.Count - 1)
            {
                NextBrandId = sorted[index + 1].Id;
                NextBrandNameUpper = (sorted[index + 1].Name ?? "").ToUpperInvariant();
            }
            else
            {
                NextBrandId = null;
                NextBrandNameUpper = null;
            }
        }

        // --- Markup ---
        private async Task SaveMarkupAsync()
        {
            if (MarkupEditor == 0 && !ShowDisableConfirm)
            {
                ShowDisableConfirm = true;
                return;
            }

            var existing = await _db.BrandMarkups.FirstOrDefaultAsync(m => m.BrandId == BrandId);
            var toSave = (decimal)Math.Clamp(MarkupEditor, 0, 1000);

            if (existing == null)
                await _db.BrandMarkups.AddAsync(new BrandMarkup { Id = Guid.NewGuid(), BrandId = BrandId, MarkupPct = toSave });
            else
                existing.MarkupPct = toSave;

            await _db.SaveChangesAsync();
            ShowDisableConfirm = false;

            await new ContentDialog
            {
                Title = "Готово",
                Content = toSave == 0 ? "Наценка отключена (0%)." : $"Наценка сохранена: {toSave}%.",
                CloseButtonText = "ОК",
                XamlRoot = _xr
            }.ShowAsync();
        }

        private async Task ResetMarkupAsync()
        {
            var m = await _db.BrandMarkups.FirstOrDefaultAsync(x => x.BrandId == BrandId);
            if (m != null)
            {
                m.MarkupPct = 0m;
                await _db.SaveChangesAsync();
            }
            MarkupEditor = 0;
        }

        public void ConfirmDisable() => _ = SaveMarkupAsync();
        public void CancelDisable() => ShowDisableConfirm = false;

        // --- Aliases ---
        public async Task AddAliasAsync()
        {
            var dlg = new Views.AddAliasDialog { XamlRoot = _xr };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            var alias = new BrandAlias { Id = Guid.NewGuid(), BrandId = BrandId, Alias = dlg.AliasName, IsPrimary = false };
            await _db.BrandAliases.AddAsync(alias);
            await _db.SaveChangesAsync();
            Aliases.Add(alias);
        }

        public async Task EditAliasAsync()
        {
            if (SelectedAlias == null) return;
            var dlg = new Views.EditBrandDialog(SelectedAlias.Alias) { XamlRoot = _xr };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            SelectedAlias.Alias = dlg.BrandName;
            _db.BrandAliases.Update(SelectedAlias);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAliasAsync()
        {
            if (!CanDeleteAlias) return;

            _lastDeleted = SelectedAlias;
            _db.BrandAliases.Remove(_lastDeleted);
            await _db.SaveChangesAsync();
            Aliases.Remove(_lastDeleted);

            UndoMessage = $"«{_lastDeleted.Alias}» удалён. Можно отменить.";
            UndoIsOpen = true;

            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                UndoIsOpen = false;
                _lastDeleted = null;
            });
        }

        private async Task UndoDeleteAsync()
        {
            if (_lastDeleted == null) return;
            await _db.BrandAliases.AddAsync(_lastDeleted);
            await _db.SaveChangesAsync();
            Aliases.Insert(0, _lastDeleted);
            _lastDeleted = null;
            UndoIsOpen = false;
        }

        // --- Website ---
        public async Task OpenWebsiteAsync()
        {
            if (!HasWebsite) return;
            try
            {
                _ = await Launcher.LaunchUriAsync(new Uri(Website));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await new ContentDialog
                {
                    Title = "Не удалось открыть сайт",
                    Content = Website,
                    CloseButtonText = "ОК",
                    XamlRoot = _xr
                }.ShowAsync();
            }
        }

        // About
        public string AboutText =>
            $"Бренд {BrandName}.\n" +
            $"Страна: {(!string.IsNullOrWhiteSpace(Country) ? Country : "—")}.\n" +
            $"Владелец: {(!string.IsNullOrWhiteSpace(OwnerCompany) ? OwnerCompany : "—")}.\n" +
            $"Сайт: {(HasWebsite ? Website : "—")}.";
    }
}