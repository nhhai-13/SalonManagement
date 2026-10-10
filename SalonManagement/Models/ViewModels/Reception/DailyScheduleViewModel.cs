namespace SalonManagement.Models.ViewModels.Reception;

public sealed record DailyScheduleViewModel(DateTime Date, TimeSpan OpensAt, TimeSpan ClosesAt, IReadOnlyList<DailyScheduleStylist> Stylists, IReadOnlyList<DailyScheduleAppointment> Appointments);
public sealed record DailyScheduleStylist(int StylistId, string Name, TimeSpan StartTime, TimeSpan EndTime);
public sealed record DailyScheduleAppointment(int AppointmentId, int StylistId, string CustomerName, string Services, TimeSpan StartTime, TimeSpan EndTime, string Status);
