using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation; // <-- для TransitionInfo
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;

namespace DromHub.Views
{
    /// <summary>
    /// Класс BrandShellPage отвечает за логику компонента BrandShellPage.
    /// </summary>
    public sealed partial class BrandShellPage : Page
    {
        private const int BrandCacheLimit = 5;

        private readonly Dictionary<Guid, Dictionary<BrandDetailsSection, Page>> _brandSectionCache = new();
        private readonly Dictionary<Guid, BrandDetailsSection> _brandSectionSelection = new();
        private readonly LinkedList<Guid> _brandCacheOrder = new();

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

            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BrandShellViewModel.Section))
                    NavigateToSection(ViewModel.Section);
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
                    cache[section] = page;
                    _brandSectionSelection[ViewModel.BrandId] = section;
                    TouchBrandCache(ViewModel.BrandId);
                }
            };
        }
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
                _brandSectionCache[id] = new Dictionary<BrandDetailsSection, Page>();
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
                ViewModel.Section = targetSection;
            }
            else
            {
                NavigateToSection(targetSection);
            }
        }
        /// <summary>
        /// Метод NavigateToSection выполняет основную операцию класса.
        /// </summary>

        private void NavigateToSection(BrandDetailsSection section)
        {
            if (ViewModel.BrandId == Guid.Empty)
            {
                return;
            }

            if (_brandSectionCache.TryGetValue(ViewModel.BrandId, out var cache) &&
                cache.TryGetValue(section, out var cachedPage))
            {
                if (!ReferenceEquals(SectionFrame.Content, cachedPage))
                {
                    SectionFrame.Content = cachedPage;
                }

                _brandSectionSelection[ViewModel.BrandId] = section;
                TouchBrandCache(ViewModel.BrandId);
                return;
            }

            var pageType = MapSectionToPageType(section);

            // Переключение разделов — всегда без анимации
            if (SectionFrame.CurrentSourcePageType != pageType)
            {
                SectionFrame.Navigate(
                    pageType,
                    ViewModel.BrandId,
                    new SuppressNavigationTransitionInfo());
            }
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

        private Dictionary<BrandDetailsSection, Page> EnsureBrandCache(Guid brandId)
        {
            if (_brandSectionCache.TryGetValue(brandId, out var cache))
            {
                return cache;
            }

            EnsureBrandCapacity(brandId);
            cache = new Dictionary<BrandDetailsSection, Page>();
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
