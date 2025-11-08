using Amazon;
using Amazon.S3;

namespace Namo.Infrastructure.Cloud.S3;

public sealed class S3ClientOptions
{
    public string? ServiceUrl { get; set; }

    public bool ForcePathStyle { get; set; }

    public RegionEndpoint? Region { get; set; }
}
