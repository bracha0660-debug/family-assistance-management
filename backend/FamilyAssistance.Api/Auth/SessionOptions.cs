namespace FamilyAssistance.Api.Auth;

public class FamSessionOptions
{
    public string CookieName { get; set; } = "FAM.Session";
    public int IdleTimeoutHours { get; set; } = 8;
    public int AbsoluteTimeoutHours { get; set; } = 12;
}
