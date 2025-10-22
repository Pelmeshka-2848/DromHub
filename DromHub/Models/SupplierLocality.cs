using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    /// <summary>
    /// Класс SupplierLocality отвечает за логику компонента SupplierLocality.
    /// </summary>
    [Table("supplier_localities")]
    public class SupplierLocality
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public decimal Id { get; set; }
        /// <summary>
        /// Свойство Code предоставляет доступ к данным Code.
        /// </summary>

        [Required]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; }
        /// <summary>
        /// Свойство Name предоставляет доступ к данным Name.
        /// </summary>

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }
        /// <summary>
        /// Свойство DeliveryDays предоставляет доступ к данным DeliveryDays.
        /// </summary>

        [Column("delivery_days")]
        public int DeliveryDays { get; set; }

        // Навигационное свойство
        /// <summary>
        /// Свойство Suppliers предоставляет доступ к данным Suppliers.
        /// </summary>
        public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
    }
}
