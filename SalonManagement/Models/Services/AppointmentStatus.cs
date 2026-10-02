namespace SalonManagement.Models
{
    /// <summary>
    /// Các trạng thái của Appointment. Status "bận" chiếm slot của thợ.
    /// </summary>
    public static class AppointmentStatus
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string CheckedIn = "CheckedIn";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string NoShow = "NoShow";

        /// <summary>
        /// Các status được coi là "bận" — chiếm slot của thợ.
        /// Cancelled và NoShow không tính là bận.
        /// </summary>
        public static readonly string[] BusyStatuses =
        {
            Pending, Confirmed, CheckedIn, InProgress, Completed
        };
    }
}