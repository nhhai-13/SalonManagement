using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed class ServiceGroupInput
{
    public int ServiceGroupId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập tên nhóm.")]
    [StringLength(100, ErrorMessage = "Tên nhóm tối đa 100 ký tự.")]
    public string GroupName { get; set; } = string.Empty;
    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị phải từ 0 trở lên.")]
    public int DisplayOrder { get; set; }
}
