using System;
using System.Text.Json;

namespace DromHub.Models;

public class BrandAuditLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public Guid? BrandId { get; set; }
    public char Action { get; set; }           // 'I','U','D'
    public string[]? ChangedColumns { get; set; }

    // Храним как string для простоты сериализации/превью
    public string? OldData { get; set; }       // jsonb
    public string? NewData { get; set; }       // jsonb

    public string? Actor { get; set; }
    public string? AppContext { get; set; }
    public long TxId { get; set; }
    public DateTimeOffset EventTime { get; set; }

    // Сгенерированные (read-only)
    public string? OldText { get; set; }
    public string? NewText { get; set; }
}
