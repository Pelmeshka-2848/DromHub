using System.ComponentModel.DataAnnotations;

namespace DromHub.Models
{
    /// <summary>
    /// Уровень влияния записи патчноута.
    /// </summary>
    public enum ChangeLogImpactLevel
    {
        /// <summary>
        /// Небольшие изменения, влияющие на ограниченный набор сценариев.
        /// </summary>
        [Display(Name = "Низкий")] Low,

        /// <summary>
        /// Существенные правки, которые стоит учитывать в повседневной работе.
        /// </summary>
        [Display(Name = "Средний")] Medium,

        /// <summary>
        /// Большие изменения, влияющие на ключевые процессы.
        /// </summary>
        [Display(Name = "Высокий")] High,

        /// <summary>
        /// Критические изменения, требующие немедленного внимания.
        /// </summary>
        [Display(Name = "Критичный")] Critical
    }
}
