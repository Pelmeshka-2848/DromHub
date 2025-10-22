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
    /// Класс OemCross отвечает за логику компонента OemCross.
    /// </summary>
    [Table("oem_crosses")]
    public class OemCross
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство OemPartId предоставляет доступ к данным OemPartId.
        /// </summary>

        [Required]
        [Column("oem_part_id")]
        public Guid OemPartId { get; set; }
        /// <summary>
        /// Свойство AftermarketPartId предоставляет доступ к данным AftermarketPartId.
        /// </summary>

        [Required]
        [Column("aftermarket_part_id")]
        public Guid AftermarketPartId { get; set; }
        /// <summary>
        /// Свойство Source предоставляет доступ к данным Source.
        /// </summary>

        [Column("source")]
        public string Source { get; set; }
        /// <summary>
        /// Свойство Note предоставляет доступ к данным Note.
        /// </summary>

        [Column("note")]
        public string? Note { get; set; }
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
        /// Свойство OemPart предоставляет доступ к данным OemPart.
        /// </summary>

        [ForeignKey("OemPartId")]
        public virtual Part OemPart { get; set; }
        /// <summary>
        /// Свойство AftermarketPart предоставляет доступ к данным AftermarketPart.
        /// </summary>

        [ForeignKey("AftermarketPartId")]
        public virtual Part AftermarketPart { get; set; }
    }
}
