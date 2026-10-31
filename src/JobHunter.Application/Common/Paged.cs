namespace JobHunter.Application.Common;

/// <summary>The list envelope of every paged endpoint (spec 03, conventions).</summary>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Clamp(int page, int pageSize, int defaultPageSize = 30) =>
        (Math.Max(1, page), pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, MaxPageSize));
}
