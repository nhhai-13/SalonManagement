using System;

namespace SalonManagement.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        // Thời điểm thực hiện thao tác
        public DateTime Timestamp { get; set; }

        // Tài khoản thực hiện thao tác
        public string? UserId { get; set; }

        public string? UserName { get; set; }

        // CREATE / UPDATE / DELETE
        public string Action { get; set; } = string.Empty;

        // ApplicationUser / Service / WorkSchedule / Appointment / Invoice
        public string EntityType { get; set; } = string.Empty;

        // ID của đối tượng bị thay đổi
        public string? EntityId { get; set; }

        // Tóm tắt thay đổi
        public string? Changes { get; set; }
    }
}