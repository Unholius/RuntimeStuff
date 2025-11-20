using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RuntimeStuff.Builders;
using RuntimeStuff.Options;

namespace RuntimeStuff.DI
{
    public static class FilterBuilderExtensions
    {
        public static IServiceCollection AddFilterBuilder(this IServiceCollection services)
        {
            services.AddTransient<FilterBuilder>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FilterBuilderOptions>>().Value;
                return new FilterBuilder(options);
            });

            return services;
        }
    }
}
