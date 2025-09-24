using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Guid id)
                await ViewModel.InitializeAsync(id, this.XamlRoot);
        }

        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var v))
                ViewModel.MarkupEditor = v;
        }

        private void ConfirmDisable_Click(object sender, RoutedEventArgs e) => ViewModel.ConfirmDisable();

        // навигация по брендам
        private void PrevBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PrevBrandId is Guid id)
                Frame?.Navigate(typeof(BrandDetailsPage), id);
        }
        private void NextBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.NextBrandId is Guid id)
                Frame?.Navigate(typeof(BrandDetailsPage), id);
        }

        private void OpenBrandInList_Click(object sender, RoutedEventArgs e)
        {
            // Можно перейти на список — возвращаемся назад
            Frame?.GoBack();
        }

        private async void AddAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.AddAliasAsync();
        private async void EditAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.EditAliasAsync();
        private async void DeleteAlias_Click(object sender, RoutedEventArgs e) => await ViewModel.DeleteAliasAsync();

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

        private void OpenEditBrandProps_Click(object sender, RoutedEventArgs e)
        {
            _ = new ContentDialog
            {
                Title = "Свойства бренда",
                Content = "Редактор свойств будет реализован позже.",
                CloseButtonText = "ОК",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }
}