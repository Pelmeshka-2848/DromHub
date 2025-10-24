using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;

namespace DromHub.ViewModels
{
    /// <summary>
    /// ViewModel for editing brand settings such as basic information, aliases and markup.
    /// </summary>
    public partial class BrandSettingsViewModel : ObservableObject
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        private Guid? _markupId;
        private bool _suspendPrimaryAliasSync;
        private bool _suspendChangeTracking;

        private static readonly HashSet<string> _trackedPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(Name),
            nameof(IsOem),
            nameof(Website),
            nameof(YearFounded),
            nameof(Description),
            nameof(UserNotes),
            nameof(SelectedCountryId),
            nameof(MarkupPercent)
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="BrandSettingsViewModel"/> class.
        /// </summary>
        public BrandSettingsViewModel(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;

            Countries = new ObservableCollection<Country>();
            Aliases = new ObservableCollection<BrandAlias>();

            InitializeCommand = new AsyncRelayCommand<(Guid brandId, XamlRoot xr)>(args => InitializeAsync(args.brandId, args.xr));
            AddAliasCommand = new AsyncRelayCommand(AddAliasAsync, () => !IsBusy);
            RemoveAliasCommand = new AsyncRelayCommand<BrandAlias?>(RemoveAliasAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && HasChanges);

            PropertyChanged += OnViewModelPropertyChanged;
            Aliases.CollectionChanged += OnAliasesCollectionChanged;
        }

        /// <summary>
        /// Gets the command that initializes the view-model data.
        /// </summary>
        public IAsyncRelayCommand<(Guid brandId, XamlRoot xr)> InitializeCommand { get; }

        /// <summary>
        /// Gets the command that adds a new alias to the brand.
        /// </summary>
        public IAsyncRelayCommand AddAliasCommand { get; }

        /// <summary>
        /// Gets the command that removes an alias from the brand.
        /// </summary>
        public IAsyncRelayCommand<BrandAlias?> RemoveAliasCommand { get; }

        /// <summary>
        /// Gets the command that persists the current brand changes.
        /// </summary>
        public IAsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// Gets the loaded countries list.
        /// </summary>
        public ObservableCollection<Country> Countries { get; }

        /// <summary>
        /// Gets the loaded brand aliases.
        /// </summary>
        public ObservableCollection<BrandAlias> Aliases { get; }

        [ObservableProperty]
        private Guid brandId;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private bool isOem;

        [ObservableProperty]
        private string? website;

        [ObservableProperty]
        private int? yearFounded;

        [ObservableProperty]
        private string? description;

        [ObservableProperty]
        private string? userNotes;

        [ObservableProperty]
        private Guid? selectedCountryId;

        [ObservableProperty]
        private double? markupPercent;

        [ObservableProperty]
        private string? lastExpandedSection;

        [ObservableProperty]
        private string newAliasText = string.Empty;

        [ObservableProperty]
        private bool hasChanges;

        partial void OnIsBusyChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
            AddAliasCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasChangesChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnNameChanged(string value)
        {
            if (_suspendPrimaryAliasSync)
            {
                return;
            }

            SyncPrimaryAliasWithName(value);
        }

        private void SyncPrimaryAliasWithName(string value)
        {
            var trimmedName = (value ?? string.Empty).Trim();

            var primaryEntry = Aliases
                .Select((alias, index) => (alias, index))
                .FirstOrDefault(tuple => tuple.alias is not null && tuple.alias.IsPrimary);

            if (primaryEntry.alias is not null)
            {
                if (!string.Equals(primaryEntry.alias.Alias, trimmedName, StringComparison.Ordinal))
                {
                    var updatedAlias = new BrandAlias
                    {
                        Id = primaryEntry.alias.Id,
                        BrandId = primaryEntry.alias.BrandId,
                        Alias = trimmedName,
                        IsPrimary = true,
                        Note = primaryEntry.alias.Note
                    };

                    Aliases[primaryEntry.index] = updatedAlias;
                }
            }
            else if (!string.IsNullOrWhiteSpace(trimmedName))
            {
                var existingEntry = Aliases
                    .Select((alias, index) => (alias, index))
                    .FirstOrDefault(tuple => string.Equals(tuple.alias?.Alias, trimmedName, StringComparison.OrdinalIgnoreCase));

                if (existingEntry.alias is not null)
                {
                    if (!existingEntry.alias.IsPrimary || !string.Equals(existingEntry.alias.Alias, trimmedName, StringComparison.Ordinal))
                    {
                        var promotedAlias = new BrandAlias
                        {
                            Id = existingEntry.alias.Id,
                            BrandId = existingEntry.alias.BrandId == Guid.Empty ? BrandId : existingEntry.alias.BrandId,
                            Alias = trimmedName,
                            IsPrimary = true,
                            Note = existingEntry.alias.Note
                        };

                        Aliases[existingEntry.index] = promotedAlias;
                    }

                    return;
                }

                Aliases.Insert(0, new BrandAlias
                {
                    Id = Guid.Empty,
                    BrandId = BrandId,
                    Alias = trimmedName,
                    IsPrimary = true
                });
            }
        }

        /// <summary>
        /// Loads the brand data for editing.
        /// </summary>
        public async Task InitializeAsync(Guid brandId, XamlRoot xr)
        {
            var brandChanged = BrandId != brandId;

            BrandId = brandId;
            if (brandChanged)
            {
                LastExpandedSection = null;
            }
            _ = xr;

            IsBusy = true;
            _suspendChangeTracking = true;
            try
            {
                Countries.Clear();
                Aliases.Clear();

                await using var db = await _dbFactory.CreateDbContextAsync();

                var countries = await db.Countries
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                foreach (var country in countries)
                {
                    Countries.Add(country);
                }

                var brand = await db.Brands
                    .Include(b => b.Markup)
                    .Include(b => b.Aliases)
                    .FirstOrDefaultAsync(b => b.Id == brandId);

                if (brand is null)
                {
                    ClearBrandFields();
                    return;
                }

                _suspendPrimaryAliasSync = true;

                Name = brand.Name;
                IsOem = brand.IsOem;
                Website = brand.Website;
                YearFounded = brand.YearFounded;
                Description = brand.Description;
                UserNotes = brand.UserNotes;
                SelectedCountryId = brand.CountryId;
                MarkupPercent = brand.Markup?.MarkupPct is decimal markup
                    ? (double)markup
                    : null;

                _markupId = brand.Markup?.Id;

                if (brand.Aliases is not null)
                {
                    foreach (var alias in brand.Aliases.OrderBy(a => a.Alias, StringComparer.OrdinalIgnoreCase))
                    {
                        Aliases.Add(new BrandAlias
                        {
                            Id = alias.Id,
                            BrandId = alias.BrandId,
                            Alias = alias.Alias,
                            IsPrimary = alias.IsPrimary,
                            Note = alias.Note
                        });
                    }
                }

                _suspendPrimaryAliasSync = false;

                SyncPrimaryAliasWithName(Name);
            }
            finally
            {
                _suspendChangeTracking = false;
                HasChanges = false;
                IsBusy = false;
            }
        }

        private void ClearBrandFields()
        {
            Name = string.Empty;
            IsOem = false;
            Website = null;
            YearFounded = null;
            Description = null;
            UserNotes = null;
            SelectedCountryId = null;
            MarkupPercent = null;
            _markupId = null;
            NewAliasText = string.Empty;
            Aliases.Clear();
        }

        private Task AddAliasAsync()
        {
            var aliasText = (NewAliasText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(aliasText))
            {
                return Task.CompletedTask;
            }

            var exists = Aliases.Any(a => string.Equals(a.Alias, aliasText, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                NewAliasText = string.Empty;
                return Task.CompletedTask;
            }

            Aliases.Add(new BrandAlias
            {
                Id = Guid.NewGuid(),
                BrandId = BrandId,
                Alias = aliasText,
                IsPrimary = false
            });

            NewAliasText = string.Empty;
            return Task.CompletedTask;
        }

        private Task RemoveAliasAsync(BrandAlias? alias)
        {
            if (alias is not null && Aliases.Contains(alias))
            {
                Aliases.Remove(alias);
            }

            return Task.CompletedTask;
        }

        private async Task SaveAsync()
        {
            if (BrandId == Guid.Empty)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();

                var brand = await db.Brands
                    .Include(b => b.Markup)
                    .Include(b => b.Aliases)
                    .FirstOrDefaultAsync(b => b.Id == BrandId);

                if (brand is null)
                {
                    return;
                }

                var trimmedName = (Name ?? string.Empty).Trim();
                brand.Name = trimmedName;
                brand.IsOem = IsOem;
                brand.Website = string.IsNullOrWhiteSpace(Website)
                    ? string.Empty
                    : Website!.Trim();
                brand.YearFounded = YearFounded;
                brand.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
                brand.UserNotes = string.IsNullOrWhiteSpace(UserNotes) ? null : UserNotes.Trim();
                brand.CountryId = SelectedCountryId;

                var primaryAlias = Aliases.FirstOrDefault(a => a.IsPrimary);
                if (primaryAlias is not null)
                {
                    primaryAlias.Alias = trimmedName;
                }

                var markupValue = MarkupPercent.HasValue ? (decimal)Math.Round(MarkupPercent.Value, 2) : (decimal?)null;
                if (markupValue.HasValue)
                {
                    if (brand.Markup is null)
                    {
                        var newMarkup = new BrandMarkup
                        {
                            Id = _markupId ?? Guid.NewGuid(),
                            BrandId = brand.Id,
                            MarkupPct = markupValue.Value
                        };

                        brand.Markup = newMarkup;
                        db.BrandMarkups.Add(newMarkup);
                        _markupId = newMarkup.Id;
                    }
                    else
                    {
                        brand.Markup.MarkupPct = markupValue.Value;
                        _markupId = brand.Markup.Id;
                    }
                }
                else if (brand.Markup is not null)
                {
                    db.BrandMarkups.Remove(brand.Markup);
                    brand.Markup = null;
                    _markupId = null;
                }

                var existingAliases = brand.Aliases?.ToDictionary(a => a.Id) ?? new();

                var desiredAliasIds = Aliases
                    .Where(a => !string.IsNullOrWhiteSpace(a.Alias))
                    .Select(a => a.Id)
                    .ToHashSet();

                foreach (var alias in existingAliases.Values)
                {
                    if (!desiredAliasIds.Contains(alias.Id))
                    {
                        db.BrandAliases.Remove(alias);
                    }
                }

                foreach (var alias in Aliases)
                {
                    var aliasText = (alias.Alias ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(aliasText))
                    {
                        continue;
                    }

                    if (existingAliases.TryGetValue(alias.Id, out var existingAlias))
                    {
                        existingAlias.Alias = aliasText;
                        existingAlias.IsPrimary = alias.IsPrimary;
                        existingAlias.Note = alias.Note;
                    }
                    else
                    {
                        var aliasId = alias.Id == Guid.Empty ? Guid.NewGuid() : alias.Id;
                        db.BrandAliases.Add(new BrandAlias
                        {
                            Id = aliasId,
                            BrandId = brand.Id,
                            Alias = aliasText,
                            IsPrimary = alias.IsPrimary,
                            Note = alias.Note
                        });

                        alias.Id = aliasId;
                    }
                }

                await db.SaveChangesAsync();

                HasChanges = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suspendChangeTracking)
            {
                return;
            }

            if (e.PropertyName is null)
            {
                HasChanges = true;
                return;
            }

            if (_trackedPropertyNames.Contains(e.PropertyName))
            {
                HasChanges = true;
            }
        }

        private void OnAliasesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suspendChangeTracking)
            {
                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Move)
            {
                HasChanges = true;
            }
        }
    }
}
