using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation; // <-- для TransitionInfo
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandShellPage отвечает за логику компонента BrandShellPage.
    /// </summary>
    public sealed partial class BrandShellPage : Page
    {
        private const int BrandCacheLimit = 5;

        private sealed class SectionCacheEntry
        {
            public SectionCacheEntry(Page page, string navigationState)
            {
                Page = page;
                NavigationState = navigationState;
            }

            public Page Page { get; private set; }

            public string NavigationState { get; set; }

            public void Update(Page page, string navigationState)
            {
                Page = page;
                NavigationState = navigationState;
            }
        }

        private readonly Dictionary<Guid, Dictionary<BrandDetailsSection, SectionCacheEntry>> _brandSectionCache = new();
        private readonly Dictionary<Guid, BrandDetailsSection> _brandSectionSelection = new();
        private readonly LinkedList<Guid> _brandCacheOrder = new();
        private BrandDetailsSection _currentSection = BrandDetailsSection.Overview;
        private bool _suppressSectionChange;

        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public BrandShellViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор BrandShellPage инициализирует экземпляр класса.
        /// </summary>

        public BrandShellPage()
        {
            InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandShellViewModel>();
            DataContext = ViewModel;

            SectionFrame.CacheSize = BrandCacheLimit;

            ViewModel.PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(BrandShellViewModel.Section))
                {
                    if (_suppressSectionChange)
                    {
                        return;
                    }

                    var targetSection = ViewModel.Section;
                    var navigated = await NavigateToSectionAsync(targetSection);

                    if (!navigated && targetSection != _currentSection)
                    {
                        try
                        {
                            _suppressSectionChange = true;
                            ViewModel.Section = _currentSection;
                        }
                        finally
                        {
                            _suppressSectionChange = false;
                        }
                    }
                }
            };

            SectionFrame.Navigated += (_, e) =>
            {
                if (e.Content is FrameworkElement fe && fe.DataContext == null)
                    fe.DataContext = ViewModel; // единый VM для подпейджей

                if (e.Content is Page page &&
                    ViewModel.BrandId != Guid.Empty &&
                    TryMapPageTypeToSection(e.SourcePageType, out var section))
                {
                    var cache = EnsureBrandCache(ViewModel.BrandId);
                    var navigationState = SectionFrame.GetNavigationState();

                    if (cache.TryGetValue(section, out var entry))
                    {
                        entry.Update(page, navigationState);
                    }
                    else
                    {
                        cache[section] = new SectionCacheEntry(page, navigationState);
                    }

                    _brandSectionSelection[ViewModel.BrandId] = section;
                    TouchBrandCache(ViewModel.BrandId);
                    _currentSection = section;
                }
            };
        }

        /// <summary>
        /// Возвращает <c>true</c>, если текущая страница настроек содержит несохранённые изменения.
        /// </summary>
        public bool HasPendingBrandSettingsChanges =>
            SectionFrame.Content is BrandSettingsPage settingsPage &&
            settingsPage.ViewModel.HasChanges &&
            !settingsPage.ViewModel.SaveCommand.IsRunning;

        /// <summary>
        /// Пытается обработать несохранённые изменения на странице настроек.
        /// </summary>
        public Task<bool> TryHandleUnsavedBrandSettingsAsync() => EnsureCanLeaveSettingsAsync();
        /// <summary>
        /// Метод OnNavigatedTo выполняет основную операцию класса.
        /// </summary>

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is not Guid id)
            {
                return;
            }

            var hasCache = _brandSectionCache.ContainsKey(id);

            if (!hasCache)
            {
                EnsureBrandCapacity(id);
                _brandSectionCache[id] = new Dictionary<BrandDetailsSection, SectionCacheEntry>();
            }
            else
            {
                TouchBrandCache(id);
            }

            await ViewModel.InitializeAsync(id, this.XamlRoot);

            var targetSection = _brandSectionSelection.TryGetValue(id, out var storedSection)
                ? storedSection
                : BrandDetailsSection.Overview;

            if (!hasCache)
            {
                _brandSectionSelection[id] = targetSection;
            }

            if (ViewModel.Section != targetSection)
            {
                try
                {
                    _suppressSectionChange = true;
                    ViewModel.Section = targetSection;
                }
                finally
                {
                    _suppressSectionChange = false;
                }
            }

            await NavigateToSectionAsync(targetSection, force: true);
        }
        /// <summary>
        /// Метод NavigateToSection выполняет основную операцию класса.
        /// </summary>

        private async Task<bool> NavigateToSectionAsync(BrandDetailsSection section, bool force = false)
        {
            if (ViewModel.BrandId == Guid.Empty)
            {
                return false;
            }

            if (!force && section == _currentSection && SectionFrame.Content is not null)
            {
                return true;
            }

            if (!await EnsureCanLeaveCurrentSectionAsync(section, force))
            {
                return false;
            }

            if (_brandSectionCache.TryGetValue(ViewModel.BrandId, out var cache) &&
                cache.TryGetValue(section, out var cachedEntry) &&
                TryRestoreCachedSection(cachedEntry))
            {
                _brandSectionSelection[ViewModel.BrandId] = section;
                TouchBrandCache(ViewModel.BrandId);
                _currentSection = section;
                return true;
            }

            var pageType = MapSectionToPageType(section);

            // Переключение разделов — всегда без анимации
            if (force || SectionFrame.CurrentSourcePageType != pageType)
            {
                SectionFrame.Navigate(
                    pageType,
                    ViewModel.BrandId,
                    new SuppressNavigationTransitionInfo());
            }

            if (SectionFrame.CurrentSourcePageType == pageType)
            {
                _currentSection = section;
            }

            return true;
        }
        /// <summary>
        /// Метод SectionButton_Checked выполняет основную операцию класса.
        /// </summary>

        private void SectionButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is AppBarToggleButton b && b.Tag is string tag &&
                Enum.TryParse(tag, out BrandDetailsSection section))
            {
                ViewModel.Section = section;
            }
        }

        private async Task<bool> EnsureCanLeaveCurrentSectionAsync(BrandDetailsSection targetSection, bool force)
        {
            if (force)
            {
                return true;
            }

            if (_currentSection == targetSection)
            {
                return true;
            }

            if (_currentSection != BrandDetailsSection.Settings)
            {
                return true;
            }

            return await EnsureCanLeaveSettingsAsync();
        }

        private async Task<bool> EnsureCanLeaveSettingsAsync()
        {
            if (SectionFrame.Content is not BrandSettingsPage settingsPage)
            {
                return true;
            }

            var vm = settingsPage.ViewModel;

            if (!vm.HasChanges || vm.SaveCommand.IsRunning)
            {
                return true;
            }

            var dialog = new ContentDialog
            {
                Title = "Есть несохранённые изменения",
                Content = "Сохранить изменения перед выходом?",
                PrimaryButtonText = "Сохранить",
                SecondaryButtonText = "Не сохранять",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = settingsPage.XamlRoot ?? XamlRoot
            };

            var result = await dialog.ShowAsync();

            switch (result)
            {
                case ContentDialogResult.Primary:
                    if (vm.SaveCommand.CanExecute(null))
                    {
                        await vm.SaveCommand.ExecuteAsync(null);
                    }

                    return !vm.HasChanges;

                case ContentDialogResult.Secondary:
                    return true;

                default:
                    return false;
            }
        }

        // ===== Навигация между брендами (слайд) =====
        /// <summary>
        /// Метод NavigateToBrand выполняет основную операцию класса.
        /// </summary>
        private void NavigateToBrand(Guid id, SlideNavigationTransitionEffect effect)
        {
            Frame?.Navigate(
                typeof(BrandShellPage),
                id,
                new SlideNavigationTransitionInfo { Effect = effect });
        }
        /// <summary>
        /// Метод Prev_KeyboardAccelerator_Invoked выполняет основную операцию класса.
        /// </summary>

        private void Prev_KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (ViewModel.HasPrev && ViewModel.PrevBrandId is Guid id)
            {
                NavigateToBrand(id, SlideNavigationTransitionEffect.FromLeft);
                args.Handled = true;
            }
        }
        /// <summary>
        /// Метод Next_KeyboardAccelerator_Invoked выполняет основную операцию класса.
        /// </summary>

        private void Next_KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (ViewModel.HasNext && ViewModel.NextBrandId is Guid id)
            {
                NavigateToBrand(id, SlideNavigationTransitionEffect.FromRight);
                args.Handled = true;
            }
        }

        // Обновим кнопки, чтобы тоже использовали общий helper
        /// <summary>
        /// Метод GoPrev_Click выполняет основную операцию класса.
        /// </summary>
        private void GoPrev_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasPrev && ViewModel.PrevBrandId is Guid id)
                NavigateToBrand(id, SlideNavigationTransitionEffect.FromLeft);
        }
        /// <summary>
        /// Метод GoNext_Click выполняет основную операцию класса.
        /// </summary>

        private void GoNext_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasNext && ViewModel.NextBrandId is Guid id)
                NavigateToBrand(id, SlideNavigationTransitionEffect.FromRight);
        }

        private static Type MapSectionToPageType(BrandDetailsSection section) => section switch
        {
            BrandDetailsSection.Overview => typeof(BrandOverviewPage),
            BrandDetailsSection.Parts => typeof(BrandPartsPage),
            BrandDetailsSection.Settings => typeof(BrandSettingsPage),
            BrandDetailsSection.About => typeof(BrandAboutPage),
            BrandDetailsSection.Changes => typeof(BrandChangesPage),
            _ => typeof(BrandOverviewPage)
        };

        private static bool TryMapPageTypeToSection(Type? pageType, out BrandDetailsSection section)
        {
            if (pageType == typeof(BrandOverviewPage))
            {
                section = BrandDetailsSection.Overview;
                return true;
            }

            if (pageType == typeof(BrandPartsPage))
            {
                section = BrandDetailsSection.Parts;
                return true;
            }

            if (pageType == typeof(BrandSettingsPage))
            {
                section = BrandDetailsSection.Settings;
                return true;
            }

            if (pageType == typeof(BrandAboutPage))
            {
                section = BrandDetailsSection.About;
                return true;
            }

            if (pageType == typeof(BrandChangesPage))
            {
                section = BrandDetailsSection.Changes;
                return true;
            }

            section = default;
            return false;
        }

        private bool TryRestoreCachedSection(SectionCacheEntry entry)
        {
            if (ReferenceEquals(SectionFrame.Content, entry.Page))
            {
                return true;
            }

            if (string.IsNullOrEmpty(entry.NavigationState))
            {
                return false;
            }

            try
            {
                SectionFrame.SetNavigationState(entry.NavigationState);
                return true;
            }
            catch
            {
                entry.NavigationState = string.Empty;
                return false;
            }
        }

        private Dictionary<BrandDetailsSection, SectionCacheEntry> EnsureBrandCache(Guid brandId)
        {
            if (_brandSectionCache.TryGetValue(brandId, out var cache))
            {
                return cache;
            }

            EnsureBrandCapacity(brandId);
            cache = new Dictionary<BrandDetailsSection, SectionCacheEntry>();
            _brandSectionCache[brandId] = cache;
            return cache;
        }

        private void EnsureBrandCapacity(Guid brandId)
        {
            if (_brandSectionCache.ContainsKey(brandId))
            {
                return;
            }

            if (_brandSectionCache.Count >= BrandCacheLimit && _brandCacheOrder.First is not null)
            {
                var oldest = _brandCacheOrder.First.Value;
                _brandCacheOrder.RemoveFirst();
                _brandSectionCache.Remove(oldest);
                _brandSectionSelection.Remove(oldest);
            }

            _brandCacheOrder.AddLast(brandId);
        }

        private void TouchBrandCache(Guid brandId)
        {
            var node = _brandCacheOrder.Find(brandId);

            if (node is null)
            {
                _brandCacheOrder.AddLast(brandId);
                return;
            }

            _brandCacheOrder.Remove(node);
            _brandCacheOrder.AddLast(node);
        }
    }
}
