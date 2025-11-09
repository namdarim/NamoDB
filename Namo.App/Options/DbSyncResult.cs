namespace Namo.App.DBSync;

/// <summary>
/// Flattened result for UI: success flag and a user-facing message (optional).
/// </summary>
public sealed record DbSyncResult(bool Succeeded, string? Message);
