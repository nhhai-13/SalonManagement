using SalonManagement.Models;

namespace SalonManagement.Services;

public static class SalonClock
{
    public static DateTime GetLocalNow(TimeProvider timeProvider)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(BusinessHour.SalonTimeZone);
        return TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime;
    }
}
