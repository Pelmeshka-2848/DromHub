using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    [Table("countries")]
    public class Country
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("iso2")]
        [StringLength(2)]
        public string Iso2 { get; set; } = string.Empty;

        // Europe / Americas / Asia … (или твои значения)
        [Column("region")]
        public string? Region { get; set; }

        // В БД: flag_icon_name text
        [Column("flag_icon_name")]
        public string? FlagIconName { get; set; }

        // В БД: region_icon_name text
        [Column("region_icon_name")]
        public string? RegionIconName { get; set; }
    }
}