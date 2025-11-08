using Amazon;
using Amazon.S3;

namespace Namo.Infrastructure.Cloud.S3;

public sealed class S3ClientOptions
{
    public string? ServiceUrl { get; set; }

    public bool ForcePathStyle { get; set; }

    public RegionEndpoint? Region { get; set; }

    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public string? SessionToken { get; set; }
}
