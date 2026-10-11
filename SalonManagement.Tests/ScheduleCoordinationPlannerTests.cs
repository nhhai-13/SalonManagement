using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ScheduleCoordinationPlannerTests
{
    private static readonly DateOnly Day = new(2026, 10, 11);
    private static readonly BusinessHourResponse OpenHours = new(
        DayOfWeek.Sunday, false, new TimeOnly(8, 0), new TimeOnly(17, 0), BusinessHour.SalonTimeZone);

    [Fact]
    public void BuildPlan_RecommendsQualifiedStylistWhoIsWorkingAndFreeAtOriginalTime()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], OpenHours);

        var option = Assert.Single(plan.Stylists);
        Assert.True(option.AvailableAtOriginalTime);
        Assert.Empty(option.ReasonsUnavailableAtOriginalTime);
        Assert.True(plan.HasOptions);
    }

    [Fact]
    public void BuildPlan_ExcludesStylistWithoutRequiredServiceSkill()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ uốn", "Uốn tóc", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], OpenHours);

        var option = Assert.Single(plan.Stylists);
        Assert.False(option.AvailableAtOriginalTime);
        Assert.Contains(option.ReasonsUnavailableAtOriginalTime, reason => reason.Contains("Cắt tóc nam"));
        Assert.Empty(option.SuggestedStartTimes);
        Assert.False(plan.HasOptions);
    }

    [Fact]
    public void BuildPlan_ReportsCollisionAndFindsOtherSameDayTimeSlots()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        var existing = BookedAppointment(candidate, id: 55, start: new TimeSpan(9, 30, 0), duration: 60);

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment, existing], OpenHours);

        var option = Assert.Single(plan.Stylists);
        Assert.False(option.AvailableAtOriginalTime);
        Assert.Contains(option.ReasonsUnavailableAtOriginalTime, reason => reason.Contains("#55"));
        Assert.Equal(new[] { "08:00", "08:15", "08:30" }, option.SuggestedStartTimes);
        Assert.True(plan.HasOptions);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("NoShow")]
    public void BuildPlan_DoesNotTreatTerminalAppointmentsAsConflicts(string status)
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        var terminal = BookedAppointment(candidate, id: 55, start: new TimeSpan(9, 0, 0), duration: 60);
        terminal.Status = status;

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment, terminal], OpenHours);

        Assert.True(Assert.Single(plan.Stylists).AvailableAtOriginalTime);
    }

    [Fact]
    public void BuildPlan_DoesNotSuggestOutsideStylistShiftOrSalonHours()
    {
        var appointment = BookedAppointment(start: new TimeSpan(7, 30, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0));
        var earlySalonHours = OpenHours with { OpensAt = new TimeOnly(8, 30), ClosesAt = new TimeOnly(9, 0) };

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], earlySalonHours);

        var option = Assert.Single(plan.Stylists);
        Assert.False(option.AvailableAtOriginalTime);
        Assert.Empty(option.SuggestedStartTimes);
        Assert.Contains(option.ReasonsUnavailableAtOriginalTime, reason => reason.Contains("ngoài ca làm"));
        Assert.False(plan.HasOptions);
    }

    [Fact]
    public void BuildPlan_AllowsOriginalTimeInsideTheSalonEvenWhenShiftExtendsBeyondOpeningHours()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(7, 0, 0), new TimeSpan(18, 0, 0));

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], OpenHours);

        Assert.True(Assert.Single(plan.Stylists).AvailableAtOriginalTime);
    }

    [Fact]
    public void BuildPlan_UsesLongestReservedOrServiceDurationForAvailability()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 30);
        appointment.AppointmentServices.Single().DurationMinutes = 90;
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0));

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], OpenHours);

        Assert.Equal(90, ScheduleCoordinationPlanner.GetDuration(appointment));
        Assert.False(Assert.Single(plan.Stylists).AvailableAtOriginalTime);
        Assert.Empty(Assert.Single(plan.Stylists).SuggestedStartTimes);
    }

    [Fact]
    public void BuildPlan_ReturnsNoOptionsWhenAllStylistsAreOffOrUnqualified()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var offShift = Stylist(2, "Thợ nghỉ", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), "DayOff");
        var unqualified = Stylist(3, "Thợ khác", "Uốn tóc", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [offShift, unqualified], [appointment], OpenHours);

        Assert.False(plan.HasOptions);
        Assert.Equal(2, plan.Stylists.Count);
        Assert.All(plan.Stylists, option => Assert.NotEmpty(option.ReasonsUnavailableAtOriginalTime));
    }

    [Fact]
    public void BuildPlan_RequiresAWorkShiftForTheSelectedDate()
    {
        var appointment = BookedAppointment(start: new TimeSpan(9, 0, 0), duration: 60);
        var candidate = Stylist(2, "Thợ cắt", "Cắt tóc nam", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        candidate.WorkSchedules.Single().WorkDate = Day.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var plan = ScheduleCoordinationPlanner.BuildPlan(
            appointment, 1, Day, [candidate], [appointment], OpenHours);

        Assert.False(plan.HasOptions);
        Assert.Contains(Assert.Single(plan.Stylists).ReasonsUnavailableAtOriginalTime,
            reason => reason.Contains("chưa được xếp ca"));
    }

    private static Stylist Stylist(
        int id,
        string name,
        string? specialty,
        TimeSpan shiftStart,
        TimeSpan shiftEnd,
        string status = "Working") => new()
    {
        StylistId = id,
        FullName = name,
        Specialty = specialty,
        IsActive = true,
        WorkSchedules =
        [
            new WorkSchedule
            {
                StylistId = id,
                WorkDate = Day.ToDateTime(TimeOnly.MinValue),
                StartTime = shiftStart,
                EndTime = shiftEnd,
                Status = status
            }
        ]
    };

    private static Appointment BookedAppointment(
        Stylist? stylist = null,
        int id = 10,
        TimeSpan? start = null,
        int duration = 60) => new()
    {
        AppointmentId = id,
        StylistId = stylist?.StylistId ?? 1,
        Stylist = stylist ?? new Stylist { StylistId = 1, FullName = "Thợ nghỉ" },
        AppointmentDate = Day.ToDateTime(TimeOnly.MinValue),
        StartTime = start ?? new TimeSpan(9, 0, 0),
        EndTime = (start ?? new TimeSpan(9, 0, 0)).Add(TimeSpan.FromMinutes(duration)),
        Status = "Confirmed",
        AppointmentServices =
        [
            new AppointmentService
            {
                DurationMinutes = duration,
                Service = new Service
                {
                    ServiceName = "Cắt tóc nam",
                    DurationMinutes = duration,
                    ServiceGroup = new ServiceGroup { GroupName = "Cắt tóc" }
                }
            }
        ]
    };
}
