namespace SalonManagement.Services;
public static partial class AttendanceServiceRegistration
{
    static partial void AddCheckIn(IServiceCollection services)
    {
        services.AddScoped<CheckInService>();
        services.AddSingleton<AppointmentNotificationBus>();
    }
}
