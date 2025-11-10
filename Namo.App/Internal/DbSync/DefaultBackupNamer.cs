using Namo.Domain.DBSync;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Namo.App.Internal.DbSync;
internal sealed class DefaultBackupNamer : IBackupNamer
{
    private const string UnknownAppliedAt = "unknown";
    private const string TimestampFormatUtc = "yyyyMMdd'T'HHmmss'Z'";

    public string GetName(BackupNamingContext context)
    {
        // Keep everything in UTC for determinism
        var version = string.IsNullOrWhiteSpace(context.RemoteVersionId)
            ? "createdLocally"
            : Sanitize(context.RemoteVersionId!);

        var appliedAt = context.AppliedAtUtc.HasValue
            ? context.AppliedAtUtc.Value.ToString(TimestampFormatUtc, CultureInfo.InvariantCulture)
            : UnknownAppliedAt;

        var now = DateTime.UtcNow.ToString(TimestampFormatUtc, CultureInfo.InvariantCulture);

        // Also sanitize reason to be safe
        var reason = Sanitize(context.Reason ?? "unspecified");

        var fileName = $"backup.{reason}.{version}.{appliedAt}__to__{now}.db";
        return fileName;
    }

    private static string Sanitize(string value)
    {
        // Replace invalid filename chars with '-'
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('-');
            else if (char.IsWhiteSpace(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }

        // Collapse duplicate separators for neatness
        var sanitized = Regex.Replace(sb.ToString(), "[-_]{2,}", "-");
        return string.IsNullOrWhiteSpace(sanitized) ? "none" : sanitized;
    }
}
