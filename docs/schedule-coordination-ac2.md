# AC 2 — Gợi ý thợ thay thế và giờ hẹn

## Phạm vi triển khai

API lịch điều phối trả thêm đánh giá cho từng thợ đang hoạt động, gồm khả năng nhận lịch đúng giờ ban đầu, lý do không phù hợp và tối đa ba giờ bắt đầu thay thế. Giao diện hiển thị thợ phù hợp, giờ thay thế và lý do không tìm được phương án. Đây là chức năng gợi ý chỉ đọc; chưa xác nhận chuyển lịch, lưu kết quả, hay gửi email.

## Quy tắc tạm áp dụng — cần PO xác nhận

- **Kỹ năng:** dùng trường `Stylists.Specialty` hiện có làm danh sách kỹ năng, cho phép nhiều kỹ năng phân cách bằng dấu phẩy, chấm phẩy, gạch chéo hoặc `|`. Kỹ năng phải khớp tên dịch vụ hoặc nhóm dịch vụ; so sánh không phân biệt hoa thường và dấu tiếng Việt. Lịch nhiều dịch vụ yêu cầu thợ khớp tất cả dịch vụ.
- **Thời lượng:** lấy giá trị lớn hơn giữa khoảng đặt lịch (`EndTime - StartTime`) và tổng thời lượng các dịch vụ trên lịch để tránh xếp lịch quá sát.
- **Ca làm:** chỉ dùng ca có `WorkSchedules.Status = Working` trong ngày được chọn. Thợ không có ca làm được xem là không khả dụng.
- **Xung đột:** các lịch cùng thợ giao nhau về thời gian đều chiếm chỗ, trừ lịch hoàn tất, hủy hoặc khách không đến. Lịch đang thực hiện vì thế vẫn chặn thời gian.
- **Giờ mở cửa:** khung đề xuất phải nằm trong ca thợ và giờ mở cửa salon. Nếu chưa cấu hình giờ salon, dùng mặc định 08:00–17:00 theo API giờ làm việc hiện tại.
- **Dời giờ:** chỉ tìm trong cùng ngày, bước 15 phút, tối đa ba lựa chọn đầu tiên cho mỗi thợ. Chưa có cấu hình khoảng dời lịch nên không đề xuất sang ngày khác.
- **Di chuyển:** không cộng thời gian di chuyển vì dữ liệu hiện không lưu địa điểm giữa các cơ sở/lịch.

Các giả định trên cần PO chốt trước khi coi gợi ý là quyết định nghiệp vụ cuối cùng. Mã chuyên môn hiện là chuỗi tự do, không phải bảng kỹ năng đã chuẩn hóa; những hồ sơ không khớp sẽ không được đề xuất.

## API và kiểm thử

- `GET /api/schedule-coordination/appointments?stylistId={id}&date=YYYY-MM-DD` trả thêm `plan` theo từng lịch: danh sách thợ, khả dụng tại giờ cũ, lý do và `suggestedStartTimes`.
- Kiểm thử planner bao gồm kỹ năng, ca làm, giờ mở cửa, thời lượng, xung đột lịch, khung giờ cùng ngày và trường hợp không có phương án.
