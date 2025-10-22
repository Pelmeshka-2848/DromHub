using DromHub.Models;
using DromHub.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DromHub.ViewModels
{
    /// <summary>
    /// Класс CartViewModel отвечает за логику компонента CartViewModel.
    /// </summary>
    public class CartViewModel : INotifyPropertyChanged
    {
        // Статический экземпляр для глобального доступа
        private static CartViewModel _instance;
        /// <summary>
        /// Свойство Instance предоставляет доступ к данным Instance.
        /// </summary>
        public static CartViewModel Instance => _instance ??= new CartViewModel();

        private readonly Cart _cart;

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Свойство CartItems предоставляет доступ к данным CartItems.
        /// </summary>

        public ObservableCollection<CartItem> CartItems { get; } = new ObservableCollection<CartItem>();

        public string PhoneNumber
        {
            get => _cart.PhoneNumber;
            set
            {
                _cart.PhoneNumber = value;
                OnPropertyChanged();
                PlaceOrderCommand?.RaiseCanExecuteChanged();
            }
        }
        /// <summary>
        /// Свойство TotalItems предоставляет доступ к данным TotalItems.
        /// </summary>

        public int TotalItems => _cart.TotalItems;
        /// <summary>
        /// Свойство TotalPrice предоставляет доступ к данным TotalPrice.
        /// </summary>
        public decimal TotalPrice => _cart.TotalPrice;
        /// <summary>
        /// Свойство ClearCartCommand предоставляет доступ к данным ClearCartCommand.
        /// </summary>

        public RelayCommand ClearCartCommand { get; }
        /// <summary>
        /// Свойство PlaceOrderCommand предоставляет доступ к данным PlaceOrderCommand.
        /// </summary>
        public RelayCommand PlaceOrderCommand { get; }

        // Приватный конструктор для singleton
        /// <summary>
        /// Конструктор CartViewModel инициализирует экземпляр класса.
        /// </summary>
        private CartViewModel()
        {
            _cart = new Cart();
            _cart.PropertyChanged += OnCartPropertyChanged;

            ClearCartCommand = new RelayCommand(ClearCart,
                () => CartItems.Count > 0);

            PlaceOrderCommand = new RelayCommand(PlaceOrder,
                () => !string.IsNullOrEmpty(PhoneNumber) && CartItems.Count > 0);
        }
        /// <summary>
        /// Метод LoadCartAsync выполняет основную операцию класса.
        /// </summary>

        public Task LoadCartAsync()
        {
            // Обновляем ObservableCollection из корзины в памяти
            CartItems.Clear();
            foreach (var item in _cart.Items)
            {
                CartItems.Add(item);
            }

            UpdateCommands();
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(PhoneNumber));

            System.Diagnostics.Debug.WriteLine($"=== LoadCartAsync ===");
            System.Diagnostics.Debug.WriteLine($"Cart items loaded: {CartItems.Count}");
            System.Diagnostics.Debug.WriteLine($"Total items: {TotalItems}, Total price: {TotalPrice}");

            return Task.CompletedTask;
        }
        /// <summary>
        /// Метод AddToCartAsync выполняет основную операцию класса.
        /// </summary>

        public Task AddToCartAsync(Part part, int quantity = 1)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CartViewModel.AddToCartAsync ===");
                System.Diagnostics.Debug.WriteLine($"Part: {part.CatalogNumber}, Quantity: {quantity}");

                _cart.AddItem(part, quantity);

                System.Diagnostics.Debug.WriteLine($"Cart updated - Items: {_cart.Items.Count}");
                System.Diagnostics.Debug.WriteLine($"=== CartViewModel.AddToCartAsync Completed ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in CartViewModel.AddToCartAsync: {ex.Message}");
                throw;
            }

            return Task.CompletedTask;
        }
        /// <summary>
        /// Метод RemoveFromCartAsync выполняет основную операцию класса.
        /// </summary>

        public Task RemoveFromCartAsync(CartItem item)
        {
            _cart.RemoveItem(item);
            return Task.CompletedTask;
        }
        /// <summary>
        /// Метод ClearCart выполняет основную операцию класса.
        /// </summary>

        private void ClearCart()
        {
            _cart.Clear();
        }
        /// <summary>
        /// Метод PlaceOrder выполняет основную операцию класса.
        /// </summary>

        private async void PlaceOrder()
        {
            try
            {
                if (!CartItems.Any())
                {
                    await ShowMessageAsync("Cart is empty.");
                    return;
                }

                if (string.IsNullOrEmpty(PhoneNumber))
                {
                    await ShowMessageAsync("Please enter your phone number.");
                    return;
                }

                // Здесь будет логика сохранения заказа в таблицу Orders
                var orderData = new
                {
                    PhoneNumber = PhoneNumber,
                    Items = CartItems.Select(item => new
                    {
                        PartId = item.PartId,
                        PartNumber = item.Part.CatalogNumber,
                        Quantity = item.Quantity,
                        Price = item.Part.LocalStocks?.FirstOrDefault()?.Price ?? 0
                    }).ToList(),
                    TotalPrice = TotalPrice,
                    CreatedAt = DateTime.UtcNow
                };

                System.Diagnostics.Debug.WriteLine($"Order created: {System.Text.Json.JsonSerializer.Serialize(orderData)}");

                // Очищаем корзину после создания заказа
                ClearCart();

                await ShowMessageAsync("Order placed successfully! Thank you for your purchase.");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error placing order: {ex.Message}");
            }
        }
        /// <summary>
        /// Метод OnCartPropertyChanged выполняет основную операцию класса.
        /// </summary>

        private void OnCartPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Cart property changed: {e.PropertyName}");

            // Когда корзина изменяется, обновляем ViewModel
            switch (e.PropertyName)
            {
                case nameof(Cart.Items):
                    CartItems.Clear();
                    foreach (var item in _cart.Items)
                    {
                        CartItems.Add(item);
                    }
                    UpdateCommands();
                    System.Diagnostics.Debug.WriteLine($"ObservableCollection updated: {CartItems.Count} items");
                    break;

                case nameof(Cart.TotalItems):
                    OnPropertyChanged(nameof(TotalItems));
                    System.Diagnostics.Debug.WriteLine($"TotalItems updated: {TotalItems}");
                    break;

                case nameof(Cart.TotalPrice):
                    OnPropertyChanged(nameof(TotalPrice));
                    System.Diagnostics.Debug.WriteLine($"TotalPrice updated: {TotalPrice}");
                    break;

                case nameof(Cart.PhoneNumber):
                    OnPropertyChanged(nameof(PhoneNumber));
                    break;
            }
        }
        /// <summary>
        /// Метод UpdateCommands выполняет основную операцию класса.
        /// </summary>

        private void UpdateCommands()
        {
            ClearCartCommand?.RaiseCanExecuteChanged();
            PlaceOrderCommand?.RaiseCanExecuteChanged();
            System.Diagnostics.Debug.WriteLine("Commands updated");
        }
        /// <summary>
        /// Метод ShowMessageAsync выполняет основную операцию класса.
        /// </summary>

        private async Task ShowMessageAsync(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Cart message: {message}");
            // Ваша реализация показа сообщения
        }
        /// <summary>
        /// Метод OnPropertyChanged выполняет основную операцию класса.
        /// </summary>

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}