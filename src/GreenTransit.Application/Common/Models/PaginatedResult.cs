namespace GreenTransit.Application.Common.Models;

/// <summary>Resultado paginado genérico para queries de listado.</summary>
public sealed class PaginatedResult<T>
{
    public IReadOnlyList<T> Items      { get; init; } = [];
    public int              TotalCount { get; init; }
    public int              PageNumber { get; init; }
    public int              PageSize   { get; init; }

    public int  TotalPages  => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext     => PageNumber < TotalPages;

    public static PaginatedResult<T> Create(
        IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        => new() { Items = items, TotalCount = totalCount, PageNumber = pageNumber, PageSize = pageSize };
}
