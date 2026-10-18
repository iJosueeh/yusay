using Microsoft.Extensions.DependencyInjection;
using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Assessment.Commands.CreateInstrumentVersion;
using Yusay.Application.Assessment.Commands.MarkInstrumentVersionReady;
using Yusay.Application.Assessment.Commands.PublishInstrumentVersion;
using Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;
using Yusay.Application.Assessment.Commands.RetireInstrumentVersion;
using Yusay.Application.Assessment.Commands.ReturnInstrumentVersionToDraft;
using Yusay.Application.Assessment.Commands.UpdateInstrument;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentById;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentVersionById;
using Yusay.Application.Assessment.Queries.GetPublishedInstrumentById;
using Yusay.Application.Assessment.Queries.ListAdministrativeInstruments;
using Yusay.Application.Assessment.Queries.ListPublishedInstruments;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.SignOut;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Queries.ValidateAccessToken;
using Yusay.Application.Tracking.Commands.ActivateDimensionVersion;
using Yusay.Application.Tracking.Commands.CreateCheckIn;
using Yusay.Application.Tracking.Commands.CreateDimension;
using Yusay.Application.Tracking.Commands.CreateDimensionVersion;
using Yusay.Application.Tracking.Commands.DeleteCheckIn;
using Yusay.Application.Tracking.Commands.RetireDimensionVersion;
using Yusay.Application.Tracking.Commands.UpdateCheckIn;
using Yusay.Application.Tracking.Commands.UpdateDimension;
using Yusay.Application.Tracking.Commands.UpdateDimensionVersion;
using Yusay.Application.Tracking.Queries.GetCheckInById;
using Yusay.Application.Tracking.Queries.GetDimensionById;
using Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;
using Yusay.Application.Tracking.Queries.ListCheckIns;
using Yusay.Application.Tracking.Queries.ListDimensions;

namespace Yusay.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateInstrumentUseCase, CreateInstrumentUseCase>();
        services.AddScoped<IUpdateInstrumentUseCase, UpdateInstrumentUseCase>();
        services.AddScoped<ICreateInstrumentVersionUseCase, CreateInstrumentVersionUseCase>();
        services.AddScoped<IReplaceDraftDefinitionUseCase, ReplaceDraftDefinitionUseCase>();
        services.AddScoped<IMarkInstrumentVersionReadyUseCase, MarkInstrumentVersionReadyUseCase>();
        services.AddScoped<IReturnInstrumentVersionToDraftUseCase, ReturnInstrumentVersionToDraftUseCase>();
        services.AddScoped<IPublishInstrumentVersionUseCase, PublishInstrumentVersionUseCase>();
        services.AddScoped<IRetireInstrumentVersionUseCase, RetireInstrumentVersionUseCase>();
        services.AddScoped<IListAdministrativeInstrumentsUseCase, ListAdministrativeInstrumentsUseCase>();
        services.AddScoped<IGetAdministrativeInstrumentByIdUseCase, GetAdministrativeInstrumentByIdUseCase>();
        services.AddScoped<IGetAdministrativeInstrumentVersionByIdUseCase, GetAdministrativeInstrumentVersionByIdUseCase>();
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<IVerifyEmailUseCase, VerifyEmailUseCase>();
        services.AddScoped<IRequestPasswordResetUseCase, RequestPasswordResetUseCase>();
        services.AddScoped<IResetPasswordUseCase, ResetPasswordUseCase>();
        services.AddScoped<ISignInUseCase, SignInUseCase>();
        services.AddScoped<ISignOutUseCase, SignOutUseCase>();
        services.AddScoped<IValidateAccessTokenUseCase, ValidateAccessTokenUseCase>();
        services.AddScoped<ICreateCheckInUseCase, CreateCheckInUseCase>();
        services.AddScoped<IGetCheckInByIdUseCase, GetCheckInByIdUseCase>();
        services.AddScoped<IUpdateCheckInUseCase, UpdateCheckInUseCase>();
        services.AddScoped<IDeleteCheckInUseCase, DeleteCheckInUseCase>();
        services.AddScoped<IListCheckInsUseCase, ListCheckInsUseCase>();
        services.AddScoped<IListDimensionsUseCase, ListDimensionsUseCase>();
        services.AddScoped<IGetDimensionByIdUseCase, GetDimensionByIdUseCase>();
        services.AddScoped<IListAdministrativeDimensionsUseCase, ListAdministrativeDimensionsUseCase>();
        services.AddScoped<ICreateDimensionUseCase, CreateDimensionUseCase>();
        services.AddScoped<IUpdateDimensionUseCase, UpdateDimensionUseCase>();
        services.AddScoped<ICreateDimensionVersionUseCase, CreateDimensionVersionUseCase>();
        services.AddScoped<IUpdateDimensionVersionUseCase, UpdateDimensionVersionUseCase>();
        services.AddScoped<IActivateDimensionVersionUseCase, ActivateDimensionVersionUseCase>();
        services.AddScoped<IRetireDimensionVersionUseCase, RetireDimensionVersionUseCase>();
        services.AddScoped<IListPublishedInstrumentsUseCase, ListPublishedInstrumentsUseCase>();
        services.AddScoped<IGetPublishedInstrumentByIdUseCase, GetPublishedInstrumentByIdUseCase>();

        return services;
    }
}
