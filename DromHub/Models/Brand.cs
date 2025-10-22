using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Класс Brand отвечает за логику компонента Brand.
    /// </summary>
    [Table("brands")]
    public class Brand
    {
        /// <summary>
        /// Свойство Id предоставляет доступ к данным Id.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Свойство Name предоставляет доступ к данным Name.
        /// </summary>

        [Required, MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Свойство NormalizedName предоставляет доступ к данным NormalizedName.
        /// </summary>

        [Column("normalized_name")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string? NormalizedName { get; private set; }
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
        /// Свойство IsOem предоставляет доступ к данным IsOem.
        /// </summary>

        [Column("is_oem")]
        public bool IsOem { get; set; }
        /// <summary>
        /// Свойство Website предоставляет доступ к данным Website.
        /// </summary>

        [Column("website"), MaxLength(512)]
        public string? Website { get; set; }

        // НОВОЕ
        /// <summary>
        /// Свойство Description предоставляет доступ к данным Description.
        /// </summary>
        [Column("description"), MaxLength(2000)]
        public string? Description { get; set; }
        /// <summary>
        /// Свойство UserNotes предоставляет доступ к данным UserNotes.
        /// </summary>

        [Column("user_notes"), MaxLength(4000)]
        public string? UserNotes { get; set; }
        /// <summary>
        /// Свойство YearFounded предоставляет доступ к данным YearFounded.
        /// </summary>

        [Column("year_founded")]
        public int? YearFounded { get; set; }

        // FK → countries
        /// <summary>
        /// Свойство CountryId предоставляет доступ к данным CountryId.
        /// </summary>
        [Column("country_id")]
        public Guid? CountryId { get; set; }   // ← было int?, должно быть Guid?
        /// <summary>
        /// Свойство Country предоставляет доступ к данным Country.
        /// </summary>
        public Country? Country { get; set; }

        // Навигация (как у тебя было)
        /// <summary>
        /// Свойство Aliases предоставляет доступ к данным Aliases.
        /// </summary>
        public virtual ICollection<BrandAlias>? Aliases { get; set; }
        /// <summary>
        /// Свойство Markup предоставляет доступ к данным Markup.
        /// </summary>
        public virtual BrandMarkup? Markup { get; set; }
        /// <summary>
        /// Свойство Parts предоставляет доступ к данным Parts.
        /// </summary>
        public virtual ICollection<Part>? Parts { get; set; }

        // UI helpers
        /// <summary>
        /// Свойство PartsCount предоставляет доступ к данным PartsCount.
        /// </summary>
        [NotMapped] public int PartsCount { get; set; }
        /// <summary>
        /// Свойство AliasesCount предоставляет доступ к данным AliasesCount.
        /// </summary>
        [NotMapped] public int AliasesCount { get; set; }

        // Наценка бренда в процентах (заполняем из BrandMarkups)
        /// <summary>
        /// Свойство MarkupPercent предоставляет доступ к данным MarkupPercent.
        /// </summary>
        [NotMapped] public decimal? MarkupPercent { get; set; }

        // Число непервичных алиасов (для фильтра "без алиасов")
        /// <summary>
        /// Свойство NonPrimaryAliasesCount предоставляет доступ к данным NonPrimaryAliasesCount.
        /// </summary>
        [NotMapped] public int NonPrimaryAliasesCount { get; set; }
    }
}