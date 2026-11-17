namespace StayDesk.Application.Paging;

// SRS §6: every paged endpoint returns this shape.
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public const int MaxPageSize = 100;

    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

public enum SortDirection
{
    Asc,
    Desc,
}
