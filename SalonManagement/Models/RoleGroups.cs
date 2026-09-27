namespace SalonManagement.Models;

public static class RoleGroups
{
    public const string InternalStaff =
        UserRoles.Admin + "," +
        UserRoles.Owner + "," +
        UserRoles.Receptionist + "," +
        UserRoles.Stylist;

    public const string Management =
        UserRoles.Admin + "," +
        UserRoles.Owner;

    public const string FrontDesk =
        UserRoles.Admin + "," +
        UserRoles.Owner + "," +
        UserRoles.Receptionist;

    public const string AllUsers =
        UserRoles.Admin + "," +
        UserRoles.Owner + "," +
        UserRoles.Receptionist + "," +
        UserRoles.Stylist + "," +
        UserRoles.Customer;
}