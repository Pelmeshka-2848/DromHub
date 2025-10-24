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
    /// Сервис доступа к истории изменений брендов и каталога.
    /// </summary>
    public class ChangeLogService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        /// <summary>
        /// Создаёт экземпляр сервиса.
        /// </summary>
        public ChangeLogService(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        /// <summary>
        /// Возвращает историю изменений, релевантную конкретному бренду.
        /// </summary>
        public async Task<IReadOnlyList<ChangeLogPatchResult>> GetBrandHistoryAsync(Guid brandId, CancellationToken cancellationToken = default)
        {
            var history = await LoadHistoryAsync(cancellationToken);

            var filtered = history
                .Select(patch => patch with
                {
                    Sections = patch.Sections
                        .Select(section => section with
                        {
                            Entries = section.Entries
                                .Where(entry =>
                                    entry.BrandId == brandId ||
                                    entry.PartBrandId == brandId ||
                                    (entry.BrandId is null && entry.PartId is null))
                                .ToList()
                        })
                        .Where(section => section.Entries.Count > 0)
                        .ToList()
                })
                .Where(patch => patch.Sections.Count > 0)
                .ToList();

            return filtered;
        }

        /// <summary>
        /// Возвращает полную историю изменений без фильтрации.
        /// </summary>
        public Task<IReadOnlyList<ChangeLogPatchResult>> GetGlobalHistoryAsync(CancellationToken cancellationToken = default)
            => LoadHistoryAsync(cancellationToken);

        private async Task<IReadOnlyList<ChangeLogPatchResult>> LoadHistoryAsync(CancellationToken cancellationToken)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var history = await db.ChangeLogPatches
                .AsNoTracking()
                .OrderBy(patch => patch.SortOrder)
                .ThenByDescending(patch => patch.ReleaseDate)
                .Select(patch => new ChangeLogPatchResult(
                    patch.Id,
                    patch.Version,
                    patch.Title,
                    patch.ReleaseDate,
                    patch.Sections
                        .OrderBy(section => section.SortOrder)
                        .Select(section => new ChangeLogSectionResult(
                            section.Id,
                            section.Title,
                            section.Category,
                            section.Entries
                                .OrderBy(entry => entry.SortOrder)
                                .Select(entry => new ChangeLogEntryResult(
                                    entry.Id,
                                    entry.Headline,
                                    entry.Description,
                                    entry.ImpactLevel,
                                    entry.IconAsset,
                                    entry.BrandId,
                                    entry.Brand != null ? entry.Brand.Name : null,
                                    entry.PartId,
                                    entry.Part != null ? entry.Part.Name : null,
                                    entry.Part != null ? entry.Part.CatalogNumber : null,
                                    entry.Part != null ? (Guid?)entry.Part.BrandId : null
                                ))
                                .ToList()
                        ))
                        .ToList()
                ))
                .ToListAsync(cancellationToken);

            return history;
        }
    }

    /// <summary>
    /// DTO патча истории изменений.
    /// </summary>
    /// <param name="PatchId">Идентификатор патча.</param>
    /// <param name="Version">Версия релиза.</param>
    /// <param name="Title">Название релиза.</param>
    /// <param name="ReleaseDate">Дата релиза.</param>
    /// <param name="Sections">Разделы патча.</param>
    public record ChangeLogPatchResult(Guid PatchId, string Version, string? Title, DateTime ReleaseDate, List<ChangeLogSectionResult> Sections);

    /// <summary>
    /// DTO раздела патча.
    /// </summary>
    /// <param name="SectionId">Идентификатор раздела.</param>
    /// <param name="Title">Заголовок.</param>
    /// <param name="Category">Категория изменений.</param>
    /// <param name="Entries">Записи раздела.</param>
    public record ChangeLogSectionResult(Guid SectionId, string Title, ChangeLogCategory Category, List<ChangeLogEntryResult> Entries);

    /// <summary>
    /// DTO записи истории изменений.
    /// </summary>
    /// <param name="EntryId">Идентификатор записи.</param>
    /// <param name="Headline">Заголовок.</param>
    /// <param name="Description">Описание.</param>
    /// <param name="ImpactLevel">Уровень влияния.</param>
    /// <param name="IconAsset">Иконка.</param>
    /// <param name="BrandId">Бренд, к которому относится изменение.</param>
    /// <param name="BrandName">Название бренда.</param>
    /// <param name="PartId">Деталь.</param>
    /// <param name="PartName">Название детали.</param>
    /// <param name="PartCatalogNumber">Каталожный номер детали.</param>
    /// <param name="PartBrandId">Идентификатор бренда, к которому относится деталь.</param>
    public record ChangeLogEntryResult(
        Guid EntryId,
        string? Headline,
        string Description,
        ChangeLogImpactLevel ImpactLevel,
        string? IconAsset,
        Guid? BrandId,
        string? BrandName,
        Guid? PartId,
        string? PartName,
        string? PartCatalogNumber,
        Guid? PartBrandId);
}
