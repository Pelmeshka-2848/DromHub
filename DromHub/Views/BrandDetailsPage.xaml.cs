using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;

namespace DromHub.Views
{
    public sealed partial class BrandDetailsPage : Page
    {
        public BrandDetailsViewModel ViewModel { get; }

        public BrandDetailsPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandDetailsViewModel>();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            SectionFrame.Navigated += (s, e) =>
            {
                if (e.Content is FrameworkElement fe)
                    fe.DataContext = ViewModel; // единый VM для подпейджей
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Guid id)
                await ViewModel.InitializeAsync(id, this.XamlRoot);

            NavigateToSection(ViewModel.Section);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Section))
                NavigateToSection(ViewModel.Section);
        }

        private void NavigateToSection(BrandDetailsSection section)
        {
            Type pageType = section switch
            {
                BrandDetailsSection.Overview => typeof(BrandDetailsOverviewPage),
                BrandDetailsSection.Parts => typeof(BrandDetailsPartsPage),
                BrandDetailsSection.Settings => typeof(BrandDetailsSettingsPage),
                BrandDetailsSection.About => typeof(BrandDetailsAboutPage),
                BrandDetailsSection.Changes => typeof(BrandDetailsChangesPage),
                _ => typeof(BrandDetailsOverviewPage)
            };

            if (SectionFrame.CurrentSourcePageType != pageType)
                SectionFrame.Navigate(pageType);
        }

        private void SectionButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is AppBarToggleButton b && b.Tag is string tag
                && Enum.TryParse<BrandDetailsSection>(tag, out var section))
            {
                ViewModel.Section = section;
            }
        }

        // Навигация между брендами
        private void GoPrev_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasPrev && ViewModel.PrevBrandId is Guid id)
                Frame?.Navigate(typeof(BrandDetailsPage), id);
        }
        private void GoNext_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasNext && ViewModel.NextBrandId is Guid id)
                Frame?.Navigate(typeof(BrandDetailsPage), id);
        }

        // Шапочные действия
        private async void OpenWebsite_Click(object sender, RoutedEventArgs e) => await ViewModel.OpenWebsiteAsync();
        private void CreatePart_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog
            {
                Title = "Создать запчасть",
                Content = "Откройте мастер создания.",
                CloseButtonText = "ОК",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
        private void OpenMergeWizard_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(BrandMergePage));
        }
    }
}