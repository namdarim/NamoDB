using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Namo.Domain.Contracts.Cloud;

namespace Namo.Infrastructure.Cloud.S3;

public static class S3ServiceCollectionExtensions
{
    public static IServiceCollection AddVersionedS3Client(this IServiceCollection services, Action<S3ClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
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

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IVersionedObjectClient, S3VersionedObjectClient>();

        return services;
    }
}
