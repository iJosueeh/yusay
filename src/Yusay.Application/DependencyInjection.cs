using Microsoft.Extensions.DependencyInjection;

namespace Yusay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // En fases posteriores se registrarán Handlers, Validadores y Servicios de Dominio/Aplicación
        return services;
    }
}
