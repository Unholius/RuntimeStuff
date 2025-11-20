using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RuntimeStuff.Options;

namespace RuntimeStuff.DI
{
    public static class FilterBuilderOptionsExtensions
    {
        public static IServiceCollection AddFilterBuilderOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<FilterBuilderOptions>(config.GetSection("FilterBuilder"));
            return services;
        }
    }
}