using Come.Models;

namespace Come.Services;

public interface IRemotePartCatalogService
{
    Task<RemoteCatalogResult> GetPartsAsync(CancellationToken cancellationToken = default);
}

public sealed record RemoteCatalogResult(IReadOnlyList<PartItem> Parts, string Version, string Published);
