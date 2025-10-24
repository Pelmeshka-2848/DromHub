using System;
using System.ComponentModel;
using System.Linq;

namespace DromHub.Models
{
    /// <summary>
    /// Класс CartItem отвечает за логику компонента CartItem.
    /// </summary>
    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;
        private Part _part;
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>

        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// Свойство CartId предоставляет доступ к данным CartId.
        /// </summary>
        public Guid CartId { get; set; }
        /// <summary>
        /// Свойство PartId предоставляет доступ к данным PartId.
        /// </summary>
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
        /// <summary>
        /// Свойство AddedAt предоставляет доступ к данным AddedAt.
        /// </summary>

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
        /// <summary>
        /// Свойство UnitPrice предоставляет доступ к данным UnitPrice.
        /// </summary>

        public decimal? UnitPrice => Part?.LocalStocks?.FirstOrDefault()?.Price;
        /// <summary>
        /// Свойство TotalPrice предоставляет доступ к данным TotalPrice.
        /// </summary>

        public decimal TotalPrice => (UnitPrice ?? 0) * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Метод OnPropertyChanged выполняет основную операцию класса.
        /// </summary>

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}