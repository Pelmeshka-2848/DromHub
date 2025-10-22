using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DromHub.Models
{
    /// <summary>
    /// Класс Country отвечает за логику компонента Country.
    /// </summary>
    [Table("countries")]
    public class Country
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

        [Column("name")]
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Свойство Iso2 предоставляет доступ к данным Iso2.
        /// </summary>

        [Column("iso2")]
        [StringLength(2)]
        public string Iso2 { get; set; } = string.Empty;

        // Europe / Americas / Asia … (или твои значения)
        /// <summary>
        /// Свойство Region предоставляет доступ к данным Region.
        /// </summary>
        [Column("region")]
        public string? Region { get; set; }

        // В БД: flag_icon_name text
        /// <summary>
        /// Свойство FlagIconName предоставляет доступ к данным FlagIconName.
        /// </summary>
        [Column("flag_icon_name")]
        public string? FlagIconName { get; set; }

        // В БД: region_icon_name text
        /// <summary>
        /// Свойство RegionIconName предоставляет доступ к данным RegionIconName.
        /// </summary>
        [Column("region_icon_name")]
        public string? RegionIconName { get; set; }
    }
}