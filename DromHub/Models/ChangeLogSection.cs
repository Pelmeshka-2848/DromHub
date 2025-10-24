using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Раздел патчноута, объединяющий записи определённой категории.
    /// </summary>
    [Table("change_log_sections")]
    public class ChangeLogSection
    {
        /// <summary>
        /// Идентификатор раздела.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Идентификатор патча, к которому относится раздел.
        /// </summary>
        [Required]
        [Column("patch_id")]
        public Guid PatchId { get; set; }

        /// <summary>
        /// Патч, к которому относится раздел.
        /// </summary>
        public ChangeLogPatch? Patch { get; set; }

        /// <summary>
        /// Заголовок раздела.
        /// </summary>
        [Required]
        [MaxLength(256)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Категория изменений.
        /// </summary>
        [Required]
        [Column("category")]
        public ChangeLogCategory Category { get; set; }

        /// <summary>
        /// Порядок сортировки раздела внутри патча.
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; }

        /// <summary>
        /// Записи, входящие в раздел.
        /// </summary>
        public ICollection<ChangeLogEntry> Entries { get; set; } = new List<ChangeLogEntry>();
    }
}
