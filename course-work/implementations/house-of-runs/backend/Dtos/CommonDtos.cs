namespace HouseOfRuns.Api.Dtos;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class Paging
{
    public static int NormalizePage(int page) => Math.Max(1, page);

    public static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize, 1, 500);
}
