using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;

namespace DromHub.Services
{
    /// <summary>
    /// Определяет срез аудита по типу операции, когда требуется сузить выдачу лога изменений брендов.
    /// Используется UI для фильтрации записей, обеспечивая соответствие символам триггера (<c>I</c>, <c>U</c>, <c>D</c>).
    /// Значения синхронизированы со структурой таблицы <c>brand_audit_log</c>, поэтому изменение требует обновления триггера.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: перечисление неизменно и потокобезопасно.
    /// Побочные эффекты: отсутствуют.
    /// См. также: <see cref="BrandAuditService"/>.
    /// </remarks>
    public enum AuditActionFilter
    {
        /// <summary>
        /// Возвращает все события аудита без дополнительной фильтрации по типу действия.
        /// Подходит для стартового отображения истории изменений.
        /// </summary>
        All,

        /// <summary>
        /// Ограничивает выборку событиями вставки (<c>I</c>), полезно при анализе появления новых брендов.
        /// </summary>
        Insert,

        /// <summary>
        /// Включает только обновления (<c>U</c>), помогая выявить правки атрибутов бренда.
        /// </summary>
        Update,

        /// <summary>
        /// Отбирает события удаления (<c>D</c>), что актуально при расследовании потери данных.
        /// </summary>
        Delete
    }

    /// <summary>
    /// Инкапсулирует параметры фильтрации, поиска и пагинации для выборки записей аудита брендов.
    /// Позволяет UI гибко настраивать запросы к сервису, избегая прямой работы с SQL.
    /// Используйте при формировании запросов из диалогов настройки историй изменений.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: класс мутабелен и не потокобезопасен; используйте в пределах одного UI-потока.
    /// Побочные эффекты: отсутствуют.
    /// Требования к nullability: допускает <see langword="null"/> для необязательных параметров.
    /// </remarks>
    public sealed class BrandAuditFilter
    {
        /// <summary>
        /// Определяет идентификатор бренда, по которому нужно отфильтровать историю.
        /// Значение <see cref="Guid.Empty"/> отключает фильтр.
        /// </summary>
        /// <value>GUID бренда; по умолчанию — <see cref="Guid.Empty"/>.</value>
        public Guid BrandId { get; set; }

        /// <summary>
        /// Задает нижнюю границу временного диапазона выборки.
        /// Помогает отсеять устаревшие события и ускорить запрос.
        /// </summary>
        /// <value>Дата и время в часовом поясе сервера; допускает <see langword="null"/>.</value>
        public DateTime? From { get; set; }

        /// <summary>
        /// Задает верхнюю границу временного диапазона выборки.
        /// Используйте, чтобы просмотреть историю до конкретного момента.
        /// </summary>
        /// <value>Дата и время в часовом поясе сервера; допускает <see langword="null"/>.</value>
        public DateTime? To { get; set; }

        /// <summary>
        /// Определяет, какие типы действий аудита включать в выдачу.
        /// </summary>
        /// <value>Значение перечисления <see cref="AuditActionFilter"/>; по умолчанию — <see cref="AuditActionFilter.All"/>.</value>
        public AuditActionFilter Action { get; set; } = AuditActionFilter.All;

        /// <summary>
        /// Подстрока для поиска по текстовому представлению данных аудита.
        /// Позволяет быстро находить конкретные изменения.
        /// </summary>
        /// <value>Чувствительность к регистру зависит от функции <c>ILIKE</c>; допускает <see langword="null"/>.</value>
        public string? Search { get; set; }

        /// <summary>
        /// Определяет, следует ли возвращать только записи с непустым списком измененных полей.
        /// Помогает скрыть технические обновления без фактических изменений.
        /// </summary>
        /// <value><see langword="true"/>, если нужно оставить только события с изменениями.</value>
        public bool OnlyChangedFields { get; set; }

        /// <summary>
        /// Индекс страницы для пагинации; отрицательные значения нормализуются сервисом до нуля.
        /// </summary>
        /// <value>Номер страницы, начиная с нуля.</value>
        public int PageIndex { get; set; } = 0;

        /// <summary>
        /// Желаемый размер страницы; сервис ограничивает значение диапазоном [1; 200].
        /// </summary>
        /// <value>Количество строк на страницу; по умолчанию — 20.</value>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Представляет строку аудита бренда, подготовленную для потребления XAML-интерфейсом.
    /// Инкапсулирует исходные данные и производные текстовые представления, уменьшая нагрузку на слой представления.
    /// Обеспечивает единообразное отображение истории изменений в разных разделах приложения.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: объект иммутабелен после инициализации; допускает совместное чтение.
    /// Побочные эффекты: отсутствуют.
    /// </remarks>
    public sealed class BrandAuditRow
    {
        /// <summary>
        /// Идентификатор события аудита, совпадает с <c>event_id</c> в таблице.
        /// </summary>
        /// <value>GUID события.</value>
        public Guid Id { get; init; }

        /// <summary>
        /// Отметка времени события, используемая для сортировки и отображения.
        /// </summary>
        /// <value>Дата и время в UTC.</value>
        public DateTime Ts { get; init; }

        /// <summary>
        /// Код действия аудита (<c>I</c>, <c>U</c>, <c>D</c>), применяемый для визуализации и фильтрации.
        /// </summary>
        /// <value>Строка длиной 1; по умолчанию — пустая строка.</value>
        public string Action { get; init; } = string.Empty;

        /// <summary>
        /// Имя пользователя или роли, выполнившей изменение.
        /// </summary>
        /// <value>Человеко-читаемое имя; по умолчанию — пустая строка.</value>
        public string User { get; init; } = string.Empty;

        /// <summary>
        /// Имя таблицы, к которой относится событие аудита.
        /// </summary>
        /// <value>Название таблицы; по умолчанию — пустая строка.</value>
        public string Table { get; init; } = string.Empty;

        /// <summary>
        /// Идентификатор бренда, который затронуто изменением.
        /// </summary>
        /// <value>GUID бренда или <see langword="null"/>.</value>
        public Guid? BrandId { get; init; }

        /// <summary>
        /// JSON-снимок состояния сущности до изменения.
        /// </summary>
        /// <value>Строка JSON или <see langword="null"/>.</value>
        public string? OldJson { get; init; }

        /// <summary>
        /// JSON-снимок состояния сущности после изменения.
        /// </summary>
        /// <value>Строка JSON или <see langword="null"/>.</value>
        public string? NewJson { get; init; }

        /// <summary>
        /// Список названий столбцов, которые фактически изменились.
        /// </summary>
        /// <value>Набор имен; пуст, если данные не предоставлены триггером.</value>
        public IReadOnlyList<string> ChangedColumns { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Готовое представление даты и времени для XAML.
        /// </summary>
        /// <value>Строка формата «ДД.ММ.ГГГГ ЧЧ:ММ».</value>
        public string TsDisplay => Ts.ToString("dd.MM.yyyy HH:mm");

        /// <summary>
        /// Отображаемая форма кода действия.
        /// </summary>
        /// <value>Заглавная буква или «—» при отсутствии значения.</value>
        public string ActionDisplay => string.IsNullOrEmpty(Action) ? "—" : Action.ToUpperInvariant();

        /// <summary>
        /// Текстовое представление списка измененных столбцов.
        /// </summary>
        /// <value>Строка с именами через запятую или «—».</value>
        public string ChangedColumnsJoined => ChangedColumns.Count == 0 ? "—" : string.Join(", ", ChangedColumns);

        /// <summary>
        /// Отображаемое значение автора изменения.
        /// </summary>
        /// <value>Имя пользователя или «—».</value>
        public string UserDisplay => string.IsNullOrWhiteSpace(User) ? "—" : User;

        /// <summary>
        /// Отображаемое название таблицы.
        /// </summary>
        /// <value>Имя таблицы или «—».</value>
        public string TableDisplay => string.IsNullOrWhiteSpace(Table) ? "—" : Table;
    }

    /// <summary>
    /// Реализует чтение лога аудита брендов с учетом фильтров и пагинации.
    /// Снимает необходимость в прямых SQL-запросах, предоставляя готовую модель данных для UI.
    /// Учитывает специфику триггера <c>trg_brand_audit</c> и структуру таблицы <c>brand_audit_log</c>.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр потокобезопасен при условии потокобезопасной реализации <see cref="IDbContextFactory{TContext}"/>.
    /// Побочные эффекты: выполняет операции чтения из БД PostgreSQL.
    /// </remarks>
    public sealed class BrandAuditService
    {
        /// <summary>
        /// Сохраняет фабрику контекста данных для создания подключений по требованию.
        /// </summary>
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        /// <summary>
        /// Инициализирует сервис аудита с фабрикой контекста данных.
        /// </summary>
        /// <param name="dbFactory">Фабрика, создающая экземпляры <see cref="ApplicationDbContext"/> по требованию.</param>
        public BrandAuditService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Загружает записи аудита бренда, применяя фильтры, поиск и пагинацию.
        /// Сопоставляет символьные коды действий триггера с пользовательскими фильтрами.
        /// </summary>
        /// <param name="filter">Параметры фильтрации и пагинации; допускают пустые значения.</param>
        /// <param name="ct">Токен отмены операции. При отмене выбрасывается <see cref="OperationCanceledException"/>.</param>
        /// <returns>Кортеж, содержащий список строк для UI и общее количество записей.</returns>
        /// <exception cref="InvalidOperationException">Контекст данных не смог подключиться к таблице аудита.</exception>
        /// <exception cref="OperationCanceledException">Отмена операции пользователем или инфраструктурой через <paramref name="ct"/>.</exception>
        /// <remarks>
        /// Предусловия: фабрика контекста должна быть сконфигурирована и доступна.
        /// Постусловия: контекст корректно освобожден (используется <see cref="IAsyncDisposable"/>).
        /// Потокобезопасность: метод безопасен для параллельного вызова; каждый вызов создает новый контекст.
        /// Побочные эффекты: выполняет один запрос COUNT и один запрос SELECT.
        /// Сложность: O(n) относительно размера страницы.
        /// </remarks>
        /// <example>
        /// <code>
        /// var filter = new BrandAuditFilter { BrandId = brandId, OnlyChangedFields = true };
        /// var (rows, total) = await auditService.GetAsync(filter, cancellationToken);
        /// </code>
        /// </example>
        public async Task<(IReadOnlyList<BrandAuditRow> Rows, int Total)> GetAsync(
            BrandAuditFilter filter,
            CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // Таблица аудита: brand_audit_log (создана SQL-триггером)
            var q = db.BrandAuditLogs.AsNoTracking().AsQueryable();

            // ---- FILTERS ----
            if (filter.BrandId != Guid.Empty)
                q = q.Where(x => x.BrandId == filter.BrandId);

            if (filter.From.HasValue)
                q = q.Where(x => x.EventTime >= filter.From.Value);

            if (filter.To.HasValue)
                q = q.Where(x => x.EventTime <= filter.To.Value);

            if (filter.Action != AuditActionFilter.All)
            {
                var act = ToActionCode(filter.Action);
                if (act.HasValue)
                {
                    q = q.Where(x => x.Action == act.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                // простейший поиск по JSON: text-представление
                q = q.Where(x =>
                    (x.OldText != null && EF.Functions.ILike(x.OldText!, $"%{s}%")) ||
                    (x.NewText != null && EF.Functions.ILike(x.NewText!, $"%{s}%")));
            }

            if (filter.OnlyChangedFields)
            {
                q = q.Where(x => x.Action == 'U' && x.ChangedColumns != null && x.ChangedColumns.Length > 0);
            }

            // ---- ORDER ----
            q = q.OrderByDescending(x => x.EventTime);

            // ---- TOTAL ----
            var total = await q.CountAsync(ct);

            // ---- PAGE ----
            var page = Math.Max(0, filter.PageIndex);
            var size = Math.Clamp(filter.PageSize, 1, 200);
            q = q.Skip(page * size).Take(size);

            var data = await q.ToListAsync(ct);

            // ---- MAP -> UI модель ----
            var rows = new List<BrandAuditRow>(data.Count);
            foreach (var a in data)
            {
                rows.Add(new BrandAuditRow
                {
                    Id = a.EventId,
                    Ts = a.EventTime.UtcDateTime,
                    Action = MapActionDisplay(a.Action),
                    User = a.Actor ?? string.Empty,
                    Table = "brands",
                    BrandId = a.BrandId,
                    OldJson = a.OldData,
                    NewJson = a.NewData,
                    ChangedColumns = a.ChangedColumns ?? Array.Empty<string>()
                });
            }

            return (rows, total);
        }

        /// <summary>
        /// Преобразует выбранное значение фильтра действий в код, используемый триггером аудита.
        /// </summary>
        /// <param name="filter">Значение фильтра действий.</param>
        /// <returns>Символ действия или <see langword="null"/>, если фильтр не требуется.</returns>
        /// <remarks>
        /// Потокобезопасность: статический метод, не использующий состояние.
        /// Побочные эффекты: отсутствуют.
        /// </remarks>
        /// <example>
        /// <code>
        /// var code = ToActionCode(AuditActionFilter.Update); // вернет 'U'
        /// </code>
        /// </example>
        private static char? ToActionCode(AuditActionFilter filter) => filter switch
        {
            AuditActionFilter.Insert => 'I',
            AuditActionFilter.Update => 'U',
            AuditActionFilter.Delete => 'D',
            _ => null
        };

        /// <summary>
        /// Конвертирует код действия из таблицы аудита в строковое представление для UI.
        /// </summary>
        /// <param name="action">Код действия (<c>I</c>, <c>U</c>, <c>D</c>).</param>
        /// <returns>Однобуквенная строка или пустая строка для неизвестных значений.</returns>
        /// <remarks>
        /// Потокобезопасность: статический метод без состояния.
        /// Побочные эффекты: отсутствуют.
        /// </remarks>
        /// <example>
        /// <code>
        /// var display = MapActionDisplay('I'); // вернет "I"
        /// </code>
        /// </example>
        private static string MapActionDisplay(char action) => action switch
        {
            'I' => "I",
            'U' => "U",
            'D' => "D",
            _ => string.Empty
        };
    }
}
