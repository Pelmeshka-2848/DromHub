using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        public BrandPage()
        {
            this.InitializeComponent();   // важно: this.
            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            DataContext = ViewModel;

            Loaded += async (_, __) =>
            {
                ViewModel.XamlRoot = this.XamlRoot;
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            };
        }

        // ===== БРЕНДЫ =====
        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetBrand();
            var dialog = new AddBrandDialog(ViewModel) { XamlRoot = this.XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveBrandCommand.ExecuteAsync(null);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void EditBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand is Brand b)
            {
                ViewModel.XamlRoot = this.XamlRoot;
                await ViewModel.EditBrand(b);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void DeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand != null)
            {
                await ViewModel.DeleteBrandCommand.ExecuteAsync(this.XamlRoot);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        // ===== СИНОНИМЫ =====
        private async void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand == null) return;

            var dialog = new AddAliasDialog { XamlRoot = this.XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.SaveAliasAsync(dialog.AliasName);
                await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
            }
        }

        private async void EditAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAlias == null) return;
            await ViewModel.EditAliasCommand.ExecuteAsync(this.XamlRoot);
            await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
        }

        private async void DeleteAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAlias == null) return;
            await ViewModel.DeleteAliasCommand.ExecuteAsync(this.XamlRoot);
            await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
        }

        // ===== НАЦЕНКА (пресеты) =====
        private void PresetMarkup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && double.TryParse(b.Tag?.ToString(), out var preset))
            {
                ViewModel.BrandMarkupPercent = preset;
                // Переключатель применения не трогаем
            }
        }
    }
}