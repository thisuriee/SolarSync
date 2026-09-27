/*
 * File:    PagedResult.cs
 * Author:  Dahami
 * Created: 2026-09-27
 * Purpose: The paging envelope from docs/response-format.md
 *          ({items, page, pageSize, totalCount, totalPages}). Generic so every
 *          paged route (/prosumers, /reservations, /reservations/history)
 *          returns the identical shape.
 */
namespace SmartSolar.Api.Dtos;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }

    // Builds the envelope and derives totalPages (0 when there are no items).
    public static PagedResult<T> Create(List<T> items, int page, int pageSize, long totalCount)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
}
