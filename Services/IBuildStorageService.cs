namespace Come.Services;
public interface IBuildStorageService
{
    void Save(IEnumerable<string> partIds);
    IReadOnlyList<string> Load();
}
