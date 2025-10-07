using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    [Table("brands")]
    public class Brand
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required, MaxLength(255)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("normalized_name")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string? NormalizedName { get; private set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_oem")]
        public bool IsOem { get; set; }

        [Column("website"), MaxLength(512)]
        public string? Website { get; set; }

        // НОВОЕ
        [Column("description"), MaxLength(2000)]
        public string? Description { get; set; }

        [Column("user_notes"), MaxLength(4000)]
        public string? UserNotes { get; set; }

        [Column("year_founded")]
        public int? YearFounded { get; set; }

        // FK → countries
        [Column("country_id")]
        public Guid? CountryId { get; set; }   // ← было int?, должно быть Guid?
        public Country? Country { get; set; }

        // Навигация (как у тебя было)
        public virtual ICollection<BrandAlias>? Aliases { get; set; }
        public virtual BrandMarkup? Markup { get; set; }
        public virtual ICollection<Part>? Parts { get; set; }

        // UI helpers
        [NotMapped] public int PartsCount { get; set; }
        [NotMapped] public int AliasesCount { get; set; }

        // Наценка бренда в процентах (заполняем из BrandMarkups)
        [NotMapped] public decimal? MarkupPercent { get; set; }

        // Число непервичных алиасов (для фильтра "без алиасов")
        [NotMapped] public int NonPrimaryAliasesCount { get; set; }
    }
}