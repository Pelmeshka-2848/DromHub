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
    /// <summary>
    /// Класс SupplierPricelistLayout отвечает за логику компонента SupplierPricelistLayout.
    /// </summary>
    [Table("supplier_pricelist_layouts")]
    public class SupplierPricelistLayout
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство SupplierId предоставляет доступ к данным SupplierId.
        /// </summary>

        [Required]
        [Column("supplier_id")]
        public Guid SupplierId { get; set; }
        /// <summary>
        /// Свойство Name предоставляет доступ к данным Name.
        /// </summary>

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }
        /// <summary>
        /// Свойство FileType предоставляет доступ к данным FileType.
        /// </summary>

        [Required]
        [Column("file_type")]
        [MaxLength(50)]
        public string FileType { get; set; }
        /// <summary>
        /// Свойство FileMask предоставляет доступ к данным FileMask.
        /// </summary>

        [Column("file_mask")]
        [MaxLength(255)]
        public string FileMask { get; set; }
        /// <summary>
        /// Свойство ColumnsMap предоставляет доступ к данным ColumnsMap.
        /// </summary>

        [Column("columns_map")]
        public JsonDocument ColumnsMap { get; set; }
        /// <summary>
        /// Свойство Options предоставляет доступ к данным Options.
        /// </summary>

        [Column("options")]
        public JsonDocument Options { get; set; }
        /// <summary>
        /// Свойство Note предоставляет доступ к данным Note.
        /// </summary>

        [Column("note")]
        public string Note { get; set; }
        /// <summary>
        /// Свойство CreatedAt предоставляет доступ к данным CreatedAt.
        /// </summary>

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство UpdatedAt предоставляет доступ к данным UpdatedAt.
        /// </summary>

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Навигационное свойство
        /// <summary>
        /// Свойство Supplier предоставляет доступ к данным Supplier.
        /// </summary>
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }
}
