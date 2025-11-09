using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.Domain.DBSync;

public sealed class SyncState
{
    public string? LastAppliedVersionId { get; private set; }

    public SyncState(string? lastAppliedVersionId)
    {
        LastAppliedVersionId = string.IsNullOrWhiteSpace(lastAppliedVersionId) ? null : lastAppliedVersionId;
    }

    public void Update(string newVersionId)
    {
        LastAppliedVersionId = newVersionId ?? throw new ArgumentNullException(nameof(newVersionId));
    }
}
