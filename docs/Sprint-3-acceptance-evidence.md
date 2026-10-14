# Sprint 3 acceptance evidence

Date: 2026-10-14

This report distinguishes automated and local evidence from validation that still requires the team's deployed staging environment.

| Item | Automated or local evidence | Result and limit |
| --- | --- | --- |
| S3-01 Reception day schedule | `ReceptionScheduleControllerTests` exercises a SQLite-backed schedule with 8 stylists and 60 appointments and asserts a load time below 2 seconds. The local Staging-mode page returned HTTP 200 for 14/10/2026. | Passed locally. This does not measure the deployed database or verify screenshots at 360px and 1366px. |
| S3-02 Online confirmation and rejection | `PendingOnlineAppointmentsTests` covers pending online appointments, confirmation/rejection and customer profile preservation. | Automated suite passed. Email delivery and overdue handling were not exercised against a deployed worker/SMTP service. |
| S3-03 Check-in | `CheckInTests` covers the -60/+60 minute boundary, early/on-time/1–15 minute late/16+ minute late arrivals, stylist notification, audit, duplicate requests and a SQLite concurrency write. | Front desk sees whether the customer arrived early, within the 15-minute grace window, or later; the booked time remains unchanged. |
| S3-04 Reschedule and reassign | `AppointmentReschedulingServiceTests` and schedule notification tests cover skills, shift/conflict checks, audit/notification state and the five-minute simulated delivery window. | Automated tests passed. The five-minute test uses a test sender, not the team's SMTP service. |
| S3-05 Customer cancellation | `AppointmentLookupServiceTests` covers cancellation at and inside the 120-minute limit, release/audit behavior and repeat requests. It verifies that exactly 120 minutes is allowed, the freed 10:00–11:00 slot is immediately listed again, and cancelled cards retain scheduled durations of 15, 30, 45, 60, 90 or 120 minutes even if the service snapshot says 30 minutes. | Cancelled appointments now show the cancellation time in Asia/Ho_Chi_Minh and identify the actor as the customer or salon staff. |
| S3-06 No-show | `NoShowTests` covers the 30-minute threshold, slot release, phone-based counts, audit and the 15-minute undo window. | Automated tests passed. |
| S3-07 Desk booking | `DeskBookingServiceTests` runs on SQLite and verifies profile-name confirmation/preservation, `Confirmed` status, `Source=Desk`, audit attribution and rejection when the stylist lacks the selected service. | Automated tests passed. |
| Booking flow regression | `BookingServiceTests.GetMaxShiftDurationMinutesAsync_WithSqlite_ReturnsLongestShift` covers SQLite shift-duration lookup, including an overnight row. Staging-mode smoke checks for `/booking` and `/booking/select-services` return HTTP 200. | Fixed an EF Core/SQLite `TimeSpan` comparison that previously caused HTTP 500 while loading booking services. |
| Public UI responsiveness | Manually reviewed `/booking` and `/appointments/lookup` at 360px and 1366px in the local Staging-mode app. Both pages fit their viewport without horizontal overflow. | Local UI evidence only; the reception schedule, customer directory and stylist portal still need review on deployed staging. |
| S3-08 Stylist work | `StylistWorkWorkflowTests` covers own appointments, masked phone, service start/complete, actual timestamps and audit. A SQLite-backed query test catches provider translation failures. Local Staging-mode checks returned 200 for the stylist API and 403 when a receptionist called it. | Passed locally. A human walkthrough on deployed staging remains outstanding. |
| S3-09 Absent-stylist transfer | `ScheduleTransferProcessorTests` covers partial batch failures and independent successful transfers. `ScheduleChangeNotificationTests` verifies the simulated email deadline. | Automated tests passed. Real customer email delivery remains unverified. |
| S3-10 Customer profile | `ReceptionCustomerDirectoryTests` searches 5,000 SQLite profiles under one second. Booking tests verify a new appointment does not silently replace an existing customer's name. The local Staging-mode check returned 403 for a stylist calling the receptionist directory. | Passed locally. The timing is local SQLite evidence, not a production-scale database benchmark. |

## Executed checks

- `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`: passed, 336/336 tests.
- `dotnet build SalonManagement/SalonManagement.csproj --no-restore`: passed, 0 errors.
- `dotnet ef migrations has-pending-model-changes --project SalonManagement` with Production environment: no pending model changes.
- Local Staging-mode SQLite smoke checks: receptionist login succeeded, schedule page returned 200, booking and service-selection pages returned 200 after the SQLite fix, stylist API returned 200, and cross-role calls returned 403.
- The fresh local seed has no stylist-to-service assignments. The booking page loads successfully, but selecting `Cắt tóc nam` shows no eligible stylist until staff/service mappings are configured. This is a local data-readiness limit, not evidence about the team's deployed database.
- `README.md` identifies only local Staging at `http://localhost:5129` and says no public staging deployment exists. There is no deployed team URL available in the repository for live checks.
- The repository has no `.github/workflows` CI definition, and PR #54's Checks tab has no workflow results.

## GitHub review state

- On 2026-10-14, PR #54 targets `develop` from `feature/lookup-cancel` and is still open. Its Checks tab has no workflow results. The local SQLite booking fix is on `codex/sprint3-integration-review`, not on PR #54's head branch.
- PR #53 has the same S3-05 title and source branch name, but targets `main` and contains 59 commits. Keep it separate from PR #54; merging both would not be a safe substitute for reviewing the intended `develop` PR.

## Evidence still required before merging the GitHub PR

1. The team needs to provide/deploy a shared staging service, then verify this candidate against its configured database.
2. Capture and review the reception schedule, customer directory and stylist portal at 360px and 1366px on deployed staging.
3. Measure the 8-stylist/60-appointment schedule and 5,000-customer search against the team's staging database.
4. Confirm real email delivery for reschedule and absent-stylist transfer within five minutes.
5. Complete the team's review and a live staging demo with seeded or approved test data.

The local SQLite Staging-mode run is a smoke test only. It does not establish any of the shared-staging or live-service evidence above.
