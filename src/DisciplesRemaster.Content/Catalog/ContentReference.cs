using System.Text.RegularExpressions;

namespace DisciplesRemaster.Content.Catalog;

public readonly partial record struct ContentReference
{
    private ContentReference(string packageId, string localId)
    {
        PackageId = packageId;
        LocalId = localId;
        Value = $"{packageId}:{localId}";
    }

    public string PackageId { get; }

    public string LocalId { get; }

    public string Value { get; }

    public static bool TryParse(string? value, out ContentReference reference)
    {
        reference = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 161)
        {
            return false;
        }

        int separator = value.IndexOf(':');
        if (separator <= 0 || separator != value.LastIndexOf(':'))
        {
            return false;
        }

        string packageId = value[..separator];
        string localId = value[(separator + 1)..];
        if (!IsValidPackageId(packageId) || !IsValidLocalId(localId))
        {
            return false;
        }

        reference = new ContentReference(packageId, localId);
        return true;
    }

    public static ContentReference Parse(string value) =>
        TryParse(value, out ContentReference reference)
            ? reference
            : throw new FormatException("Content reference must use the project-owned package:local-id syntax.");

    public override string ToString() => Value ?? string.Empty;

    internal static bool IsValidPackageId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= ContentPackageFormatV1.MaximumPackageIdLength &&
        PackageIdPattern().IsMatch(value);

    internal static bool IsValidLocalId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= ContentPackageFormatV1.MaximumLocalIdLength &&
        LocalIdPattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalIdPattern();
}
