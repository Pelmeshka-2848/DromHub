using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DromHub.Models
{
    /// <summary>
    /// Класс Supplier отвечает за логику компонента Supplier.
    /// </summary>
    [Table("suppliers")]
    public class Supplier
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

        [Required]
        [Column("name")]
        [MaxLength(255)]
        public string Name { get; set; }
        /// <summary>
        /// Свойство Email предоставляет доступ к данным Email.
        /// </summary>

        [Column("email")]
        [MaxLength(255)]
        public string Email { get; set; }
        /// <summary>
        /// Свойство IsActive предоставляет доступ к данным IsActive.
        /// </summary>

        [Column("is_active")]
        public bool IsActive { get; set; } = true;
        /// <summary>
        /// Свойство CreatedAt предоставляет доступ к данным CreatedAt.
        /// </summary>

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство UpdatedAt предоставляет доступ к данным UpdatedAt.
        /// </summary>

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Свойство PriceSource предоставляет доступ к данным PriceSource.
        /// </summary>

        [Column("price_source")]
        public string PriceSource { get; set; } = "disk";
        /// <summary>
        /// Свойство LocalityId предоставляет доступ к данным LocalityId.
        /// </summary>

        [Column("locality_id")]
        public decimal LocalityId { get; set; }

        // Навигационные свойства
        /// <summary>
        /// Свойство Locality предоставляет доступ к данным Locality.
        /// </summary>
        [ForeignKey("LocalityId")]
        public virtual SupplierLocality Locality { get; set; }
        /// <summary>
        /// Свойство LocalStocks предоставляет доступ к данным LocalStocks.
        /// </summary>
        public virtual ICollection<LocalStock> LocalStocks { get; set; } = new List<LocalStock>();
        /// <summary>
        /// Свойство Markup предоставляет доступ к данным Markup.
        /// </summary>
        public virtual SupplierMarkup Markup { get; set; }
        /// <summary>
        /// Свойство PricelistLayout предоставляет доступ к данным PricelistLayout.
        /// </summary>
        public virtual SupplierPricelistLayout PricelistLayout { get; set; }
    }
}
