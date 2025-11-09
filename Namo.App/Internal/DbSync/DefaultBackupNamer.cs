using Namo.Domain.DBSync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.App.Internal.DbSync;

internal class DefaultBackupNamer : IBackupNamer
{
    public string GetName(BackupNamingContext context)
    {
        throw new NotImplementedException();
    }
}
