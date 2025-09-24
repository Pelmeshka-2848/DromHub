using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using DromHub.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    public partial class BrandDetailsViewModel : ObservableObject
    {
        private readonly ApplicationDbContext _db;
        private XamlRoot _xr;

        [ObservableProperty] private Guid brandId;
        [ObservableProperty] private string brandName;

        // markup
        [ObservableProperty] private double markupEditor; // 0..1000
        [ObservableProperty] private bool showDisableConfirm;

        // aliases + undo
        public ObservableCollection<BrandAlias> Aliases { get; } = new();
        [ObservableProperty] private BrandAlias selectedAlias;
        [ObservableProperty] private bool undoIsOpen;
        [ObservableProperty] private string undoMessage;
        private BrandAlias _lastDeleted;

        // parts
        public ObservableCollection<Part> Parts { get; } = new();

        // диагностика
        [ObservableProperty] private bool diagNoPrimaryAlias;
        [ObservableProperty] private bool diagNoParts;
        [ObservableProperty] private bool diagDuplicateNames;

        // соседи
        [ObservableProperty] private Guid? prevBrandId;
        [ObservableProperty] private Guid? nextBrandId;
        [ObservableProperty] private string prevBrandName;
        [ObservableProperty] private string nextBrandName;
        public bool HasPrev => PrevBrandId.HasValue;
        public bool HasNext => NextBrandId.HasValue;

        public IAsyncRelayCommand SaveMarkupCommand { get; }
        public IAsyncRelayCommand ResetMarkupCommand { get; }
        public IRelayCommand UndoDeleteAliasCommand { get; }

        public bool CanEditAlias => SelectedAlias != null;
        public bool CanDeleteAlias => SelectedAlias != null && !SelectedAlias.IsPrimary;

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
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);

            if (brand == null) return;

            BrandName = brand.Name;

            // markup (0% = off)
            var markup = await _db.BrandMarkups
                .Where(m => m.BrandId == id)
                .Select(m => (double?)m.MarkupPct)
                .FirstOrDefaultAsync();
            MarkupEditor = markup ?? 0;

            // aliases
            Aliases.Clear();
            foreach (var a in brand.Aliases.OrderByDescending(a => a.IsPrimary).ThenBy(a => a.Alias))
                Aliases.Add(a);

            // parts (покажем как есть – без доп. проекций)
            Parts.Clear();
            if (brand.Parts != null)
            {
                foreach (var p in brand.Parts.OrderBy(p => p.GetType().GetProperty("PartNumber") != null ?
                                                           p.GetType().GetProperty("PartNumber")!.GetValue(p) : null)
                                             .ThenBy(p => p.GetType().GetProperty("Name") != null ?
                                                           p.GetType().GetProperty("Name")!.GetValue(p) : null))
                    Parts.Add(p);
            }

            // диагностика
            DiagNoPrimaryAlias = !brand.Aliases.Any(a => a.IsPrimary);
            DiagNoParts = brand.Parts == null || brand.Parts.Count == 0;
            DiagDuplicateNames = await _db.Brands.AnyAsync(b => b.Id != brand.Id && b.NormalizedName == brand.NormalizedName);

            await ComputeNeighboursAsync();
            OnPropertyChanged(nameof(HasPrev));
            OnPropertyChanged(nameof(HasNext));
        }

        private async Task ComputeNeighboursAsync()
        {
            var all = await _db.Brands
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync();

            var idx = all.FindIndex(x => x.Id == BrandId);
            if (idx > 0)
            {
                PrevBrandId = all[idx - 1].Id;
                PrevBrandName = all[idx - 1].Name;
            }
            else
            {
                PrevBrandId = null;
                PrevBrandName = null;
            }

            if (idx >= 0 && idx < all.Count - 1)
            {
                NextBrandId = all[idx + 1].Id;
                NextBrandName = all[idx + 1].Name;
            }
            else
            {
                NextBrandId = null;
                NextBrandName = null;
            }
        }

        // --- markup ---
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
            var existing = await _db.BrandMarkups.FirstOrDefaultAsync(m => m.BrandId == BrandId);
            if (existing != null)
            {
                existing.MarkupPct = 0m;
                await _db.SaveChangesAsync();
            }
            MarkupEditor = 0;
        }

        public void ConfirmDisable() => _ = SaveMarkupAsync();

        // --- aliases ---
        public async Task AddAliasAsync()
        {
            var dlg = new AddAliasDialog { XamlRoot = _xr };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            var alias = new BrandAlias { Id = Guid.NewGuid(), BrandId = BrandId, Alias = dlg.AliasName, IsPrimary = false };
            await _db.BrandAliases.AddAsync(alias);
            await _db.SaveChangesAsync();
            Aliases.Add(alias);
        }

        public async Task EditAliasAsync()
        {
            if (SelectedAlias == null) return;
            var dlg = new EditBrandDialog(SelectedAlias.Alias) { XamlRoot = _xr };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

            SelectedAlias.Alias = dlg.BrandName;
            _db.BrandAliases.Update(SelectedAlias);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAliasAsync()
        {
            if (SelectedAlias == null || SelectedAlias.IsPrimary) return;

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
    }
}