namespace WemlToPdf.Services;

/// <summary> Cover fetcher </summary>
public interface ICoverFetcher
{
    /// <summary> Fetch cover </summary>
    /// <param name="publicationId">Publication ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task<byte[]?> FetchCover(int publicationId, CancellationToken ct);
}