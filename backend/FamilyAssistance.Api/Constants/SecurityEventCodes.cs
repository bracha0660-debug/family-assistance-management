namespace FamilyAssistance.Api.Constants;

public static class SecurityEventCodes
{
    public const string LoginSuccess = "SEC-001";
    public const string LoginFailedInvalidCredentials = "SEC-002";
    public const string LoginFailedAccountInactive = "SEC-003";
    public const string LoginFailedRateLimited = "SEC-004";
    public const string Logout = "SEC-005";
}

public static class SecurityEventTypes
{
    public const string LoginSuccess = "login_success";
    public const string LoginFailedInvalidCredentials = "login_failed_invalid_credentials";
    public const string LoginFailedAccountInactive = "login_failed_account_inactive";
    public const string LoginFailedRateLimited = "login_failed_rate_limited";
    public const string Logout = "logout";
}
