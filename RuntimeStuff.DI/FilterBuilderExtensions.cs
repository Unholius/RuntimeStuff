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
            services.AddTransient<StringFilterBuilder>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FilterBuilderOptions>>().Value;
                return new StringFilterBuilder(options);
            });

            return services;
        }
    }
}
