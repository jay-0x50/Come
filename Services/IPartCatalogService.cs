using Come.Models;

namespace Come.Services;
public interface IPartCatalogService { IReadOnlyList<PartItem> GetAll(); }
