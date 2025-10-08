using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Diagnostics;

namespace DromHub.Views
{
    public sealed partial class CartPage : Page
    {
        public CartViewModel ViewModel { get; }

        public CartPage()
        {
            this.InitializeComponent(); // Ёта строка должна быть

            // »спользуем статический экземпл€р
            ViewModel = CartViewModel.Instance;
            this.DataContext = ViewModel;

            Loaded += CartPage_Loaded;

            Debug.WriteLine("CartPage created with ViewModel instance");
        }

        private async void CartPage_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("=== CartPage Loaded ===");

            if (ViewModel != null)
            {
                await ViewModel.LoadCartAsync();
                UpdateEmptyCartVisibility();

                Debug.WriteLine($"CartPage loaded - Items: {ViewModel.CartItems.Count}");
                Debug.WriteLine($"Total: {ViewModel.TotalItems}, Price: {ViewModel.TotalPrice}");
            }

            Debug.WriteLine("=== CartPage Loaded Completed ===");
        }

        private async void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CartItem item)
            {
                Debug.WriteLine($"Removing item: {item.Part.CatalogNumber}");
                await ViewModel.RemoveFromCartAsync(item);
                UpdateEmptyCartVisibility();
            }
        }

        private void UpdateEmptyCartVisibility()
        {
            // EmptyCartPlaceholder должен быть определен в XAML
            if (EmptyCartPlaceholder != null)
            {
                bool hasItems = ViewModel?.CartItems?.Count > 0;
                EmptyCartPlaceholder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

                Debug.WriteLine($"Empty cart visibility: {EmptyCartPlaceholder.Visibility} (hasItems: {hasItems})");
            }
            else
            {
                Debug.WriteLine("ERROR: EmptyCartPlaceholder is null - check XAML");
            }
        }
    }
}