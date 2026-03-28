//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using System.Options;

//namespace System.DI
//{
//    public static class FilterBuilderOptionsExtensions
//    {
//        public static IServiceCollection AddFilterBuilderOptions(this IServiceCollection services, IConfiguration config)
//        {
//            services.Configure<FilterBuilderOptions>(config.GetSection("StringFilterBuilder"));
//            return services;
//        }
//    }
//}