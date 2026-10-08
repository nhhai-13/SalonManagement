namespace SalonManagement.Services;
public static partial class AttendanceServiceRegistration
{
    static partial void AddNoShow(IServiceCollection services)
    {
        services.AddScoped<NoShowService>();
        services.AddScoped<DeskBookingService>();
    }
}
