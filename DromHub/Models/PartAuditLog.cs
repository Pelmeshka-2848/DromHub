using System;

namespace DromHub.Models;

/// <summary>
/// <para>Описывает запись триггерного аудита детали, формируемую функцией <c>trg_part_audit</c> в базе PostgreSQL.</para>
/// <para>Используется сервисом <see cref="DromHub.Services.PartAuditService"/> для построения истории изменений запчастей в интерфейсе.</para>
/// <para>Не содержит бизнес-логики; отражает схему таблицы <c>part_audit_log</c> и поставляется как DTO для слоя представления.</para>
/// </summary>
/// <remarks>
/// Потокобезопасность: объект предназначен для неизменяемого чтения между потоками после материализации из БД.
/// Побочные эффекты: отсутствуют.
/// Требования к nullability: допускает <see langword="null"/> для необязательных полей (<see cref="PartId"/>, <see cref="OldData"/>, <see cref="NewData"/>).
/// </remarks>
public sealed class PartAuditLog
{
    /// <summary>
    /// <para>Содержит уникальный идентификатор события аудита, совпадающий с колонкой <c>event_id</c>.</para>
    /// <para>Используется как первичный ключ и обеспечивает идемпотентность операций удаления/отображения.</para>
    /// </summary>
    /// <value>Непустой GUID события аудита.</value>
    public Guid EventId { get; set; }

    /// <summary>
    /// <para>Фиксирует идентификатор детали, изменение которой инициировало запись аудита.</para>
    /// <para>Служит основным фильтром при просмотре истории конкретной запчасти.</para>
    /// </summary>
    /// <value>GUID детали или <see langword="null"/> для операций, не привязанных к конкретной записи.</value>
    public Guid? PartId { get; set; }

    /// <summary>
    /// <para>Отражает тип операции, выполненной над записью детали.</para>
    /// <para>Соответствует символам триггера: <c>'I'</c> — вставка, <c>'U'</c> — обновление, <c>'D'</c> — удаление.</para>
    /// </summary>
    /// <value>Буквенный код действия.</value>
    public char Action { get; set; }

    /// <summary>
    /// <para>Содержит перечень столбцов, значения которых изменились при операции обновления.</para>
    /// <para>Применяется для быстрого выявления бизнес-значимых правок.</para>
    /// </summary>
    /// <value>Массив технических имен столбцов или <see langword="null"/> для вставок и удалений.</value>
    public string[]? ChangedColumns { get; set; }

    /// <summary>
    /// <para>Хранит состояние детали до операции в формате JSONB.</para>
    /// <para>Позволяет восстанавливать исходные значения и сравнивать их с новыми.</para>
    /// </summary>
    /// <value>JSON-строка или <see langword="null"/>.</value>
    public string? OldData { get; set; }

    /// <summary>
    /// <para>Содержит новое состояние детали после операции в формате JSONB.</para>
    /// <para>Используется совместно с <see cref="OldData"/> для визуализации разницы.</para>
    /// </summary>
    /// <value>JSON-строка или <see langword="null"/> при удалении.</value>
    public string? NewData { get; set; }

    /// <summary>
    /// <para>Указывает имя пользователя или роли базы данных, выполнившей операцию.</para>
    /// <para>Нужен для аудита авторства и расследования инцидентов.</para>
    /// </summary>
    /// <value>Имя пользователя или <see langword="null"/>.</value>
    public string? Actor { get; set; }

    /// <summary>
    /// <para>Сохраняет значение <c>application_name</c> соединения PostgreSQL.</para>
    /// <para>Помогает идентифицировать клиентское приложение.</para>
    /// </summary>
    /// <value>Контекст приложения или <see langword="null"/>.</value>
    public string? AppContext { get; set; }

    /// <summary>
    /// <para>Фиксирует идентификатор транзакции PostgreSQL, в рамках которой выполнено изменение.</para>
    /// <para>Используется для группировки событий.</para>
    /// </summary>
    /// <value>Неотрицательное целое число.</value>
    public long TxId { get; set; }

    /// <summary>
    /// <para>Содержит момент времени создания записи аудита в UTC.</para>
    /// <para>Применяется для сортировки и фильтрации.</para>
    /// </summary>
    /// <value>Метка времени в формате <see cref="DateTimeOffset"/>.</value>
    public DateTimeOffset EventTime { get; set; }

    /// <summary>
    /// <para>Представляет текстовую версию старого состояния детали.</para>
    /// <para>Облегчает полнотекстовый поиск без десериализации JSON.</para>
    /// </summary>
    /// <value>Строка или <see langword="null"/>.</value>
    public string? OldText { get; set; }

    /// <summary>
    /// <para>Представляет текстовую версию нового состояния детали.</para>
    /// <para>Используется для текстового поиска и отображения.</para>
    /// </summary>
    /// <value>Строка или <see langword="null"/>.</value>
    public string? NewText { get; set; }
}
