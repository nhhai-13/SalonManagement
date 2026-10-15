using System.ComponentModel.DataAnnotations;
namespace SalonManagement.Models;
public class DeskBookingRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên khách."), StringLength(150)]
    public string FullName { get; set; } = "";
    [Required, RegularExpression(@"0[0-9]{9}", ErrorMessage = "Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.")]
    public string Phone { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thợ.")]
    public int StylistId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn dịch vụ.")]
    public int ServiceId { get; set; }
    public DateTime StartsAt { get; set; }
    public bool AcknowledgeNoShow { get; set; }
}
