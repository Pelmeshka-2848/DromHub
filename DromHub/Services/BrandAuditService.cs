using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DromHub.Data;
using Microsoft.EntityFrameworkCore;

namespace DromHub.Services
{
    public enum AuditActionFilter { All, Insert, Update, Delete }

    public sealed class BrandAuditFilter
    {
        public Guid BrandId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AuditActionFilter Action { get; set; } = AuditActionFilter.All;
        public string? Search { get; set; }
        public bool OnlyChangedFields { get; set; }
        public int PageIndex { get; set; } = 0;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Модель строки для UI (без with/foreach-assign, с display-полями).
    /// </summary>
    public sealed class BrandAuditRow
    {
        public Guid Id { get; init; }
        public DateTime Ts { get; init; }
        public string Action { get; init; } = "";
        public string User { get; init; } = "";
        public string Table { get; init; } = "";
        public Guid? BrandId { get; init; }
        public string? OldJson { get; init; }
        public string? NewJson { get; init; }
        public IReadOnlyList<string> ChangedColumns { get; init; } = Array.Empty<string>();

        // ---- DISPLAY ДЛЯ XAML (вместо StringFormat в Binding) ----
        public string TsDisplay => Ts.ToString("dd.MM.yyyy HH:mm");
        public string ActionDisplay => Action.ToUpperInvariant();
        public string ChangedColumnsJoined => ChangedColumns.Count == 0 ? "—" : string.Join(", ", ChangedColumns);
        public string UserDisplay => string.IsNullOrWhiteSpace(User) ? "—" : User;
        public string TableDisplay => string.IsNullOrWhiteSpace(Table) ? "—" : Table;
    }

    public sealed class BrandAuditService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public BrandAuditService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Возвращает (строки, всего) с применёнными фильтрами/сортировкой/пагинацией.
        /// </summary>
        public async Task<(IReadOnlyList<BrandAuditRow> Rows, int Total)> GetAsync(
            BrandAuditFilter filter,
            CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // Таблица аудита: brand_audit_log (создана SQL-триггером)
            // Столбцы предполагаются: id, ts, action, username, table_name, brand_id, old_data (jsonb), new_data (jsonb), changed_columns (text[])
            var q = db.Set<BrandAuditLogRow>().AsNoTracking().AsQueryable();

            // ---- FILTERS ----
            if (filter.BrandId != Guid.Empty)
                q = q.Where(x => x.BrandId == filter.BrandId);

            if (filter.From.HasValue)
                q = q.Where(x => x.Ts >= filter.From.Value);

            if (filter.To.HasValue)
                q = q.Where(x => x.Ts <= filter.To.Value);

            if (filter.Action != AuditActionFilter.All)
            {
                var act = filter.Action switch
                {
                    AuditActionFilter.Insert => "INSERT",
                    AuditActionFilter.Update => "UPDATE",
                    AuditActionFilter.Delete => "DELETE",
                    _ => null
                };
                if (act != null) q = q.Where(x => x.Action == act);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim();
                // простейший поиск по JSON: text-представление
                q = q.Where(x =>
                    (x.OldData != null && EF.Functions.ILike(x.OldDataText!, $"%{s}%")) ||
                    (x.NewData != null && EF.Functions.ILike(x.NewDataText!, $"%{s}%")));
            }

            if (filter.OnlyChangedFields)
            {
                q = q.Where(x => x.Action == "UPDATE" && x.ChangedColumns != null && x.ChangedColumns.Length > 0);
            }

            // ---- ORDER ----
            q = q.OrderByDescending(x => x.Ts);

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
                    Id = a.Id,
                    Ts = a.Ts,
                    Action = a.Action ?? "",
                    User = a.UserName ?? "",
                    Table = a.TableName ?? "",
                    BrandId = a.BrandId,
                    OldJson = a.OldDataText,
                    NewJson = a.NewDataText,
                    ChangedColumns = a.ChangedColumns ?? Array.Empty<string>()
                });
            }

            return (rows, total);
        }

        /// <summary>
        /// Внутренняя EF-модель чтения из brand_audit_log (только для сервиса).
        /// </summary>
        private sealed class BrandAuditLogRow
        {
            public Guid Id { get; set; }
            public DateTime Ts { get; set; }
            public string? Action { get; set; }
            public string? UserName { get; set; }
            public string? TableName { get; set; }
            public Guid? BrandId { get; set; }

            // jsonb в БД мапим на string? + теневые-свойства для поиска
            public string? OldDataText { get; set; }
            public string? NewDataText { get; set; }

            public string[]? ChangedColumns { get; set; }

            // Эти поля пометим как не сопоставленные EF, если используете Fluent API
            public object? OldData { get; set; }
            public object? NewData { get; set; }
        }
    }
}
