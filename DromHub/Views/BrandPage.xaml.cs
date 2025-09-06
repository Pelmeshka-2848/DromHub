using System;
using System.Threading.Tasks;
using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DromHub.Views
{
    public sealed partial class BrandPage : Page
    {
        public BrandViewModel ViewModel { get; }

        public BrandPage()
        {
            this.InitializeComponent();

            ViewModel = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            ViewModel.XamlRoot = this.XamlRoot;

            this.DataContext = ViewModel;

            // Ìîæíî çàãðóçèòü ñðàçó
            _ = ViewModel.LoadBrandsCommand.ExecuteAsync(null);

            // Èëè ÷åðåç ñîáûòèå Loaded (åñëè õîòèòå)
            Loaded += async (_, __) => await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = ViewModel.SearchBrandsCommand.ExecuteAsync(null);
            }
        }

        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetBrand();

            var dialog = new AddBrandDialog(ViewModel)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await ViewModel.SaveBrandCommand.ExecuteAsync(null);
                    await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Îøèáêà",
                        Content = ex.Message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedBrand == null) return;

            var dialog = new AddAliasDialog
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await ViewModel.SaveAliasAsync(dialog.AliasName);
                    await ViewModel.LoadAliasesCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Ошибка",
                        Content = ex.Message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }

}