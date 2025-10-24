using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Класс BrandMarkup отвечает за логику компонента BrandMarkup.
    /// </summary>
    [Table("brand_markups")]
    public class BrandMarkup
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство BrandId предоставляет доступ к данным BrandId.
        /// </summary>

        [Required]
        [Column("brand_id")]
        public Guid BrandId { get; set; }
        /// <summary>
        /// Свойство MarkupPct предоставляет доступ к данным MarkupPct.
        /// </summary>

        [Required]
        [Column("markup_pct", TypeName = "numeric(6,2)")]
        public decimal MarkupPct { get; set; }
        /// <summary>
        /// Свойство Note предоставляет доступ к данным Note.
        /// </summary>

        [Column("note")]
        public string? Note { get; set; }
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
        /// <summary>
        /// Свойство Brand предоставляет доступ к данным Brand.
        /// </summary>

        [ForeignKey(nameof(BrandId))]
        public virtual Brand Brand { get; set; } = default!;
    }
}