using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DromHub.Data;
using DromHub.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    /// Обеспечивает единообразное отображение истории изменений и поддерживает выбор элементов для пакетных операций.
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: объект иммутабелен после инициализации; допускает совместное чтение.
    /// Побочные эффекты: отсутствуют.
    /// </remarks>
    public sealed class BrandAuditRow : ObservableObject
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

        /// <summary>
        /// Содержит детализированный список изменений значений по каждому столбцу.
        /// </summary>
        /// <value>Наблюдаемая последовательность элементов <see cref="BrandAuditValueChange"/>; по умолчанию — пустая.</value>
        public IReadOnlyList<BrandAuditValueChange> ValueChanges { get; init; } = Array.Empty<BrandAuditValueChange>();

        /// <summary>
        /// <para>Определяет, выбран ли элемент пользователем для пакетных операций (например, удаления записей аудита).</para>
        /// <para>Используется привязками XAML для синхронизации чекбоксов и команд выборки.</para>
        /// <para>Сбрасывается при каждой повторной загрузке данных, чтобы исключить ложные срабатывания.</para>
        /// </summary>
        /// <value><see langword="true"/>, если запись отмечена; значение по умолчанию — <see langword="false"/>.</value>
        /// <remarks>
        /// Потокобезопасность: свойство предназначено для изменения из UI-потока.
        /// </remarks>
        /// <example>
        /// <code>
        /// row.IsSelected = true;
        /// </code>
        /// </example>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Хранит текущее значение свойства <see cref="IsSelected"/>, обеспечивая уведомления об изменениях.
        /// </summary>
        private bool _isSelected;
    }

    /// <summary>
    /// <para>Представляет пару «старое-новое» значение для конкретного столбца аудита, обеспечивая однозначную трактовку дельты.</para>
    /// <para>Используется для визуализации изменений в интерфейсе и для ручной проверки корректности данных.</para>
    /// <para>Поддерживает только плоские столбцы; вложенные структуры выводятся как JSON-строки для сохранения контекста.</para>
    /// </summary>
    /// <remarks>
    /// Потокобезопасность: экземпляр иммутабелен после инициализации.
    /// Побочные эффекты: отсутствуют.
    /// </remarks>
    public sealed class BrandAuditValueChange
    {
        /// <summary>
        /// Название столбца, для которого отображается изменение.
        /// </summary>
        /// <value>Имя столбца в таблице брендов; никогда не пустая строка.</value>
        public string ColumnName { get; init; } = string.Empty;

        /// <summary>
        /// Подготовленное текстовое представление значения до изменения.
        /// </summary>
        /// <value>Строка с человеческим описанием или «—».</value>
        public string OldValueDisplay { get; init; } = "—";

        /// <summary>
        /// Подготовленное текстовое представление значения после изменения.
        /// </summary>
        /// <value>Строка с человеческим описанием или «—».</value>
        public string NewValueDisplay { get; init; } = "—";
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
                    ChangedColumns = a.ChangedColumns ?? Array.Empty<string>(),
                    ValueChanges = BuildValueChanges(a)
                });
            }

            return (rows, total);
        }

        /// <summary>
        /// <para>Удаляет выбранные записи аудита бренда, чтобы администратор мог скрыть технический шум или исправить ошибки триггера.</para>
        /// <para>Применяйте после ручного анализа, когда сохранение истории нежелательно либо нарушает требования комплаенса.</para>
        /// <para>Поддерживает массовое удаление; оптимизировано под пакетные операции без загрузки сущностей в память.</para>
        /// </summary>
        /// <param name="brandId">Идентификатор бренда, для которого подтверждено удаление; допускает <see cref="Guid.Empty"/> для снятия ограничения.</param>
        /// <param name="eventIds">Коллекция идентификаторов событий аудита; игнорируются значения <see cref="Guid.Empty"/> и дубликаты.</param>
        /// <param name="ct">Токен отмены; при отмене выбрасывается <see cref="OperationCanceledException"/> до применения изменений.</param>
        /// <returns>Количество удалённых записей; может быть меньше числа запросов из-за фильтрации по бренду.</returns>
        /// <exception cref="ArgumentNullException">Когда <paramref name="eventIds"/> не предоставлены.</exception>
        /// <exception cref="OperationCanceledException">При отмене операции инфраструктурой или пользователем.</exception>
        /// <remarks>
        /// Предусловия: вызывающий должен убедиться в наличии административных прав на изменение журнала.
        /// Потокобезопасность: метод безопасен для параллельных вызовов; каждый вызов создает отдельный контекст.
        /// Побочные эффекты: выполняет оператор DELETE в БД PostgreSQL; изменения необратимы.
        /// Сложность: O(k) по числу удаляемых идентификаторов; запрос компилируется один раз на весь пакет.
        /// </remarks>
        /// <example>
        /// <code>
        /// var removed = await auditService.DeleteAsync(brandId, selectedIds, ct);
        /// if (removed == 0)
        /// {
        ///     // Записи уже удалены или принадлежали другому бренду.
        /// }
        /// </code>
        /// </example>
        public async Task<int> DeleteAsync(Guid brandId, IEnumerable<Guid> eventIds, CancellationToken ct = default)
        {
            if (eventIds is null)
            {
                throw new ArgumentNullException(nameof(eventIds));
            }

            var normalized = eventIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (normalized.Length == 0)
            {
                return 0;
            }

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var query = db.BrandAuditLogs.Where(x => normalized.Contains(x.EventId));

            if (brandId != Guid.Empty)
            {
                query = query.Where(x => x.BrandId == brandId);
            }

            var removed = await query.ExecuteDeleteAsync(ct);
            return removed;
        }

        /// <summary>
        /// Формирует читаемый список изменений значений на основе данных аудита.
        /// </summary>
        /// <param name="entity">Запись аудита из базы данных.</param>
        /// <returns>Список изменений для отображения в UI.</returns>
        /// <remarks>
        /// Алгоритм сопоставляет JSON-снимки и список столбцов, отфильтровывая неизменённые значения.
        /// Сложность: O(n) относительно количества затронутых столбцов.
        /// Потокобезопасность: статический метод без состояния.
        /// </remarks>
        private static IReadOnlyList<BrandAuditValueChange> BuildValueChanges(BrandAuditLog entity)
        {
            var result = new List<BrandAuditValueChange>();
            var oldDoc = ParseJson(entity.OldData);
            var newDoc = ParseJson(entity.NewData);

            var changed = entity.ChangedColumns?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (changed is { Length: > 0 })
            {
                foreach (var column in changed)
                {
                    var oldValue = ExtractValue(oldDoc, column);
                    var newValue = ExtractValue(newDoc, column);

                    if (oldValue == newValue)
                    {
                        continue;
                    }

                    result.Add(new BrandAuditValueChange
                    {
                        ColumnName = column,
                        OldValueDisplay = oldValue,
                        NewValueDisplay = newValue
                    });
                }
            }
            else if (entity.Action == 'I' && newDoc is not null)
            {
                foreach (var property in newDoc.Properties())
                {
                    result.Add(new BrandAuditValueChange
                    {
                        ColumnName = property.Name,
                        OldValueDisplay = "—",
                        NewValueDisplay = FormatJsonValue(property.Value)
                    });
                }
            }
            else if (entity.Action == 'D' && oldDoc is not null)
            {
                foreach (var property in oldDoc.Properties())
                {
                    result.Add(new BrandAuditValueChange
                    {
                        ColumnName = property.Name,
                        OldValueDisplay = FormatJsonValue(property.Value),
                        NewValueDisplay = "—"
                    });
                }
            }
            else if (oldDoc is not null && newDoc is not null)
            {
                var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in newDoc.Properties())
                {
                    names.Add(property.Name);
                }

                foreach (var property in oldDoc.Properties())
                {
                    names.Add(property.Name);
                }

                foreach (var name in names)
                {
                    var oldValue = ExtractValue(oldDoc, name);
                    var newValue = ExtractValue(newDoc, name);

                    if (oldValue == newValue)
                    {
                        continue;
                    }

                    result.Add(new BrandAuditValueChange
                    {
                        ColumnName = name,
                        OldValueDisplay = oldValue,
                        NewValueDisplay = newValue
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Выполняет безопасную материализацию JSON-строки из аудита в <see cref="JObject"/>, чтобы последующий анализ мог использовать LINQ-проекции.
        /// Применяйте при сравнении снимков «до/после», когда входные данные формируются PostgreSQL-триггером и могут быть пустыми или частично повреждёнными.
        /// Возвращает <see langword="null"/>, если строка отсутствует или не проходит синтаксический разбор, тем самым сигнализируя UI о необходимости graceful degradation.
        /// </summary>
        /// <param name="json">Строка JSON или <see langword="null"/>; допускаются пустые строки.</param>
        /// <returns><see cref="JObject"/>, готовый к чтению свойств, либо <see langword="null"/> при невозможности разбора.</returns>
        /// <remarks>
        /// Предусловия: отсутствуют.
        /// Постусловия: возвращаемый объект не содержит ссылок на исходный буфер.
        /// Побочные эффекты: отсутствуют.
        /// Потокобезопасность: статический метод без состояния.
        /// Сложность: O(n) относительно длины JSON-строки.
        /// </remarks>
        /// <example>
        /// <code>
        /// var parsed = ParseJson(entity.NewData);
        /// var status = parsed?[
        ///     "status"
        /// ]?.ToString();
        /// </code>
        /// </example>
        private static JObject? ParseJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JObject.Parse(json);
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }

        /// <summary>
        /// Извлекает значение свойства из JSON-снимка и приводит его к формату, пригодному для таблицы изменений.
        /// Используйте, когда нужно сопоставить конкретный столбец из списка <c>changed_columns</c> с фактическим значением в документе.
        /// Возвращает «—» для отсутствующих полей, подчёркивая, что триггер не передал данные.
        /// </summary>
        /// <param name="doc">JSON-объект или <see langword="null"/>, предоставленный триггером аудита.</param>
        /// <param name="propertyName">Имя свойства в формате столбца базы данных; сравнивается без учёта регистра.</param>
        /// <returns>Отформатированное строковое значение или «—», если свойство не найдено.</returns>
        /// <remarks>
        /// Предусловия: <paramref name="propertyName"/> не <see cref="string.Empty"/>.
        /// Побочные эффекты: отсутствуют.
        /// Потокобезопасность: статический метод без состояния.
        /// Сложность: O(1) для плоских объектов.
        /// </remarks>
        /// <example>
        /// <code>
        /// var payload = JObject.Parse("{\"name\":\"Acme\"}");
        /// var value = ExtractValue(payload, "name"); // "Acme"
        /// </code>
        /// </example>
        private static string ExtractValue(JObject? doc, string propertyName)
        {
            if (doc is null)
            {
                return "—";
            }

            if (!doc.TryGetValue(propertyName, StringComparison.OrdinalIgnoreCase, out var element))
            {
                return "—";
            }

            return FormatJsonValue(element);
        }

        /// <summary>
        /// Конвертирует произвольное JSON-значение в компактную строку, пригодную для отображения в таблице аудита.
        /// Поддерживает базовые типы <see cref="JValue"/> и сворачивает сложные структуры в однострочный JSON, чтобы не перегружать UI.
        /// Гарантирует детерминированное форматирование чисел и дат, исключая региональные артефакты.
        /// </summary>
        /// <param name="element">JSON-значение для отображения; допускает <see langword="null"/>.</param>
        /// <returns>Форматированная строка, готовая к показу в UI.</returns>
        /// <remarks>
        /// Предусловия: отсутствуют.
        /// Побочные эффекты: отсутствуют.
        /// Потокобезопасность: статический метод без состояния.
        /// Сложность: O(1) для примитивов, O(n) для сериализации вложенных структур.
        /// </remarks>
        /// <example>
        /// <code>
        /// var rendered = FormatJsonValue(JToken.Parse("{\"isActive\":true}")["isActive"]);
        /// // rendered == "true"
        /// </code>
        /// </example>
        private static string FormatJsonValue(JToken? element)
        {
            if (element is null || element.Type == JTokenType.Undefined)
            {
                return "—";
            }

            return element.Type switch
            {
                JTokenType.Null => "null",
                JTokenType.String => element.Value<string>() ?? string.Empty,
                JTokenType.Integer => element.Value<long>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Float => element.Value<double>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Boolean => element.Value<bool>() ? "true" : "false",
                JTokenType.Date => element.Value<DateTime>().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => element.ToString(Formatting.None)
            };
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
