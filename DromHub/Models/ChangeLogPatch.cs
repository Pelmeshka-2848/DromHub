using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Представляет патчноут — набор изменений, объединённых релизом.
    /// </summary>
    [Table("change_log_patches")]
    public class ChangeLogPatch
    {
        /// <summary>
        /// Идентификатор патча.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Версия релиза (например, «1.2.0»).
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Дополнительный заголовок или название релиза.
        /// </summary>
        [MaxLength(256)]
        [Column("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Дата релиза патча.
        /// </summary>
        [Column("release_date")]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Порядок сортировки патча в истории.
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; }

        /// <summary>
        /// Разделы патча.
        /// </summary>
        public ICollection<ChangeLogSection> Sections { get; set; } = new List<ChangeLogSection>();
    }
}
