namespace SalonManagement.Services;
public static partial class AttendanceServiceRegistration
{
    public static IServiceCollection AddAttendanceModules(this IServiceCollection services)
    {
        AddCheckIn(services);
        AddNoShow(services);
        return services;
    }
    static partial void AddCheckIn(IServiceCollection services);
    static partial void AddNoShow(IServiceCollection services);
}
