using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("suppliers")]
    public class Supplier
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }

        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("price_source")]
        public string PriceSource { get; set; } = "disk";

        [Column("locality_id")]
        public decimal LocalityId { get; set; }

        // Навигационные свойства
        [ForeignKey("LocalityId")]
        public virtual SupplierLocality Locality { get; set; }
        public virtual ICollection<LocalStock> LocalStocks { get; set; } = new List<LocalStock>();
        public virtual SupplierMarkup Markup { get; set; }
        public virtual SupplierPricelistLayout PricelistLayout { get; set; }
    }
}
