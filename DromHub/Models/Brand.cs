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
        public string Name { get; set; }

        [Column("normalized_name")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string NormalizedName { get; private set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_oem")]
        public bool IsOem { get; set; } = false;

        public void UpdateNormalizedName() => NormalizedName = Name?.ToUpperInvariant();

        // Навигация
        public virtual ICollection<BrandAlias> Aliases { get; set; }
        public virtual BrandMarkup Markup { get; set; }
        public virtual ICollection<Part> Parts { get; set; }

        // Поля для UI
        [NotMapped] public int PartsCount { get; set; }
        [NotMapped] public decimal? MarkupPercent { get; set; }

        // Для фильтров/диагностики
        [NotMapped] public int AliasesCount { get; set; }                // все алиасы
        [NotMapped] public int NonPrimaryAliasesCount { get; set; }      // без основного
    }
}