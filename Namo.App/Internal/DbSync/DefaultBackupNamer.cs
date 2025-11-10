using Namo.Domain.DBSync;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Namo.App.Internal.DbSync;

internal class DefaultBackupNamer : IBackupNamer
{
    public string GetName(BackupNamingContext context)
    {
        var version = (context.RemoteVersionId != null ? Sanitize(context.RemoteVersionId) : "createdLocally");
        var appliedAt = context.AppliedAtUtc.ToLocalTime().ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
        var now = DateTime.Now.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
        var fileName = $"backup.{context.Reason}.{version}.{appliedAt}__to__{now}.db";
        return fileName;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        return value
            .Replace(':', '-')
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace(' ', '_');
    }
}
