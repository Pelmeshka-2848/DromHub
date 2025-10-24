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
    /// Класс PriceMarkup отвечает за логику компонента PriceMarkup.
    /// </summary>
    [Table("price_markups")]
    public class PriceMarkup
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство MaxPrice предоставляет доступ к данным MaxPrice.
        /// </summary>

        [Required]
        [Column("max_price", TypeName = "numeric(12,2)")]
        public decimal MaxPrice { get; set; }
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
    }
}
