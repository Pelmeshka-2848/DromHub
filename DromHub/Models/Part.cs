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
    [Table("parts")]
    public class Part
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("brand_id")]
        public Guid BrandId { get; set; }

        [Required]
        [Column("catalog_number")]
        [MaxLength(255)]
        public string CatalogNumber { get; set; }

        [Column("article")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string Article { get;  set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }

        public virtual ICollection<PartImage> Images { get; set; }
        public virtual ICollection<LocalStock> LocalStocks { get; set; }
        public virtual ICollection<OemCross> OemCrossesAsOem { get; set; }
        public virtual ICollection<OemCross> OemCrossesAsAftermarket { get; set; }
    }
}
