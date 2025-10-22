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
    /// Класс PartImage отвечает за логику компонента PartImage.
    /// </summary>
    [Table("part_images")]
    public class PartImage
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
        /// Свойство Url предоставляет доступ к данным Url.
        /// </summary>

        [Required]
        [Column("url")]
        public string Url { get; set; }
        /// <summary>
        /// Свойство IsPrimary предоставляет доступ к данным IsPrimary.
        /// </summary>

        [Column("is_primary")]
        public bool IsPrimary { get; set; } = false;
        /// <summary>
        /// Свойство AddedAt предоставляет доступ к данным AddedAt.
        /// </summary>

        [Column("added_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство Status предоставляет доступ к данным Status.
        /// </summary>

        [Column("status")]
        public string Status { get; set; } = "pending";
        /// <summary>
        /// Свойство Part предоставляет доступ к данным Part.
        /// </summary>

        [ForeignKey("PartId")]
        public virtual Part Part { get; set; }
    }
}
