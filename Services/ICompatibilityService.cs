using Come.Models;

namespace Come.Services;
public interface ICompatibilityService { CompatibilityResult Evaluate(IEnumerable<PartItem> parts); }
