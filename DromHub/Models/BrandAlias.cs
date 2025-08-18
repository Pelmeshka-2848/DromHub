using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    [Table("brand_aliases")]
    public class BrandAlias
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("alias")]
        [MaxLength(255)]
        public string Alias { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Required]
        [Column("brand_id")]
        public Guid BrandId { get; set; }

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }
    }
}
