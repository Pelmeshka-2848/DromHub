using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Класс LocalStock отвечает за логику компонента LocalStock.
    /// </summary>
    [Table("local_stock")]
    public class LocalStock
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство PartId предоставляет доступ к данным PartId.
        /// </summary>

        [Required]
        [Column("part_id")]
        public Guid PartId { get; set; }
        /// <summary>
        /// Свойство SupplierId предоставляет доступ к данным SupplierId.
        /// </summary>

        [Required]
        [Column("supplier_id")]
        public Guid SupplierId { get; set; }
        /// <summary>
        /// Свойство Quantity предоставляет доступ к данным Quantity.
        /// </summary>

        [Column("qty")]
        public int Quantity { get; set; }
        /// <summary>
        /// Свойство Multiplicity предоставляет доступ к данным Multiplicity.
        /// </summary>

        [Column("multiplicity")]
        public int Multiplicity { get; set; } = 1;
        /// <summary>
        /// Свойство PriceIn предоставляет доступ к данным PriceIn.
        /// </summary>

        [Column("price_in", TypeName = "numeric(12,2)")]
        public decimal PriceIn { get; set; }
        /// <summary>
        /// Свойство Price предоставляет доступ к данным Price.
        /// </summary>

        [NotMapped]
        public decimal Price => PriceIn;
        /// <summary>
        /// Свойство Note предоставляет доступ к данным Note.
        /// </summary>

        [Column("note")]
        public string Note { get; set; }
        /// <summary>
        /// Свойство CreatedAt предоставляет доступ к данным CreatedAt.
        /// </summary>

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство UpdatedAt предоставляет доступ к данным UpdatedAt.
        /// </summary>

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство Part предоставляет доступ к данным Part.
        /// </summary>

        [ForeignKey("PartId")]
        public virtual Part Part { get; set; }
        /// <summary>
        /// Свойство Supplier предоставляет доступ к данным Supplier.
        /// </summary>

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }
    }
}