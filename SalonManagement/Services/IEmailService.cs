namespace SalonManagement.Services;
public interface IEmailService
{
    Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword);
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode);
    Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime)
        => throw new NotSupportedException("Appointment change email sender is not registered.");
    Task SendRescheduleNotificationEmailAsync(string toEmail, string customerName, string stylistName,
        DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, string servicesSummary,
        string? reason = null, string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
        string salonHotline = "1900 1234")
        => throw new NotSupportedException("Reschedule email sender is not registered.");
}