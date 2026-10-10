using SalonManagement.Models;

namespace SalonManagement.Services
{
    public interface IAvailabilityService
    {
        Task<List<AvailabilitySlot>> GetAvailableSlotsAsync(
            DateTime date,
            List<int> serviceIds,
            int? stylistId = null);

        Task<List<DateTime>> FindNextAvailableDatesAsync(
            DateTime fromDate,
            int count,
            List<int> serviceIds,
            int? stylistId = null);
    }
}