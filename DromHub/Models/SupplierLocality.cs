using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("supplier_localities")]
    public class SupplierLocality
    {
        [Key]
        [Column("id")]
        public decimal Id { get; set; }

        [Required]
        [Column("code")]
        [MaxLength(50)]
        public string Code { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }

        [Column("delivery_days")]
        public int DeliveryDays { get; set; }

        // Навигационное свойство
        public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
    }
}
