using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

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
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Guid id)
                await ViewModel.InitializeAsync(id, this.XamlRoot);
        }

        // --- prev / next ---
        private void PrevBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PrevBrandId.HasValue)
                Frame?.Navigate(typeof(BrandDetailsPage), ViewModel.PrevBrandId.Value);
        }
        private void NextBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.NextBrandId.HasValue)
                Frame?.Navigate(typeof(BrandDetailsPage), ViewModel.NextBrandId.Value);
        }

        // --- меню разделов ---
        private void Menu_Overview_Click(object sender, RoutedEventArgs e) => ViewModel.Section = BrandDetailsSection.Overview;
        private void Menu_Parts_Click(object sender, RoutedEventArgs e) => ViewModel.Section = BrandDetailsSection.Parts;
        private void Menu_Aliases_Click(object sender, RoutedEventArgs e) => ViewModel.Section = BrandDetailsSection.Aliases;
        private void Menu_About_Click(object sender, RoutedEventArgs e) => ViewModel.Section = BrandDetailsSection.About;
        private void Menu_Changes_Click(object sender, RoutedEventArgs e) => ViewModel.Section = BrandDetailsSection.Changes;

        // --- наценка ---
        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var v))
                ViewModel.MarkupEditor = v;
        }
        private void ConfirmDisable_Click(object sender, RoutedEventArgs e) => ViewModel.ConfirmDisable();
        private void CancelDisable_Click(object sender, RoutedEventArgs e) => ViewModel.CancelDisable();

        // --- алиасы ---
        private async void AddAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.AddAliasAsync();
        private async void EditAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.EditAliasAsync();
        private async void DeleteAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.DeleteAliasAsync();

        // --- разное ---
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

        private void SectionOverview_Click(object sender, RoutedEventArgs e)
            => ViewModel.Section = DromHub.ViewModels.BrandDetailsSection.Overview;

        private void SectionParts_Click(object sender, RoutedEventArgs e)
            => ViewModel.Section = DromHub.ViewModels.BrandDetailsSection.Parts;

        private void SectionAliases_Click(object sender, RoutedEventArgs e)
            => ViewModel.Section = DromHub.ViewModels.BrandDetailsSection.Aliases;

        private void SectionAbout_Click(object sender, RoutedEventArgs e)
            => ViewModel.Section = DromHub.ViewModels.BrandDetailsSection.About;

        private void SectionChanges_Click(object sender, RoutedEventArgs e)
            => ViewModel.Section = DromHub.ViewModels.BrandDetailsSection.Changes;

        private void GoPrev_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.HasPrev == true && ViewModel.PrevBrandId is Guid id)
            {
                Frame?.Navigate(typeof(BrandDetailsPage), id);
            }
        }

        private void GoNext_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.HasNext == true && ViewModel.NextBrandId is Guid id)
            {
                Frame?.Navigate(typeof(BrandDetailsPage), id);
            }
        }

        private void DisableInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        {
            ViewModel.CancelDisable();
        }
        private void OpenMergeWizard_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(BrandMergePage));
        }
    }
}