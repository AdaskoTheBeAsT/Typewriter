namespace Issue145;

public sealed class CustomerModel
{
    public int Id { get; set; }

    public required string DisplayName { get; set; }

    public string AuditStamp { internal get; set; } = string.Empty;

    internal string InternalSecret { get; set; } = string.Empty;

    public const string PublicCode = "PUBLIC";

    internal const string InternalCode = "SECRET";
}

internal sealed class InternalSyncState
{
    public string Cursor { get; set; } = string.Empty;
}
