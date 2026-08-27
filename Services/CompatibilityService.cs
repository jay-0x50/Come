using Come.Models;

namespace Come.Services;

public sealed class CompatibilityService : ICompatibilityService
{
    public CompatibilityResult Evaluate(IEnumerable<PartItem> source)
    {
        var parts = source.ToDictionary(part => part.Category);
        var messages = new List<CompatibilityMessage>();
        parts.TryGetValue(PartCategory.Cpu, out var cpu);
        parts.TryGetValue(PartCategory.Mainboard, out var board);
        parts.TryGetValue(PartCategory.Memory, out var memory);
        parts.TryGetValue(PartCategory.Graphics, out var graphics);
        parts.TryGetValue(PartCategory.Case, out var pcCase);
        parts.TryGetValue(PartCategory.Cooler, out var cooler);
        parts.TryGetValue(PartCategory.Power, out var power);

        if (cpu is not null && board is not null && !EqualsIgnoreCase(cpu.Socket, board.Socket))
            messages.Add(Error(PartCategory.Cpu, "SOCKET_MISMATCH", "CPU와 메인보드의 소켓 규격이 맞지 않습니다."));

        if (memory is not null && board is not null && !EqualsIgnoreCase(memory.RamType, board.RamType))
            messages.Add(Error(PartCategory.Memory, "RAMTYPE_MISMATCH", "메모리 규격을 메인보드 지원 규격과 맞춰주세요."));

        if (board is not null && pcCase is not null && !pcCase.SupportedFormFactors.Any(value => EqualsIgnoreCase(value, board.FormFactor)))
            messages.Add(Error(PartCategory.Case, "FORMFACTOR_NOT_SUPPORTED", "선택한 케이스에 메인보드를 장착할 수 없습니다."));

        if (graphics is not null && pcCase is not null && graphics.LengthMm > pcCase.MaxGpuLengthMm)
            messages.Add(Error(PartCategory.Graphics, "GPU_TOO_LONG", $"그래픽카드 길이가 케이스 허용치 {pcCase.MaxGpuLengthMm}mm를 초과합니다."));

        if (cooler is not null && pcCase is not null && cooler.HeightMm > pcCase.MaxCoolerHeightMm)
            messages.Add(Error(PartCategory.Cooler, "COOLER_TOO_HIGH", $"CPU 쿨러 높이가 케이스 허용치 {pcCase.MaxCoolerHeightMm}mm를 초과합니다."));

        var totalPower = parts.Values.Where(part => part.Category != PartCategory.Power).Sum(part => part.PowerConsumptionW);
        var required = (int)Math.Ceiling(totalPower * 1.2 / 10d) * 10;
        if (power is null && parts.Count >= 3)
            messages.Add(new(PartCategory.Power, CompatibilitySeverity.Warning, "PSU_NOT_SELECTED", $"예상 소비전력 기준 {Math.Max(500, required)}W 이상의 파워를 권장합니다."));
        else if (power?.Wattage < required)
            messages.Add(Error(PartCategory.Power, "PSU_CAPACITY_LOW", $"안정적인 사용을 위해 {required}W 이상의 파워가 필요합니다."));

        return new CompatibilityResult(messages, messages.All(message => message.Severity != CompatibilitySeverity.Error));
    }

    private static CompatibilityMessage Error(PartCategory category, string code, string text) =>
        new(category, CompatibilitySeverity.Error, code, text);

    private static bool EqualsIgnoreCase(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
