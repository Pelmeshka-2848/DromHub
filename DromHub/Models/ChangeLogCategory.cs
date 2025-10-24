using System.ComponentModel.DataAnnotations;

namespace DromHub.Models
{
    /// <summary>
    /// Категории изменений в патчноутах.
    /// </summary>
    public enum ChangeLogCategory
    {
        /// <summary>
        /// Изменения, касающиеся брендов (описания, позиционирование).
        /// </summary>
        [Display(Name = "Бренд")] Brand,

        /// <summary>
        /// Обновления ассортимента и конкретных деталей.
        /// </summary>
        [Display(Name = "Детали")] Parts,

        /// <summary>
        /// Корректировки цен, складских остатков и поставщиков.
        /// </summary>
        [Display(Name = "Цены")] Pricing,

        /// <summary>
        /// Общие улучшения или технические изменения.
        /// </summary>
        [Display(Name = "Общее")] General,

        /// <summary>
        /// Работы с логистикой, сроками и поставками.
        /// </summary>
        [Display(Name = "Логистика")] Logistics
    }
}
