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
    /// Класс BrandAlias отвечает за логику компонента BrandAlias.
    /// </summary>
    [Table("brand_aliases")]
    public class BrandAlias
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство Alias предоставляет доступ к данным Alias.
        /// </summary>

        [Required]
        [Column("alias")]
        [MaxLength(255)]
        public string Alias { get; set; }
        /// <summary>
        /// Свойство Note предоставляет доступ к данным Note.
        /// </summary>

        [Column("note")]
        public string? Note { get; set; }
        /// <summary>
        /// Свойство BrandId предоставляет доступ к данным BrandId.
        /// </summary>

        [Required]
        [Column("brand_id")]
        public Guid BrandId { get; set; }
        /// <summary>
        /// Свойство IsPrimary предоставляет доступ к данным IsPrimary.
        /// </summary>

        [Required]
        [Column("is_primary")]
        public bool IsPrimary { get; set; } = false;
        /// <summary>
        /// Свойство Brand предоставляет доступ к данным Brand.
        /// </summary>

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }
    }
}