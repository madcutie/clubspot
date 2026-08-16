using ClubSpot.Domain.Core;

namespace ClubSpot.Api.Auth;

public static class AuthorizationPolicies
{
    public const string PeopleView = "people.view";
    public const string PeopleManage = "people.manage";
    public const string AgendaOperate = "agenda.operate";
    public const string ConfigurationEdit = "configuration.edit";

    public static IServiceCollection AddClubSpotAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(PeopleView, policy => policy.RequireRole(Role.Administrator.ToString(), Role.MemberDesk.ToString(), Role.CourtReception.ToString()))
            .AddPolicy(PeopleManage, policy => policy.RequireRole(Role.Administrator.ToString(), Role.MemberDesk.ToString()))
            .AddPolicy(AgendaOperate, policy => policy.RequireRole(Role.Administrator.ToString(), Role.CourtReception.ToString()))
            .AddPolicy(ConfigurationEdit, policy => policy.RequireRole(Role.Administrator.ToString()));
        return services;
    }
}
