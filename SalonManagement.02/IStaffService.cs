using System.Threading.Tasks;

namespace SalonManagement.StaffManagement
{
    public interface IStaffService
    {
        // ĐÃ THÊM actorId: int vào 3 method dưới đây để ghi audit log
        // (Id của admin đang đăng nhập thực hiện thao tác)

        Task<ServiceResponseModel> CreateStaffAsync(CreateStaffDto dto, int actorId);

        Task<ServiceResponseModel> UpdateStaffAsync(int id, UpdateStaffDto dto, int actorId);

        // newStatus = false -> deactivate (sẽ chạy rule bảo vệ Admin cuối cùng)
        // newStatus = true  -> activate
        Task<ServiceResponseModel> ChangeStatusAsync(int id, bool newStatus, int actorId);

        Task<ServiceResponseModel> GetStaffListAsync(StaffFilterQueryDto queryDto);

        Task<ServiceResponseModel> GetStaffByIdAsync(int id);

        // ===== METHOD MỚI: BƯỚC 5 - REVOKE SESSION =====
        // Tăng TokenVersion của user lên 1 -> mọi token cũ (mang version thấp hơn)
        // sẽ bị từ chối ở lần request tiếp theo.
        Task<ServiceResponseModel> RevokeSessionAsync(int id, int actorId);

        // ===== METHOD MỚI: BƯỚC 4 - ĐỔI MẬT KHẨU LẦN ĐẦU =====
        Task<ServiceResponseModel> ChangePasswordFirstTimeAsync(int userId, ChangePasswordFirstTimeDto dto);
    }
}