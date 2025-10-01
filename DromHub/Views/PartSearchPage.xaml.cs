using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DromHub.Views
{
    public sealed partial class PartSearchPage : Page
    {
        public PartViewModel ViewModel { get; }

        public PartSearchPage()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<PartViewModel>();
            this.DataContext = ViewModel;
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = ViewModel.SearchPartsCommand.ExecuteAsync(null);
            }
        }

        private async void AddPart_Click(object sender, RoutedEventArgs e)
        {
            // Создаем новую VM для добавления запчасти
            var partVm = App.ServiceProvider.GetRequiredService<PartViewModel>();
            partVm.ResetPart();
            await partVm.LoadBrandsCommand.ExecuteAsync(null);

            var dialog = new AddPartDialog(partVm)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await partVm.SavePartCommand.ExecuteAsync(null);

                    // Обновляем список только если сохранение прошло успешно
                    await ViewModel.SearchPartsCommand.ExecuteAsync(null);
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

        private async void ViewPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                // Передаём ваш ApplicationDbContext (в вашем коде он называется ViewModel.Context)
                var dialog = new ViewPartDialog(part, ViewModel.Context)
                {
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }




        private async void EditPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                var logger = App.ServiceProvider.GetRequiredService<ILogger<PartViewModel>>();
                var partVm = new PartViewModel(ViewModel.Context, logger)
                {
                    Id = part.Id,
                    CatalogNumber = part.CatalogNumber,
                    Name = part.Name,
                    SelectedBrand = part.Brand
                };

                await partVm.LoadBrandsCommand.ExecuteAsync(null);

                var dialog = new EditPartDialog(partVm)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await partVm.SavePartCommand.ExecuteAsync(null);
                    await ViewModel.SearchPartsCommand.ExecuteAsync(null);
                }
            }
        }

        private async void DeletePart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                var dialog = new ContentDialog
                {
                    Title = "Удаление записи",
                    Content = $"Вы действительно хотите удалить {part.Name}?",
                    PrimaryButtonText = "Да",
                    CloseButtonText = "Нет",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var partToDelete = await ViewModel.Context.Parts
                        .FirstOrDefaultAsync(p => p.Id == part.Id);

                    if (partToDelete != null)
                    {
                        ViewModel.Context.Parts.Remove(partToDelete);
                        await ViewModel.Context.SaveChangesAsync();
                        await ViewModel.SearchPartsCommand.ExecuteAsync(null);
                    }
                }
            }
        }
    }
}