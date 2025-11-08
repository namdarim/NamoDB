using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Namo.Domain.Contracts.Cloud;

namespace Namo.Infrastructure.Cloud.S3;

public static class S3ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionedS3Client(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<S3ClientOptions>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = options.ForcePathStyle
            };

            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
            }

            if (options.Region is not null)
            {
                config.RegionEndpoint = options.Region;
            }

            var hasAccessKey = !string.IsNullOrEmpty(options.AccessKeyId);
            var hasSecretKey = !string.IsNullOrEmpty(options.SecretAccessKey);

            if (hasAccessKey != hasSecretKey)
            {
                throw new InvalidOperationException("Both AccessKeyId and SecretAccessKey must be provided to configure S3 credentials.");
            }

            if (!hasAccessKey)
            {
                return new AmazonS3Client(config);
            }

            if (!string.IsNullOrEmpty(options.SessionToken))
            {
                var sessionCredentials = new SessionAWSCredentials(options.AccessKeyId!, options.SecretAccessKey!, options.SessionToken);
                return new AmazonS3Client(sessionCredentials, config);
            }

            var basicCredentials = new BasicAWSCredentials(options.AccessKeyId!, options.SecretAccessKey!);
            return new AmazonS3Client(basicCredentials, config);
        });

        services.AddSingleton<IVersionedObjectClient, S3VersionedObjectClient>();

        return services;
    }
}
