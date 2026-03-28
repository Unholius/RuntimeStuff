//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Options;
//using System.Linq.Expressions;

//namespace System.DI
//{
//    public static class FilterBuilderExtensions
//    {
//        public static IServiceCollection AddFilterBuilder(this IServiceCollection services)
//        {
//            services.AddTransient(sp =>
//            {
//                var options = sp.GetRequiredService<IOptions<FilterBuilderOptions>>().Value;
//                return new StringFilterBuilder(options);
//            });

//            return services;
//        }
//    }
//}