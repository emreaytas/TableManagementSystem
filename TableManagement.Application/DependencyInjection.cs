using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TableManagement.Application.Services;
using FluentValidation;

namespace TableManagement.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // AutoMapper
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

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