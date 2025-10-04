using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation; // <-- для TransitionInfo
using System;

namespace DromHub.Views
{
    public sealed partial class BrandShellPage : Page
    {
        public BrandShellViewModel ViewModel { get; }

        public BrandShellPage()
        {
            InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandShellViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BrandShellViewModel.Section))
                    NavigateToSection(ViewModel.Section);
            };

            SectionFrame.Navigated += (_, e) =>
            {
                if (e.Content is FrameworkElement fe && fe.DataContext == null)
                    fe.DataContext = ViewModel; // единый VM для подпейджей
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid id)
                await ViewModel.InitializeAsync(id, this.XamlRoot);

            // Экран по умолчанию — Overview, БЕЗ анимации
            if (SectionFrame.Content == null ||
                SectionFrame.CurrentSourcePageType != typeof(BrandOverviewPage))
            {
                SectionFrame.Navigate(
                    typeof(BrandOverviewPage),
                    ViewModel.BrandId,
                    new SuppressNavigationTransitionInfo());
            }
        }

        private void NavigateToSection(BrandDetailsSection section)
        {
            var pageType = section switch
            {
                BrandDetailsSection.Overview => typeof(BrandOverviewPage),
                BrandDetailsSection.Parts => typeof(BrandPartsPage),
                BrandDetailsSection.Settings => typeof(BrandSettingsPage),
                BrandDetailsSection.About => typeof(BrandAboutPage),
                BrandDetailsSection.Changes => typeof(BrandChangesPage),
                _ => typeof(BrandOverviewPage)
            };

            // Переключение разделов — всегда без анимации
            if (SectionFrame.CurrentSourcePageType != pageType)
            {
                SectionFrame.Navigate(
                    pageType,
                    ViewModel.BrandId,
                    new SuppressNavigationTransitionInfo());
            }
        }

        private void SectionButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is AppBarToggleButton b && b.Tag is string tag &&
                Enum.TryParse(tag, out BrandDetailsSection section))
            {
                ViewModel.Section = section;
            }
        }

        // ===== Навигация между брендами (слайд) =====
        private void GoPrev_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasPrev && ViewModel.PrevBrandId is Guid id)
            {
                Frame?.Navigate(
                    typeof(BrandShellPage),
                    id,
                    new SlideNavigationTransitionInfo
                    {
                        Effect = SlideNavigationTransitionEffect.FromLeft
                    });
            }
        }

        private void GoNext_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasNext && ViewModel.NextBrandId is Guid id)
            {
                Frame?.Navigate(
                    typeof(BrandShellPage),
                    id,
                    new SlideNavigationTransitionInfo
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
            }
        }
    }
}