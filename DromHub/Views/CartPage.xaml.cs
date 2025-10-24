using DromHub.Models;
using DromHub.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Diagnostics;

namespace DromHub.Views
{
    /// <summary>
    /// Класс CartPage отвечает за логику компонента CartPage.
    /// </summary>
    public sealed partial class CartPage : Page
    {
        /// <summary>
        /// Свойство ViewModel предоставляет доступ к данным ViewModel.
        /// </summary>
        public CartViewModel ViewModel { get; }
        /// <summary>
        /// Конструктор CartPage инициализирует экземпляр класса.
        /// </summary>

        public CartPage()
        {
            this.InitializeComponent(); //    

            //   
            ViewModel = CartViewModel.Instance;
            this.DataContext = ViewModel;

            Loaded += CartPage_Loaded;

            Debug.WriteLine("CartPage created with ViewModel instance");
        }
        /// <summary>
        /// Метод CartPage_Loaded выполняет основную операцию класса.
        /// </summary>

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
        /// <summary>
        /// Метод RemoveItem_Click выполняет основную операцию класса.
        /// </summary>

        private async void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CartItem item)
            {
                Debug.WriteLine($"Removing item: {item.Part.CatalogNumber}");
                await ViewModel.RemoveFromCartAsync(item);
                UpdateEmptyCartVisibility();
            }
        }
        /// <summary>
        /// Метод UpdateEmptyCartVisibility выполняет основную операцию класса.
        /// </summary>

        private void UpdateEmptyCartVisibility()
        {
            // EmptyCartPlaceholder     XAML
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