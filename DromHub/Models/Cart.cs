using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DromHub.Models
{
    public class Cart : INotifyPropertyChanged
    {
        private readonly List<CartItem> _items = new();
        private string _phoneNumber = string.Empty;

        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

        public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

        public int TotalItems => _items.Sum(item => item.Quantity);
        public decimal TotalPrice => _items.Sum(item => item.TotalPrice);

        public event PropertyChangedEventHandler PropertyChanged;

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

        public void RemoveItem(CartItem item)
        {
            _items.Remove(item);
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
        }

        public void Clear()
        {
            _items.Clear();
            PhoneNumber = string.Empty;
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(TotalPrice));
            OnPropertyChanged(nameof(PhoneNumber));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}