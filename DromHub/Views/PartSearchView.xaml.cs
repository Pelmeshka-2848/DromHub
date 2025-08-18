using Microsoft.UI.Xaml.Controls;
using DromHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DromHub.Views
{
    public sealed partial class PartSearchView : Page
    {
        public PartSearchViewModel ViewModel { get; }

        public PartSearchView()
        {
            this.InitializeComponent();
            ViewModel = App.ServiceProvider.GetRequiredService<PartSearchViewModel>();
            this.DataContext = ViewModel;
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _ = ViewModel.SearchPartsCommand.ExecuteAsync(null);
            }
        }

        private async void NavigateToPartView(object sender, RoutedEventArgs e)
        {
            var partVm = new PartViewModel(ViewModel.Context);
            await partVm.LoadBrandsCommand.ExecuteAsync(null);

            var dialog = new AddPartDialog(partVm)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await partVm.SavePartCommand.ExecuteAsync(null);

                ViewModel.Parts.Add(new Part
                {
                    Id = partVm.Id,
                    CatalogNumber = partVm.CatalogNumber,
                    Name = partVm.Name,
                    BrandId = partVm.SelectedBrand?.Id ?? Guid.Empty,
                    Brand = partVm.SelectedBrand
                });
            }
        }

        private async void EditPart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Part part)
            {
                var partVm = new PartViewModel(ViewModel.Context, part)
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