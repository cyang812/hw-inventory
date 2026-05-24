namespace HwInventory.Api.Models;

/// <summary>
/// Hardware condition: physical/operational state of an item.
/// </summary>
public enum HardwareCondition
{
    Working,
    Partial,
    Broken,
    Unknown,
}

/// <summary>
/// Hardware lifecycle status. "Idle" is intentionally absent — it is a derived
/// view based on <see cref="Hardware.LastUsedAt"/>, not a stored state.
/// </summary>
public enum HardwareStatus
{
    Available,
    InUse,
    Loaned,
    Archived,
    Sold,
    Lost,
}

public enum ProjectStatus
{
    Idea,
    Planned,
    InProgress,
    Paused,
    Done,
    Abandoned,
}

public enum ProjectPriority
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Activity kinds. The <see cref="ActivityKindMeta"/> helper decides which kinds
/// bump LastUsedAt vs LastActivityAt.
/// </summary>
public enum ActivityKind
{
    Used,
    Flashed,
    Repaired,
    Measured,
    Configured,
    Inspected,
    Moved,
    Note,
}

public enum HardwareConfigKind
{
    Firmware,
    Os,
    Bootloader,
    Config,
}

public enum AttachmentStorageBackend
{
    Local,
    S3,
}

public static class ActivityKindMeta
{
    /// <summary>Kinds that update both LastUsedAt and LastActivityAt.</summary>
    public static bool UpdatesLastUsed(ActivityKind kind) => kind switch
    {
        ActivityKind.Used or
        ActivityKind.Flashed or
        ActivityKind.Repaired or
        ActivityKind.Measured or
        ActivityKind.Configured => true,
        _ => false,
    };
}
