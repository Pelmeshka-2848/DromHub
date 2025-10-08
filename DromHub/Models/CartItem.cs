using System;
using System.ComponentModel;
using System.Linq;

namespace DromHub.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CartId { get; set; }
        public Guid PartId { get; set; }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        public Part Part { get; set; }

        public decimal TotalPrice => (Part?.LocalStocks?.FirstOrDefault()?.Price ?? 0) * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}