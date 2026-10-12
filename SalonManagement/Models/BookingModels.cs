using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record CreateBookingRequest(
    [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [param: Required, StringLength(30, MinimumLength = 7)] string Phone,
    [param: Range(1, int.MaxValue)] int ServiceId,
    [param: Range(1, int.MaxValue)] int StylistId,
    DateOnly AppointmentDate,
    TimeOnly StartTime);

public sealed record BookingOption(int Id, string Name);

public sealed record BookingOptionsResponse(
    IReadOnlyList<BookingOption> Services,
    IReadOnlyList<BookingOption> Stylists);

public sealed record CreateBookingResponse(
    int AppointmentId,
    int CustomerId,
    bool IsReturningCustomer,
    string Status,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ServiceName,
    string StylistName);

public sealed class BookingConflictException(string message) : InvalidOperationException(message);

public sealed class BookingValidationException(string message) : ArgumentException(message);
