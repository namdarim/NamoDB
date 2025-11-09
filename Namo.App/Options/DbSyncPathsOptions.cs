using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.App.Options;

public sealed class DbSyncPathsOptions
{
    public string LocalDbPath { get; set; } = "";
    public string SnapshotDir { get; set; } = "";
    public string? ManifestPath { get; set; }
}
