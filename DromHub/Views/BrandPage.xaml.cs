using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using DromHub.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        public BrandPage()
        {
            InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            DataContext = ViewModel;
            Loaded += async (_, __) => await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
        }

        // Бренды
        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetBrand();
            var dialog = new AddBrandDialog(ViewModel) { XamlRoot = this.XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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
                await ViewModel.EditBrand(b);                  // уже реализовано в VM
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void DeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand is Brand)
            {
                // в VM DeleteBrandCommand уже показывает подтверждение
                await ViewModel.DeleteBrandCommand.ExecuteAsync(this.XamlRoot);
                await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
            }
        }

        // Синонимы
        private async void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand == null) return;

            var dialog = new AddAliasDialog { XamlRoot = this.XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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
    }
}