namespace StayDesk.Application.Auth;

public static class Roles
{
    public const string Guest = "Guest";
    public const string Staff = "Staff";

    // Admin users also hold Staff (SRS §2), so StaffOrAdmin checks only need Staff.
    public const string Admin = "Admin";

    public static readonly string[] All = [Guest, Staff, Admin];
}
