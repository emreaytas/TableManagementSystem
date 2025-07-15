using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TableManagement.Application.Services;
using FluentValidation;
using AutoMapper;
using TableManagement.Application.Mappings;

namespace TableManagement.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // AutoMapper - Explicit configuration to avoid ambiguity
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<AutoMapperProfile>();
            });

            var mapper = mapperConfig.CreateMapper();
            services.AddSingleton<IMapper>(mapper);

            // FluentValidation
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // Services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITableService, TableService>();
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}