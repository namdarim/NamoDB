using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namo.Infrastructure.DBSync;

public sealed class S3Settings
{
    public required string ServiceUrl { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string Bucket { get; init; }
    public required string Key { get; init; }
    public bool ForcePathStyle { get; init; } = false;
}