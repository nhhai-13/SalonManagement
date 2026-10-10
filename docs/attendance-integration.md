# Tích hợp S3-03 và S3-06

Hai nhánh riêng: `codex/s3-03`, `codex/s3-06`. Nền tích hợp đã kiểm tra: `origin/develop` (`d9282f4`). Không merge hoặc push vào nhánh của nhóm.

Mỗi nhánh merge sạch với develop và main. Hai nhánh merge với nhau sạch; bản ghép vượt qua 165 tests và cả hai bộ HTTP regression trên SQL Server LocalDB. Nhánh S3-03 riêng: 156 tests; S3-06 riêng: 154 tests.

Giữ xác thực JWT/cookie, controller đặt lịch và nhân sự của develop. Controller attendance và đăng ký dịch vụ nằm trong file riêng. Hai nhánh dùng chung migration `20261008135432_AppointmentAttendance`; không tạo lại cùng schema với ID khác. S3-06 cần một thay đổi ở StylistAvailabilityService để giải phóng lịch NoShow.

## Xung đột còn có thể xảy ra

Kiểm tra các remote hiện tại bằng `git merge-tree --write-tree --name-only`:

- `feature/s3-04/reschedule-modal-and-slot-validation`: sạch.
- Các nhánh S3-04 audit-trail-history, calendar-drag-and-drop, email-notification-queue: xung đột ApplicationDbContextModelSnapshot.cs.
- `code-full-product-backlog--sprint-2`: snapshot và Appointment.cs.
- `codex/update-sprint-2`: snapshot, Appointment.cs và _Layout.cshtml.
- `codex/sprint2-booking-fixes`: như trên; riêng S3-06 thêm StylistAvailabilityService.cs.

Không thể bảo đảm mọi nhánh chưa tích hợp đều không xung đột. Khi nhóm đưa các nhánh này vào develop, cập nhật lại hai nhánh attendance và chạy kiểm thử trước khi merge. Với Appointment.cs, giữ cả thuộc tính đặt lịch của nhóm lẫn trường attendance. Với availability, giữ quy tắc đặt lịch của nhóm và loại NoShow khỏi lịch bận. Với layout, đưa các partial _S303Nav/_S306Nav vào menu mới.

Snapshot là file EF sinh tự động: không chọn toàn bộ ours/theirs. Hợp nhất model và cấu hình DbContext trước, tạo lại snapshot/migration phù hợp schema cuối cùng; kiểm tra pending model changes và nâng cấp database thử nghiệm từ schema của nhóm. Giữ lịch sử migration đã chạy trên database thật.
