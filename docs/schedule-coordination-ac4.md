# AC 4 — Danh sách khách cần liên hệ

## Hành vi

- Khi lễ tân tải danh sách điều phối theo thợ/ngày, hệ thống tính lại gợi ý và lưu lịch chưa có thợ/khung giờ phù hợp vào `ScheduleContactFollowUps`.
- Lịch chuyển thất bại được ghi vào cùng danh sách nhưng giữ phân loại `TransferFailed`; lịch không có phương án dùng `NoAlternative`. Đồng bộ gợi ý không ghi đè lý do thất bại.
- Khi một lịch được chuyển thành công hoặc đã được áp dụng trước đó, bản ghi liên hệ của lịch đó được gỡ. Nếu lịch từng ở nhóm chưa có phương án nhưng nay có phương án trở lại, bản ghi nhóm đó cũng được gỡ.
- `GET /api/schedule-coordination/follow-ups` trả tên khách, điện thoại, dịch vụ, ngày/giờ hẹn gốc, lý do, phân loại và tình trạng liên hệ; hỗ trợ lọc theo ngày, trạng thái liên hệ và phân loại.
- `PUT /api/schedule-coordination/follow-ups/{id}/contact` lưu trạng thái `Pending`/`Contacted`, ghi chú và thời điểm đã liên hệ.
- Trang riêng `/reception/schedule-coordination/follow-ups` cho phép lễ tân lọc danh sách, gọi theo số điện thoại và lưu kết quả liên hệ.

## Lưu trữ và triển khai

- Bảng mới có khóa ngoại duy nhất theo lịch hẹn, lưu bản chụp ngày/giờ hẹn ban đầu để giờ hiển thị không đổi sau khi dữ liệu lịch bị cập nhật.
- Áp dụng migration `20261011000000_AddScheduleContactFollowUps` trước khi dùng API mới. Script triển khai tổng hợp `SalonManagement/Data/Migrations/migration.sql` cũng đã được cập nhật.
- Lát này chưa gửi email đổi thợ và chưa đáp ứng thời hạn gửi email 5 phút.
