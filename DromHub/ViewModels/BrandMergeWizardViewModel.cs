using CommunityToolkit.Mvvm.Input;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс BrandMergeWizardViewModel отвечает за логику компонента BrandMergeWizardViewModel.
    /// </summary>
    public class BrandMergeWizardViewModel
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly ILogger<BrandMergeWizardViewModel> _log;
        /// <summary>
        /// Свойство XamlRoot предоставляет доступ к данным XamlRoot.
        /// </summary>

        public XamlRoot XamlRoot { get; set; }
        /// <summary>
        /// Свойство AllBrands предоставляет доступ к данным AllBrands.
        /// </summary>

        public ObservableCollection<Brand> AllBrands { get; } = new();
        /// <summary>
        /// Свойство FilteredSources предоставляет доступ к данным FilteredSources.
        /// </summary>
        public ObservableCollection<Brand> FilteredSources { get; } = new();
        /// <summary>
        /// Свойство FilteredTargets предоставляет доступ к данным FilteredTargets.
        /// </summary>
        public ObservableCollection<Brand> FilteredTargets { get; } = new();
        /// <summary>
        /// Свойство SelectedSources предоставляет доступ к данным SelectedSources.
        /// </summary>

        public ObservableCollection<Brand> SelectedSources { get; } = new();
        private Brand _selectedTarget;
        public Brand SelectedTarget
        {
            get => _selectedTarget;
            set { _selectedTarget = value; RecalcSummary(); }
        }
        /// <summary>
        /// Свойство SearchSources предоставляет доступ к данным SearchSources.
        /// </summary>

        public string SearchSources { get; set; } = string.Empty;
        /// <summary>
        /// Свойство SearchTarget предоставляет доступ к данным SearchTarget.
        /// </summary>
        public string SearchTarget { get; set; } = string.Empty;

        // summary
        /// <summary>
        /// Свойство SummaryText предоставляет доступ к данным SummaryText.
        /// </summary>
        public string SummaryText { get; private set; } = "—";
        /// <summary>
        /// Свойство AliasesConflictsText предоставляет доступ к данным AliasesConflictsText.
        /// </summary>
        public string AliasesConflictsText { get; private set; } = "—";
        /// <summary>
        /// Свойство MarkupSummaryText предоставляет доступ к данным MarkupSummaryText.
        /// </summary>
        public string MarkupSummaryText { get; private set; } = "—";
        /// <summary>
        /// Свойство CanMerge предоставляет доступ к данным CanMerge.
        /// </summary>

        public bool CanMerge => SelectedSources.Count > 0 && SelectedTarget != null;
        /// <summary>
        /// Свойство MergeCommand предоставляет доступ к данным MergeCommand.
        /// </summary>

        public IAsyncRelayCommand MergeCommand { get; }
        /// <summary>
        /// Конструктор BrandMergeWizardViewModel инициализирует экземпляр класса.
        /// </summary>

        public BrandMergeWizardViewModel(IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<BrandMergeWizardViewModel> log)
        {
            _dbFactory = dbFactory;
            _log = log;
            MergeCommand = new AsyncRelayCommand(MergeAsync, () => CanMerge);
            SelectedSources.CollectionChanged += (_, __) =>
            {
                RecalcSummary();
                (MergeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            };
        }
        /// <summary>
        /// Метод LoadAsync выполняет основную операцию класса.
        /// </summary>

        public async Task LoadAsync()
        {
            AllBrands.Clear();
            await using var db = await _dbFactory.CreateDbContextAsync();

            var items = await db.Brands
                .Select(b => new Brand
                {
                    Id = b.Id,
                    Name = b.Name,
                    IsOem = b.IsOem,
                    PartsCount = db.Parts.Count(p => p.BrandId == b.Id),
                    AliasesCount = db.BrandAliases.Count(a => a.BrandId == b.Id),
                    MarkupPercent = db.BrandMarkups
                        .Where(m => m.BrandId == b.Id)
                        .Select(m => (decimal?)m.MarkupPct)
                        .FirstOrDefault()
                })
                .OrderBy(b => b.Name)
                .AsNoTracking()
                .ToListAsync();

            foreach (var b in items) AllBrands.Add(b);

            ApplySourcesFilter();
            ApplyTargetFilter();
        }
        /// <summary>
        /// Метод ApplySourcesFilter выполняет основную операцию класса.
        /// </summary>

        public void ApplySourcesFilter()
        {
            var q = AllBrands.AsEnumerable();
            var s = (SearchSources ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(s))
                q = q.Where(b => b.Name.Contains(s, StringComparison.OrdinalIgnoreCase));

            FilteredSources.Clear();
            foreach (var b in q) FilteredSources.Add(b);
        }
        /// <summary>
        /// Метод ApplyTargetFilter выполняет основную операцию класса.
        /// </summary>

        public void ApplyTargetFilter()
        {
            var q = AllBrands.AsEnumerable();
            var s = (SearchTarget ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(s))
                q = q.Where(b => b.Name.Contains(s, StringComparison.OrdinalIgnoreCase));

            // не показываем в целевых уже выбранные источники
            var sourceIds = SelectedSources.Select(x => x.Id).ToHashSet();
            q = q.Where(b => !sourceIds.Contains(b.Id));

            FilteredTargets.Clear();
            foreach (var b in q) FilteredTargets.Add(b);
        }
        /// <summary>
        /// Метод AddSource выполняет основную операцию класса.
        /// </summary>

        public void AddSource(Guid id)
        {
            if (SelectedTarget != null && SelectedTarget.Id == id) return;
            if (SelectedSources.Any(x => x.Id == id)) return;

            var b = AllBrands.FirstOrDefault(x => x.Id == id);
            if (b != null)
            {
                SelectedSources.Add(b);
                ApplyTargetFilter();
            }
        }
        /// <summary>
        /// Метод RemoveSource выполняет основную операцию класса.
        /// </summary>

        public void RemoveSource(Guid id)
        {
            var existing = SelectedSources.FirstOrDefault(x => x.Id == id);
            if (existing != null)
            {
                SelectedSources.Remove(existing);
                ApplyTargetFilter();
            }
        }
        /// <summary>
        /// Метод SetTarget выполняет основную операцию класса.
        /// </summary>

        public void SetTarget(Guid id)
        {
            if (SelectedSources.Any(x => x.Id == id)) return; // целевой не должен быть источником
            SelectedTarget = AllBrands.FirstOrDefault(x => x.Id == id);
            (MergeCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }
        /// <summary>
        /// Метод RecalcSummary выполняет основную операцию класса.
        /// </summary>

        private async void RecalcSummary()
        {
            if (SelectedSources.Count == 0 || SelectedTarget == null)
            {
                SummaryText = "—";
                AliasesConflictsText = "—";
                MarkupSummaryText = "—";
                OnSummaryChanged();
                return;
            }

            try
            {
                var sourceIds = SelectedSources.Select(s => s.Id).ToList();
                await using var db = await _dbFactory.CreateDbContextAsync();

                var parts = await db.Parts.Where(p => sourceIds.Contains(p.BrandId)).CountAsync();

                // алиасы источников (кроме primary), потенциальные дубликаты с целевым
                var targetAliases = await db.BrandAliases
                    .Where(a => a.BrandId == SelectedTarget.Id)
                    .Select(a => a.Alias.ToLower())
                    .ToListAsync();

                var sourceAliases = await db.BrandAliases
                    .Where(a => sourceIds.Contains(a.BrandId) && !a.IsPrimary)
                    .Select(a => a.Alias.ToLower())
                    .ToListAsync();

                var dup = sourceAliases.Intersect(targetAliases).Count();
                var toMove = sourceAliases.Count - dup;

                SummaryText = $"Будет перенесено: деталей — {parts}, синонимов — {toMove}.";
                AliasesConflictsText = dup > 0 ? $"Конфликты по синонимам: {dup} (будут пропущены)." : "Конфликтов по синонимам нет.";

                // Наценка: если у целевого нет, но у какого-то источника есть — сообщим
                var targetMarkup = await db.BrandMarkups.Where(m => m.BrandId == SelectedTarget.Id)
                                                         .Select(m => (decimal?)m.MarkupPct)
                                                         .FirstOrDefaultAsync();

                var anySourceMarkup = await db.BrandMarkups.Where(m => sourceIds.Contains(m.BrandId))
                                                            .Select(m => (decimal?)m.MarkupPct)
                                                            .FirstOrDefaultAsync();

                if (targetMarkup.HasValue)
                    MarkupSummaryText = $"У целевого бренда наценка уже задана: {targetMarkup.Value:0.##}%.";
                else if (anySourceMarkup.HasValue)
                    MarkupSummaryText = $"У целевого наценка не задана. Будет перенесена наценка из первого источника: {anySourceMarkup.Value:0.##}%.";
                else
                    MarkupSummaryText = "Наценки у целевого и источников не задано.";

                OnSummaryChanged();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "RecalcSummary failed");
                SummaryText = "Не удалось рассчитать сводку.";
                AliasesConflictsText = "—";
                MarkupSummaryText = "—";
                OnSummaryChanged();
            }
        }
        /// <summary>
        /// Метод OnSummaryChanged выполняет основную операцию класса.
        /// </summary>

        private void OnSummaryChanged()
        {
            // упростим: дернём обновление через «пересоздание» свойств
            // (если используете ObservableObject — замените на SetProperty/OnPropertyChanged)
        }
        /// <summary>
        /// Метод MergeAsync выполняет основную операцию класса.
        /// </summary>

        private async Task MergeAsync()
        {
            if (!CanMerge) return;

            // подтверждение
            var cd = new ContentDialog
            {
                Title = "Подтвердите слияние",
                Content = $"Источник(и): {string.Join(", ", SelectedSources.Select(s => s.Name))}\n" +
                          $"Целевой: {SelectedTarget?.Name}\n\n" +
                          "Все детали и синонимы будут перенесены. Источники будут удалены.",
                PrimaryButtonText = "Объединить",
                CloseButtonText = "Отмена",
                XamlRoot = XamlRoot
            };
            if (await cd.ShowAsync() != ContentDialogResult.Primary) return;

            await using var db = await _dbFactory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var targetId = SelectedTarget.Id;
                var sourceIds = SelectedSources.Select(s => s.Id).ToList();

                // 1) Перенос запчастей
                await db.Parts.Where(p => sourceIds.Contains(p.BrandId))
                               .ExecuteUpdateAsync(s => s.SetProperty(p => p.BrandId, targetId));

                // 2) Перенос алиасов (кроме primary). Дубликаты — пропускаем
                var targetAliases = await db.BrandAliases
                    .Where(a => a.BrandId == targetId)
                    .Select(a => a.Alias.ToLower())
                    .ToListAsync();
                var targetAliasSet = new HashSet<string>(targetAliases);

                var sourceAliases = await db.BrandAliases
                    .Where(a => sourceIds.Contains(a.BrandId) && !a.IsPrimary)
                    .ToListAsync();

                foreach (var a in sourceAliases)
                {
                    if (!targetAliasSet.Contains(a.Alias.ToLower()))
                    {
                        db.BrandAliases.Add(new BrandAlias
                        {
                            Id = Guid.NewGuid(),
                            BrandId = targetId,
                            Alias = a.Alias,
                            IsPrimary = false,
                            Note = $"migrated from {a.BrandId}"
                        });
                        targetAliasSet.Add(a.Alias.ToLower());
                    }
                }

                // 3) Наценка: если у целевого нет — возьмём первую попавшуюся из источников
                var targetMarkup = await db.BrandMarkups.FirstOrDefaultAsync(m => m.BrandId == targetId);
                if (targetMarkup == null)
                {
                    var srcMarkup = await db.BrandMarkups
                        .Where(m => sourceIds.Contains(m.BrandId))
                        .OrderByDescending(m => m.UpdatedAt)
                        .FirstOrDefaultAsync();

                    if (srcMarkup != null)
                    {
                        db.BrandMarkups.Add(new BrandMarkup
                        {
                            Id = Guid.NewGuid(),
                            BrandId = targetId,
                            MarkupPct = srcMarkup.MarkupPct
                        });
                    }
                }

                await db.SaveChangesAsync();

                // 4) Удаляем бренды-источники (их primary-алиасы удалятся каскадом)
                var toDelete = await db.Brands.Where(b => sourceIds.Contains(b.Id)).ToListAsync();
                db.Brands.RemoveRange(toDelete);

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                await new ContentDialog
                {
                    Title = "Готово",
                    Content = "Слияние завершено.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();

                // Обновим списки
                SelectedSources.Clear();
                SelectedTarget = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _log.LogError(ex, "Merge failed");
                await new ContentDialog
                {
                    Title = "Ошибка",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
        }
    }
}