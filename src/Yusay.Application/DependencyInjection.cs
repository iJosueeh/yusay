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
        services.AddScoped<Identity.Commands.SignIn.ISignInUseCase, Identity.Commands.SignIn.SignInUseCase>();
        services.AddScoped<Identity.Commands.SignOut.ISignOutUseCase, Identity.Commands.SignOut.SignOutUseCase>();
        services.AddScoped<Identity.Queries.ValidateAccessToken.IValidateAccessTokenUseCase, Identity.Queries.ValidateAccessToken.ValidateAccessTokenUseCase>();
        services.AddScoped<Tracking.Commands.CreateCheckIn.ICreateCheckInUseCase, Tracking.Commands.CreateCheckIn.CreateCheckInUseCase>();
        services.AddScoped<Tracking.Queries.GetCheckInById.IGetCheckInByIdUseCase, Tracking.Queries.GetCheckInById.GetCheckInByIdUseCase>();
        services.AddScoped<Tracking.Commands.UpdateCheckIn.IUpdateCheckInUseCase, Tracking.Commands.UpdateCheckIn.UpdateCheckInUseCase>();
        services.AddScoped<Tracking.Commands.DeleteCheckIn.IDeleteCheckInUseCase, Tracking.Commands.DeleteCheckIn.DeleteCheckInUseCase>();
        services.AddScoped<Tracking.Queries.ListCheckIns.IListCheckInsUseCase, Tracking.Queries.ListCheckIns.ListCheckInsUseCase>();
        services.AddScoped<Tracking.Queries.ListDimensions.IListDimensionsUseCase, Tracking.Queries.ListDimensions.ListDimensionsUseCase>();
        services.AddScoped<Tracking.Queries.GetDimensionById.IGetDimensionByIdUseCase, Tracking.Queries.GetDimensionById.GetDimensionByIdUseCase>();
        services.AddScoped<Tracking.Queries.ListAdministrativeDimensions.IListAdministrativeDimensionsUseCase, Tracking.Queries.ListAdministrativeDimensions.ListAdministrativeDimensionsUseCase>();
        services.AddScoped<Tracking.Commands.CreateDimension.ICreateDimensionUseCase, Tracking.Commands.CreateDimension.CreateDimensionUseCase>();
        services.AddScoped<Tracking.Commands.UpdateDimension.IUpdateDimensionUseCase, Tracking.Commands.UpdateDimension.UpdateDimensionUseCase>();
        services.AddScoped<Tracking.Commands.CreateDimensionVersion.ICreateDimensionVersionUseCase, Tracking.Commands.CreateDimensionVersion.CreateDimensionVersionUseCase>();
        services.AddScoped<Tracking.Commands.UpdateDimensionVersion.IUpdateDimensionVersionUseCase, Tracking.Commands.UpdateDimensionVersion.UpdateDimensionVersionUseCase>();
        services.AddScoped<Tracking.Commands.ActivateDimensionVersion.IActivateDimensionVersionUseCase, Tracking.Commands.ActivateDimensionVersion.ActivateDimensionVersionUseCase>();
        services.AddScoped<Tracking.Commands.RetireDimensionVersion.IRetireDimensionVersionUseCase, Tracking.Commands.RetireDimensionVersion.RetireDimensionVersionUseCase>();

        return services;
    }
}
