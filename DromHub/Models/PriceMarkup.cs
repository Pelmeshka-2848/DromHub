using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("price_markups")]
    public class PriceMarkup
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("max_price", TypeName = "numeric(12,2)")]
        public decimal MaxPrice { get; set; }

        [Required]
        [Column("markup_pct", TypeName = "numeric(6,2)")]
        public decimal MarkupPct { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
