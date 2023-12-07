using Egw.PubManagement.Application.Services;
using Egw.PubManagement.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WemlToPdf.Services;

/// <summary> Cover fetcher </summary>
public class CoverFetcher : ICoverFetcher
{
    private readonly IStorageWrapper _storage;
    private readonly IDbContextFactory<PublicationDbContext> _dbContextFactory;

    /// <summary> Default constructor </summary>
    /// <param name="storage">Storage wrapper</param>
    /// <param name="dbContextFactory">DB Context Factory</param>
    public CoverFetcher(
        IStorageWrapper storage,
        IDbContextFactory<PublicationDbContext> dbContextFactory)
    {
        _storage = storage;
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<byte[]?> FetchCover(int publicationId, CancellationToken ct)
    {
        await using PublicationDbContext db = await _dbContextFactory.CreateDbContextAsync(ct);
        Uri? coverUri = await db.Covers
            .Where(r => r.PublicationId == publicationId && r.TypeId == "web" && r.IsMain)
            .Select(r => r.Uri)
            .FirstOrDefaultAsync(ct);
        if (coverUri is null)
        {
            return null;
        }

        await using Stream? stream = await _storage.Covers.Read(coverUri.ToString(), ct);
        if (stream is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}