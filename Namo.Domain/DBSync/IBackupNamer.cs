using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.Domain.DBSync;

public interface IBackupNamer
{
    string GetName(BackupNamingContext context);
}