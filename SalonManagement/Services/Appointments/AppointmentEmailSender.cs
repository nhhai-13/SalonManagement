using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public class AppointmentEmailSender(
    ApplicationDbContext db,
    IEmailService emailService,
    ILogger<AppointmentEmailSender> logger)
{
    public async Task SendConfirmationEmailAsync(int appointmentId)
    {
        // 1. Lấy thông tin lịch hẹn (bao gồm Khách, Thợ, Dịch vụ)
        var appointment = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

        if (appointment == null) return;

        // 2. Nếu đã gửi thành công rồi thì không gửi lại (Chặn gửi lần 2)
        if (appointment.ConfirmationEmailStatus == "Sent") return;

        // 3. Nếu khách không có email -> Ghi trạng thái NoEmail
        if (string.IsNullOrWhiteSpace(appointment.Customer.Email))
        {
            appointment.ConfirmationEmailStatus = "NoEmail";
            await db.SaveChangesAsync();
            return;
        }

        try
        {
            // 4. Chuẩn bị nội dung
            var servicesSummary = string.Join(", ", appointment.AppointmentServices.Select(s => s.Service.ServiceName));

            // 5. Gọi hàm gửi email có sẵn trong EmailService của nhóm
            await emailService.SendBookingConfirmationEmailAsync(
                toEmail: appointment.Customer.Email,
                customerName: appointment.Customer.FullName,
                bookingReference: appointment.BookingReference,
                servicesSummary: servicesSummary,
                stylistName: appointment.Stylist.FullName,
                appointmentDate: appointment.AppointmentDate,
                startTime: appointment.StartTime,
                endTime: appointment.EndTime
            );

            // 6. Cập nhật trạng thái thành công
            appointment.ConfirmationEmailStatus = "Sent";
            appointment.ConfirmationEmailSentAt = DateTime.Now;
            appointment.ConfirmationEmailError = null;
        }
        catch (Exception ex)
        {
            // 7. Cập nhật trạng thái thất bại nếu có lỗi
            logger.LogError(ex, "Lỗi gửi email xác nhận cho lịch hẹn {Id}", appointmentId);
            appointment.ConfirmationEmailStatus = "Failed";
            appointment.ConfirmationEmailError = ex.Message;
        }

        await db.SaveChangesAsync();
    }
}