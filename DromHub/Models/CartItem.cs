using System;
using System.ComponentModel;
using System.Linq;

namespace DromHub.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;
        private Part _part;

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
        public Part Part
        {
            get => _part;
            set
            {
                _part = value;
                OnPropertyChanged(nameof(Part));
                OnPropertyChanged(nameof(UnitPrice));
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        public decimal? UnitPrice => Part?.LocalStocks?.FirstOrDefault()?.Price;

        public decimal TotalPrice => (UnitPrice ?? 0) * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}