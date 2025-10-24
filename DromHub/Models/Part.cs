    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    namespace DromHub.Models
    {
        /// <summary>
        /// Класс Part отвечает за логику компонента Part.
        /// </summary>
        [Table("parts")]
        public class Part
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
            /// Свойство CatalogNumber предоставляет доступ к данным CatalogNumber.
            /// </summary>

            [Required]
            [Column("catalog_number")]
            [MaxLength(255)]
            public string CatalogNumber { get; set; }
            /// <summary>
            /// Свойство Article предоставляет доступ к данным Article.
            /// </summary>

            [Column("article")]
            [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
            public string Article { get;  set; }
            /// <summary>
            /// Свойство Name предоставляет доступ к данным Name.
            /// </summary>

            [Column("name")]
            public string Name { get; set; }
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

            // Навигационные свойства
            /// <summary>
            /// Свойство Brand предоставляет доступ к данным Brand.
            /// </summary>
            [ForeignKey("BrandId")]
            public virtual Brand Brand { get; set; }
            /// <summary>
            /// Свойство Images предоставляет доступ к данным Images.
            /// </summary>

            public virtual ICollection<PartImage> Images { get; set; }
            /// <summary>
            /// Свойство LocalStocks предоставляет доступ к данным LocalStocks.
            /// </summary>
            public virtual ICollection<LocalStock> LocalStocks { get; set; }
            /// <summary>
            /// Свойство OemCrossesAsOem предоставляет доступ к данным OemCrossesAsOem.
            /// </summary>
            public virtual ICollection<OemCross> OemCrossesAsOem { get; set; }
            /// <summary>
            /// Свойство OemCrossesAsAftermarket предоставляет доступ к данным OemCrossesAsAftermarket.
            /// </summary>
            public virtual ICollection<OemCross> OemCrossesAsAftermarket { get; set; }
        }
    }
