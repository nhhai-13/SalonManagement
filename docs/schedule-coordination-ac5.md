# AC 5 — Email thông báo sau khi chuyển lịch

## Quy tắc đã chốt

- Email gửi bằng tiếng Việt, có tên khách, dịch vụ, ngày hẹn, tên thợ cũ/mới, giờ cũ/mới và kênh liên hệ hỗ trợ.
- Subject và nội dung luôn nêu cả hai giá trị trước/sau; subject phân biệt đổi thợ, đổi giờ hoặc đổi cả hai.
- Chỉ tạo outbox khi khách có email. Chuyển lịch thất bại và yêu cầu chuyển đã áp dụng trước đó không tạo thông báo mới.
- Một lần chuyển thành công tạo một bản ghi outbox trong cùng transaction với cập nhật lịch. Khóa duy nhất theo `AppointmentId` + `Version` ngăn tạo trùng trong cùng phiên bản.
- Worker chạy nền kiểm tra hàng đợi mỗi 5 giây. Chính sách gửi là tối đa 5 lần, timeout mỗi lần 10 giây, khoảng chờ thử lại 15, 30, 60 và 120 giây. Lỗi cuối được lưu với trạng thái `Failed`; lần gửi thành công lưu thời điểm `SentAt`.
- Mục tiêu 5 phút được kiểm thử trong điều kiện gửi bình thường. Sự cố SMTP/mạng có thể làm email chuyển sang trạng thái thử lại hoặc thất bại; hệ thống không cam kết SLA tuyệt đối ngoài khả năng kiểm soát.

## Triển khai

- Hàng đợi lưu snapshot người nhận, dịch vụ, thợ/giờ cũ mới, ngày chuyển, phiên bản, trạng thái, số lần thử, lỗi cuối và thời điểm gửi.
- Cấu hình SMTP qua `ScheduleChangeEmail:Smtp`; mật khẩu chỉ cấu hình bằng biến môi trường `ScheduleChangeEmail__Smtp__Password` hoặc secret manager. Cấu hình `SupportPhone`/`SupportEmail` dùng trong template.
- `GET /api/schedule-coordination/notifications/{id}` trả trạng thái gửi cho giao diện kết quả chuyển lịch; giao diện tự cập nhật trạng thái đang chờ/gửi/đã gửi/thất bại.
- Áp dụng migration `20261011010000_AddScheduleChangeNotificationOutbox` trên SQL Server. SQLite development dùng schema khởi tạo/tự bổ sung trong `Program.cs`.
- Trước khi chạy môi trường thật, cần điền SMTP host, sender address và thông tin liên hệ hỗ trợ. Không có mật khẩu SMTP nào được lưu trong cấu hình mặc định.
