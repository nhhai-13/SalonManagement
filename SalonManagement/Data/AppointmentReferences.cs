using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;
using System.Security.Cryptography;
namespace SalonManagement.Data;
public partial class ApplicationDbContext
{
    private async Task AssignMissingBookingReferencesAsync(CancellationToken cancellationToken)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var added = ChangeTracker.Entries<Appointment>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        var reserved = added.Where(a => !string.IsNullOrWhiteSpace(a.BookingReference)).Select(a => a.BookingReference).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var appointment in added.Where(a => string.IsNullOrWhiteSpace(a.BookingReference)))
        {
            string reference;
            do
            {
                reference = string.Concat(Enumerable.Range(0, 8).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]));
            } while (reserved.Contains(reference) || await Appointments.AnyAsync(a => a.BookingReference == reference, cancellationToken));
            appointment.BookingReference = reference;
            reserved.Add(reference);
        }
    }
}