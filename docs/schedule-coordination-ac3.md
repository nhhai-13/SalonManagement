# AC 3 — Chọn và xác nhận chuyển lịch theo lô

## Hành vi

- Lễ tân chọn thợ/giờ cho từng lịch, đánh dấu khách đã đồng ý, rồi gửi một lần kiểm tra trước khi xác nhận.
- `POST /api/schedule-coordination/transfers/preview` đọc lại dữ liệu hiện tại, kiểm tra dịch vụ, ca làm, giờ mở cửa, trạng thái và lịch trùng. Các lựa chọn trong cùng đợt được kiểm tra theo thứ tự lễ tân đã chọn; lịch hợp lệ trước giữ chỗ trong bước preview.
- `POST /api/schedule-coordination/transfers` kiểm tra lại từng lịch ngay trước khi lưu. Mỗi lịch được lưu độc lập; lỗi ở lịch sau không hoàn tác lịch đã lưu trước đó. Với cơ sở dữ liệu quan hệ, bước kiểm tra xung đột và cập nhật chạy trong transaction `Serializable`.
- Chỉ cập nhật thợ, giờ bắt đầu/kết thúc và trạng thái sau khi lễ tân xác nhận khách đồng ý. Trạng thái sau chuyển là `Confirmed`.
- Gửi lại cùng lựa chọn sau khi lưu được trả về `AlreadyApplied`, không cập nhật lần hai. Lựa chọn trùng ID trong cùng một payload bị từ chối riêng.
- Phản hồi theo từng ID có trạng thái, thông báo/lý do và thợ/giờ dự kiến; giao diện tổng hợp thành công, thất bại và cho phép thử lại từng lịch lỗi.

## Quy tắc nghiệp vụ cần PO xác nhận

Chưa có xác nhận trực tiếp từ PO. Các quy tắc triển khai trên đã được người yêu cầu chấp thuận trong phiên làm việc: cập nhật từng lịch theo thứ tự chọn, cho phép kết quả một phần, kiểm tra lại dữ liệu tại thời điểm lưu và yêu cầu xác nhận khách trước khi đặt `Confirmed`. Transaction `Serializable` bảo vệ trường hợp hai yêu cầu đồng thời cùng giành một khung giờ trên SQL Server.

Không có bảng lưu lịch sử từng lần xác nhận trong schema hiện tại; kết quả trả trong phản hồi API. Việc gửi email, ghi nhận liên hệ/gọi lại và lưu audit history nằm ngoài phạm vi lát này.
