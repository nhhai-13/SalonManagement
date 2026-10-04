namespace SalonManagement.Models.ViewModels;

public sealed class HomeViewModel
{
    public IReadOnlyList<BusinessHour> BusinessHours { get; init; } = [];
    public IEnumerable<string> OpeningHours => BusinessHours.Select(h =>
        $"{(h.DayOfWeek == DayOfWeek.Sunday ? "Chủ Nhật" : $"Thứ {(int)h.DayOfWeek + 1}")}: " +
        (h.IsClosed || h.OpensAt == null || h.ClosesAt == null || h.OpensAt >= h.ClosesAt
            ? "Đóng cửa" : $"{h.OpensAt:HH:mm} – {h.ClosesAt:HH:mm}"));
    public IReadOnlyList<Service> Services { get; init; } = [];
    public IReadOnlyList<Stylist> Stylists { get; init; } = [];
}
