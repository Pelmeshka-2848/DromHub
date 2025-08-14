using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("supplier_markups")]
    public class SupplierMarkup
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("supplier_id")]
        public Guid SupplierId { get; set; }

        [Required]
        [Column("markup_pct", TypeName = "numeric(6,2)")]
        public decimal MarkupPct { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Навигационное свойство
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }
}
