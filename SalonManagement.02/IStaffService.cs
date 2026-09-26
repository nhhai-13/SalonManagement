using System.Threading.Tasks;

namespace SalonManagement.StaffManagement
{
    public interface IStaffService
    {
        Task<ServiceResponseModel> CreateStaffAsync(CreateStaffDto dto);
        Task<ServiceResponseModel> UpdateStaffAsync(int id, UpdateStaffDto dto);
        Task<ServiceResponseModel> ChangeStatusAsync(int id, bool newStatus);
        Task<ServiceResponseModel> GetStaffListAsync(StaffFilterQueryDto queryDto);
        Task<ServiceResponseModel> GetStaffByIdAsync(int id);
    }
}