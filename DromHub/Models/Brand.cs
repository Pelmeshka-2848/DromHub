using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("brands")]
    public class Brand
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        [MaxLength(255)]
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

        // Навигационные свойства
        public virtual ICollection<BrandAlias> Aliases { get; set; }
        public virtual BrandMarkup Markup { get; set; }
        public virtual ICollection<Part> Parts { get; set; }
    }
}
