namespace Come.Models;

public sealed record SelectedPartLine(PartCategory Category, string CategoryName, string Glyph, PartItem Part);
public enum CompatibilitySeverity { Info, Warning, Error }
public sealed record CompatibilityMessage(PartCategory? Category, CompatibilitySeverity Severity, string Code, string Text);

public sealed class CompatibilityResult
{
    public static readonly CompatibilityResult Empty = new([], true);
    public CompatibilityResult(IReadOnlyList<CompatibilityMessage> messages, bool isCompatible)
    {
        Messages = messages;
        IsCompatible = isCompatible;
    }
    public IReadOnlyList<CompatibilityMessage> Messages { get; }
    public bool IsCompatible { get; }
    public bool HasWarnings => Messages.Any(message => message.Severity == CompatibilitySeverity.Warning);
}

public sealed record PaymentReceipt(string OrderNumber, string ApprovalNumber, DateTime PaidAt);
