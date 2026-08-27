namespace Come.Models;

public sealed record class PartItem
{
    public required string Id { get; init; }
    public required PartCategory Category { get; init; }
    public required string Name { get; init; }
    public required string Manufacturer { get; init; }
    public required decimal Price { get; init; }
    public required int Stock { get; init; }
    public required string SpecSummary { get; init; }
    public required string DetailSummary { get; init; }
    public required string Accent { get; init; }
    public required string Glyph { get; init; }
    public int PowerConsumptionW { get; init; }
    public int Popularity { get; init; }
    public int Performance { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyDictionary<string, string> Specifications { get; init; } = new Dictionary<string, string>();
    public string? Socket { get; init; }
    public string? RamType { get; init; }
    public string? FormFactor { get; init; }
    public IReadOnlyList<string> SupportedFormFactors { get; init; } = [];
    public int? LengthMm { get; init; }
    public int? HeightMm { get; init; }
    public int? MaxGpuLengthMm { get; init; }
    public int? MaxCoolerHeightMm { get; init; }
    public int? Wattage { get; init; }
    public string DataSource { get; init; } = "COME 선별 카탈로그";
    public string SourceUrl { get; init; } = string.Empty;
    public string LastVerified { get; init; } = string.Empty;
    public bool IsCatalogOnly { get; init; }

    public bool CanPurchase => !IsCatalogOnly && Price > 0 && Stock > 0;
    public string PriceDisplay => Price > 0 ? $"{Price:N0}원" : "가격 미제공";
    public string StockDisplay => IsCatalogOnly ? "사양·3D 미리보기 전용" : Stock > 0 ? $"재고 {Stock}개" : "품절 · 미리보기 전용";
    public string SelectionActionText => CanPurchase ? "선택" : "미리보기";
    public string SourceBadge => IsCatalogOnly ? "LIVE DATA" : string.IsNullOrWhiteSpace(LastVerified) ? "COME PICK" : "VERIFIED";
    public string VerificationDisplay => string.IsNullOrWhiteSpace(LastVerified) ? DataSource : $"{DataSource} · {LastVerified} 검증";
    public string Model3DFile => Category switch
    {
        PartCategory.Cpu => "cpu.glb",
        PartCategory.Mainboard => "motherboard.glb",
        PartCategory.Memory => "ram.glb",
        PartCategory.Graphics => "gpu.glb",
        PartCategory.Storage => "m.2.glb",
        PartCategory.Power => "psu.glb",
        PartCategory.Case => "system_unit_update.glb",
        PartCategory.Cooler => "cpu_fan.glb",
        _ => "system_unit_update.glb"
    };

    public string CategoryName => Category switch
    {
        PartCategory.Cpu => "CPU", PartCategory.Mainboard => "메인보드",
        PartCategory.Memory => "메모리", PartCategory.Graphics => "그래픽카드",
        PartCategory.Storage => "스토리지", PartCategory.Power => "파워",
        PartCategory.Case => "케이스", PartCategory.Cooler => "쿨러", _ => Category.ToString()
    };

    public string SearchText => $"{Name} {Manufacturer} {string.Join(' ', Tags)}".ToLowerInvariant();
}
