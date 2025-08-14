using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("part_images")]
    public class PartImage
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("part_id")]
        public Guid PartId { get; set; }

        [Required]
        [Column("url")]
        public string Url { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; } = false;

        [Column("added_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [Column("status")]
        public string Status { get; set; } = "pending";

        [ForeignKey("PartId")]
        public virtual Part Part { get; set; }
    }
}
