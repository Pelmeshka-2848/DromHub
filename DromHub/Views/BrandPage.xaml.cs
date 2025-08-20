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
            this.DataContext = ViewModel;
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
            var brandVM = App.ServiceProvider.GetRequiredService<BrandViewModel>();
            brandVM.ResetBrand();
            // await brandVM.LoadBrandsCommand.ExecuteAsync(null);

            var dialog = new AddBrandDialog(brandVM)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await brandVM.SaveBrandCommand.ExecuteAsync(null);

                    // Обновляем список только если сохранение прошло успешно
                    await ViewModel.LoadBrandsCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    // Показываем пользователю сообщение об ошибке
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