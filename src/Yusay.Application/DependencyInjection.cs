using Microsoft.Extensions.DependencyInjection;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.VerifyEmail;

namespace Yusay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<IVerifyEmailUseCase, VerifyEmailUseCase>();
        services.AddScoped<Identity.Commands.RequestPasswordReset.IRequestPasswordResetUseCase, Identity.Commands.RequestPasswordReset.RequestPasswordResetUseCase>();
        services.AddScoped<Identity.Commands.ResetPassword.IResetPasswordUseCase, Identity.Commands.ResetPassword.ResetPasswordUseCase>();

        return services;
    }
}
