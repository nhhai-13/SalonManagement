# Sprint 3 acceptance evidence

Date: 2026-10-14

This report distinguishes automated and local evidence from validation that still requires the team's deployed staging environment.

| Item | Automated or local evidence | Result and limit |
| --- | --- | --- |
| S3-01 Reception day schedule | `ReceptionScheduleControllerTests` exercises a SQLite-backed schedule with 8 stylists and 60 appointments and asserts a load time below 2 seconds. The local Staging-mode page returned HTTP 200 for 14/10/2026. | Passed locally. This does not measure the deployed database or verify screenshots at 360px and 1366px. |
| S3-02 Online confirmation and rejection | `PendingOnlineAppointmentsTests` covers pending online appointments, confirmation/rejection and customer profile preservation. | Automated suite passed. Email delivery and overdue handling were not exercised against a deployed worker/SMTP service. |
| S3-03 Check-in | `CheckInTests` covers the -60/+60 minute boundary, late calculation, stylist notification, audit, duplicate requests and a SQLite concurrency write. | Automated tests passed. |
| S3-04 Reschedule and reassign | `AppointmentReschedulingServiceTests` and schedule notification tests cover skills, shift/conflict checks, audit/notification state and the five-minute simulated delivery window. | Automated tests passed. The five-minute test uses a test sender, not the team's SMTP service. |
| S3-05 Customer cancellation | `AppointmentLookupServiceTests` covers cancellation at and inside the 120-minute limit, release/audit behavior and repeat requests. | Automated tests passed. |
| S3-06 No-show | `NoShowTests` covers the 30-minute threshold, slot release, phone-based counts, audit and the 15-minute undo window. | Automated tests passed. |
| S3-07 Desk booking | `DeskBookingServiceTests` runs on SQLite and verifies profile-name confirmation/preservation, `Confirmed` status, `Source=Desk`, audit attribution and rejection when the stylist lacks the selected service. | Automated tests passed. |
| S3-08 Stylist work | `StylistWorkWorkflowTests` covers own appointments, masked phone, service start/complete, actual timestamps and audit. A SQLite-backed query test catches provider translation failures. Local Staging-mode checks returned 200 for the stylist API and 403 when a receptionist called it. | Passed locally. A human walkthrough on deployed staging remains outstanding. |
| S3-09 Absent-stylist transfer | `ScheduleTransferProcessorTests` covers partial batch failures and independent successful transfers. `ScheduleChangeNotificationTests` verifies the simulated email deadline. | Automated tests passed. Real customer email delivery remains unverified. |
| S3-10 Customer profile | `ReceptionCustomerDirectoryTests` searches 5,000 SQLite profiles under one second. Booking tests verify a new appointment does not silently replace an existing customer's name. The local Staging-mode check returned 403 for a stylist calling the receptionist directory. | Passed locally. The timing is local SQLite evidence, not a production-scale database benchmark. |

## Executed checks

- `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`: passed, 325/325 tests.
- `dotnet build SalonManagement/SalonManagement.csproj --no-restore`: passed, 0 errors.
- `dotnet ef migrations has-pending-model-changes --project SalonManagement` with Production environment: no pending model changes.
- Local Staging-mode SQLite smoke checks: receptionist login succeeded, schedule page returned 200, stylist API returned 200, and cross-role calls returned 403.

## GitHub review state

- On 2026-10-14, PR #54 targets `develop` from `feature/lookup-cancel` and GitHub reports it as ready to merge. Its Checks tab shows zero checks, so GitHub has no CI result for this PR.
- PR #53 has the same S3-05 title and source branch name, but targets `main` and contains 59 commits. Keep it separate from PR #54; merging both would not be a safe substitute for reviewing the intended `develop` PR.

## Evidence still required before merging the GitHub PR

1. Deploy this candidate to the team's shared staging service and verify it against that service's configured database.
2. Capture and review the schedule/customer/stylist pages at 360px and 1366px.
3. Measure the 8-stylist/60-appointment schedule and 5,000-customer search against the team's staging database.
4. Confirm real email delivery for reschedule and absent-stylist transfer within five minutes.
5. Complete the team's review and a live staging demo with seeded or approved test data.

The local SQLite Staging-mode run is a smoke test only. It does not establish any of the shared-staging or live-service evidence above.
