using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Конкретная запись патчноута.
    /// </summary>
    [Table("change_log_entries")]
    public class ChangeLogEntry
    {
        /// <summary>
        /// Идентификатор записи.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Раздел, к которому относится запись.
        /// </summary>
        [Required]
        [Column("section_id")]
        public Guid SectionId { get; set; }

        /// <summary>
        /// Навигация к разделу.
        /// </summary>
        public ChangeLogSection? Section { get; set; }

        /// <summary>
        /// Заголовок записи.
        /// </summary>
        [MaxLength(256)]
        [Column("headline")]
        public string? Headline { get; set; }

        /// <summary>
        /// Подробное описание изменения.
        /// </summary>
        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Уровень влияния изменения.
        /// </summary>
        [Required]
        [Column("impact_level")]
        public ChangeLogImpactLevel ImpactLevel { get; set; }

        /// <summary>
        /// Путь к иконке, отображающей тип изменения.
        /// </summary>
        [MaxLength(256)]
        [Column("icon_asset")]
        public string? IconAsset { get; set; }

        /// <summary>
        /// Прикреплённый бренд (если изменение относится к конкретному бренду).
        /// </summary>
        [Column("brand_id")]
        public Guid? BrandId { get; set; }

        /// <summary>
        /// Навигация к бренду.
        /// </summary>
        public Brand? Brand { get; set; }

        /// <summary>
        /// Прикреплённая деталь (если изменение относится к конкретной детали).
        /// </summary>
        [Column("part_id")]
        public Guid? PartId { get; set; }

        /// <summary>
        /// Навигация к детали.
        /// </summary>
        public Part? Part { get; set; }

        /// <summary>
        /// Порядок сортировки записи внутри раздела.
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
