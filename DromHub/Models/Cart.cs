using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DromHub.Models
{
    /// <summary>
    /// Класс Cart отвечает за логику компонента Cart.
    /// </summary>
    public class Cart : INotifyPropertyChanged
    {
        private readonly List<CartItem> _items = new();
        private string _phoneNumber = string.Empty;
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>

        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// Свойство CreatedAt предоставляет доступ к данным CreatedAt.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство Status предоставляет доступ к данным Status.
        /// </summary>
        public string Status { get; set; } = "active";

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                _phoneNumber = value;
                OnPropertyChanged(nameof(PhoneNumber));
            }
        }
        /// <summary>
        /// Свойство Items предоставляет доступ к данным Items.
        /// </summary>

        public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
        /// <summary>
        /// Свойство TotalItems предоставляет доступ к данным TotalItems.
        /// </summary>

        public int TotalItems => _items.Sum(item => item.Quantity);
        /// <summary>
        /// Свойство TotalPrice предоставляет доступ к данным TotalPrice.
        /// </summary>
        public decimal TotalPrice => _items.Sum(item => item.TotalPrice);

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Метод AddItem выполняет основную операцию класса.
        /// </summary>

        public void AddItem(Part part, int quantity = 1)
        {
            var existingItem = _items.FirstOrDefault(item => item.PartId == part.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var newItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = this.Id,
                    PartId = part.Id,
                    Part = part,
                    Quantity = quantity,
                    AddedAt = DateTime.UtcNow
                };
                _items.Add(newItem);
            }
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
        }
        /// <summary>
        /// Метод RemoveItem выполняет основную операцию класса.
        /// </summary>

        public void RemoveItem(CartItem item)
        {
            _items.Remove(item);
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
        }
        /// <summary>
        /// Метод Clear выполняет основную операцию класса.
        /// </summary>

        public void Clear()
        {
            _items.Clear();
            PhoneNumber = string.Empty;
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(PhoneNumber));
        }
        /// <summary>
        /// Метод OnPropertyChanged выполняет основную операцию класса.
        /// </summary>

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}