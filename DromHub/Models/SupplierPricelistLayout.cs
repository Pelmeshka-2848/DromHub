using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("supplier_pricelist_layouts")]
    public class SupplierPricelistLayout
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("supplier_id")]
        public Guid SupplierId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }

        [Required]
        [Column("file_type")]
        [MaxLength(50)]
        public string FileType { get; set; }

        [Column("file_mask")]
        [MaxLength(255)]
        public string FileMask { get; set; }

        [Column("columns_map")]
        public JsonDocument ColumnsMap { get; set; }

        [Column("options")]
        public JsonDocument Options { get; set; }

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
