using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    [Table("local_stock")]
    public class LocalStock
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("part_id")]
        public Guid PartId { get; set; }

        [Required]
        [Column("supplier_id")]
        public Guid SupplierId { get; set; }

        [Column("qty")]
        public int Quantity { get; set; }

        [Column("multiplicity")]
        public int Multiplicity { get; set; } = 1;

        [Column("price_in", TypeName = "numeric(12,2)")]
        public decimal PriceIn { get; set; }

        [NotMapped]
        public decimal Price => PriceIn;

        [Column("note")]
        public string Note { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PartId")]
        public virtual Part Part { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }
}