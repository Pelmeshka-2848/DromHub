using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("oem_crosses")]
    public class OemCross
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("oem_part_id")]
        public Guid OemPartId { get; set; }

        [Required]
        [Column("aftermarket_part_id")]
        public Guid AftermarketPartId { get; set; }

        [Column("source")]
        public string Source { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("OemPartId")]
        public virtual Part OemPart { get; set; }

        [ForeignKey("AftermarketPartId")]
        public virtual Part AftermarketPart { get; set; }
    }
}
