using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Come.Models;

namespace Come.Services;

public sealed class TechFuelCatalogService : IRemotePartCatalogService
{
    private const string CatalogUrl = "https://techfuelhq.com/data/pc-builder-parts.json";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<RemoteCatalogResult> GetPartsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(CatalogUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var meta = root.GetProperty("_meta");
        var parts = new List<PartItem>(140);

        AddCategory(root, "cpu", PartCategory.Cpu, parts);
        AddCategory(root, "gpu", PartCategory.Graphics, parts);
        AddCategory(root, "motherboard", PartCategory.Mainboard, parts);
        AddCategory(root, "ram", PartCategory.Memory, parts);
        AddCategory(root, "storage", PartCategory.Storage, parts);
        AddCategory(root, "psu", PartCategory.Power, parts);
        AddCategory(root, "case", PartCategory.Case, parts);

        return new(parts, Text(meta, "version"), Text(meta, "published"));
    }

    private static void AddCategory(JsonElement root, string propertyName, PartCategory category, ICollection<PartItem> output)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array) return;
        foreach (var item in array.EnumerateArray()) output.Add(Map(item, category));
    }

    private static PartItem Map(JsonElement item, PartCategory category)
    {
        var name = Text(item, "model");
        var vendor = Text(item, "vendor");
        var tier = Text(item, "performance_tier");
        var verified = Text(item, "last_verified");
        var source = Text(item, "source");
        var specs = BuildSpecifications(item);
        var tags = Array(item, "use_cases")
            .Prepend(category switch
            {
                PartCategory.Graphics => "GPU", PartCategory.Mainboard => "메인보드",
                PartCategory.Memory => "RAM", PartCategory.Storage => "SSD",
                PartCategory.Power => "파워", PartCategory.Case => "케이스", _ => "CPU"
            })
            .Append(tier)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        var performance = tier.ToLowerInvariant() switch
        {
            "flagship" => 98, "high" => 90, "mid" => 78, "entry" => 64, _ => 74
        };

        return new PartItem
        {
            Id = $"techfuel-{Text(item, "id")}",
            Category = category,
            Name = name,
            Manufacturer = vendor,
            Price = 0,
            Stock = 0,
            SpecSummary = Summary(item, category),
            DetailSummary = Text(item, "notes") is { Length: > 0 } notes ? notes : $"{vendor} {name} 공식 사양 기반 제품 데이터",
            Accent = Accent(category),
            Glyph = Glyph(category),
            PowerConsumptionW = category switch
            {
                PartCategory.Cpu => Number(item, "tdp_w"),
                PartCategory.Graphics => Number(item, "tgp_w"),
                PartCategory.Mainboard => 50,
                PartCategory.Memory => 10,
                PartCategory.Storage => 8,
                PartCategory.Case => 5,
                _ => 0
            },
            Popularity = Math.Min(99, performance + 1),
            Performance = performance,
            Tags = tags,
            Specifications = specs,
            Socket = TextOrNull(item, "socket"),
            RamType = TextOrNull(item, "ram_type"),
            FormFactor = category == PartCategory.Mainboard ? TextOrNull(item, "form_factor") : null,
            SupportedFormFactors = Array(item, "supports_form_factors"),
            LengthMm = NullableNumber(item, "length_mm"),
            MaxGpuLengthMm = NullableNumber(item, "max_gpu_length_mm"),
            MaxCoolerHeightMm = NullableNumber(item, "max_cpu_cooler_mm"),
            Wattage = NullableNumber(item, "wattage_w"),
            DataSource = "TechFuelHQ API · CC BY 4.0",
            SourceUrl = string.IsNullOrWhiteSpace(source) ? CatalogUrl : source,
            LastVerified = verified,
            IsCatalogOnly = true
        };
    }

    private static string Summary(JsonElement item, PartCategory category) => category switch
    {
        PartCategory.Cpu => $"{Number(item, "cores")}코어 {Number(item, "threads")}스레드 · 최대 {Decimal(item, "boost_ghz")}GHz · {Number(item, "tdp_w")}W",
        PartCategory.Graphics => $"{Number(item, "vram_gb")}GB VRAM · {Number(item, "length_mm")}mm · {Number(item, "tgp_w")}W",
        PartCategory.Mainboard => $"{Text(item, "socket")} · {Text(item, "ram_type")} · {Text(item, "form_factor")}",
        PartCategory.Memory => $"{Text(item, "ram_type")} · {Number(item, "capacity_gb")}GB · {Number(item, "speed_mtps")}MT/s",
        PartCategory.Storage => $"{Number(item, "capacity_gb")}GB · {Text(item, "interface")} · 읽기 {Number(item, "read_mbps"):N0}MB/s",
        PartCategory.Power => $"{Number(item, "wattage_w")}W · {Text(item, "efficiency")} · {Text(item, "form_factor")}",
        PartCategory.Case => $"{Text(item, "form_factor")} · GPU {Number(item, "max_gpu_length_mm")}mm · 쿨러 {Number(item, "max_cpu_cooler_mm")}mm",
        _ => string.Empty
    };

    private static IReadOnlyDictionary<string, string> BuildSpecifications(JsonElement item)
    {
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "vendor", "model", "notes", "source", "last_verified", "use_cases", "performance_tier"
        };
        var result = new Dictionary<string, string>();
        foreach (var property in item.EnumerateObject())
        {
            if (hidden.Contains(property.Name)) continue;
            var value = Display(property.Value);
            if (!string.IsNullOrWhiteSpace(value)) result[Label(property.Name)] = value;
            if (result.Count == 7) break;
        }
        return result;
    }

    private static string Display(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => "지원",
        JsonValueKind.False => "미지원",
        JsonValueKind.Array => string.Join(" / ", element.EnumerateArray().Select(Display)),
        _ => string.Empty
    };

    private static string Label(string key) => key switch
    {
        "cores" => "코어", "threads" => "스레드", "boost_ghz" => "최대 클럭 (GHz)",
        "tdp_w" => "TDP (W)", "socket" => "소켓", "ram_type" => "메모리 규격",
        "vram_gb" => "VRAM (GB)", "tgp_w" => "TGP (W)", "length_mm" => "길이 (mm)",
        "recommended_psu_w" => "권장 파워 (W)", "form_factor" => "폼팩터", "chipset" => "칩셋",
        "m2_slots" => "M.2 슬롯", "capacity_gb" => "용량 (GB)", "speed_mtps" => "속도 (MT/s)",
        "cas_latency" => "CAS 지연", "interface" => "인터페이스", "read_mbps" => "읽기 (MB/s)",
        "write_mbps" => "쓰기 (MB/s)", "wattage_w" => "정격 출력 (W)", "efficiency" => "효율",
        "modular" => "모듈러", "max_gpu_length_mm" => "GPU 최대 길이 (mm)",
        "max_cpu_cooler_mm" => "쿨러 최대 높이 (mm)", "supports_form_factors" => "지원 보드",
        _ => key.Replace('_', ' ')
    };

    private static string Accent(PartCategory category) => category switch
    {
        PartCategory.Cpu => "#FF6B57", PartCategory.Mainboard => "#8E73FF",
        PartCategory.Memory => "#36D5C8", PartCategory.Graphics => "#73E16C",
        PartCategory.Storage => "#FFB84D", PartCategory.Power => "#F6C95C",
        PartCategory.Case => "#C98B64", _ => "#5BB8FF"
    };

    private static string Glyph(PartCategory category) => category switch
    {
        PartCategory.Cpu => "CPU", PartCategory.Mainboard => "MB", PartCategory.Memory => "RAM",
        PartCategory.Graphics => "GPU", PartCategory.Storage => "SSD", PartCategory.Power => "PSU",
        PartCategory.Case => "CASE", _ => "FAN"
    };

    private static string Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string? TextOrNull(JsonElement item, string property) => Text(item, property) is { Length: > 0 } value ? value : null;

    private static int Number(JsonElement item, string property) => NullableNumber(item, property) ?? 0;

    private static int? NullableNumber(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        return value.TryGetInt32(out var number) ? number : (int)Math.Round(value.GetDouble());
    }

    private static string Decimal(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return "0";
        return value.GetDouble().ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> Array(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Select(Display).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
    }
}
