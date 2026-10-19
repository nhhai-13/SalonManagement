namespace SalonManagement.Services;
public interface IEmailService
{
    Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword);
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode);
 codex/sprint-4-todo

    Task SendBookingConfirmationEmailAsync(string toEmail, string customerName, string bookingReference,
        string servicesSummary, string stylistName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh") => Task.CompletedTask;
    Task SendAppointmentReminderEmailAsync(string toEmail, string customerName, string bookingReference,
        string servicesSummary, string stylistName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh") => Task.CompletedTask;
 develop
    Task SendReviewRequestEmailAsync(string toEmail, string customerName, string stylistName, string reviewUrl)
        => Task.CompletedTask;
    Task SendServiceReviewEmailAsync(string toEmail, string customerName, string reviewLink, DateTime expiresAt)
        => Task.CompletedTask;
    Task SendAppointmentNotificationEmailAsync(string toEmail, string customerName, string reference, string services,
        string stylistName, DateTime appointmentDate, TimeSpan startTime, bool isReminder)
        => Task.CompletedTask;
    Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime)
        => throw new NotSupportedException("Appointment change email sender is not registered.");
    Task SendRescheduleNotificationEmailAsync(string toEmail, string customerName, string stylistName,
        DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, string servicesSummary,
        string? reason = null, string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
        string salonHotline = "1900 1234")
        => throw new NotSupportedException("Reschedule email sender is not registered.");
}
