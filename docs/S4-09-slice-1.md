# S4-09 — Lát 1 (AC2)

Màn hình: `/admin/appointment-analytics`. API GET: `/api/appointment-analytics?StartDate=2026-09-01&EndDate=2026-09-30`.

## Quy tắc mặc định, chờ PO xác nhận

- Chỉ role `Owner`, dùng JWT hiện có (header hoặc cookie đăng nhập).
- Mẫu số gồm tất cả trạng thái, kể cả Pending và lịch tương lai; một AppointmentId là một lịch, không join dịch vụ/thợ.
- Ngày hẹn hiện tại (`AppointmentDate`/`StartTime`) là giờ địa phương Việt Nam UTC+7; không dùng CreatedAt. Hai ngày biên đều được tính.
- Không đến: `AppointmentStatuses.NoShow`; huỷ: `AppointmentStatuses.Cancelled` hoặc tên cũ `Canceled`, không phân biệt hoa thường. Đã đối chiếu bộ trạng thái trên develop.
- Tối đa 366 ngày tính cả hai ngày biên; mặc định đầu tháng hiện tại đến hôm nay theo UTC+7.
- Làm tròn nửa lên 1 chữ số thập phân; mẫu dưới 20 lịch có cảnh báo Mẫu nhỏ.
- Mẫu số bằng 0: hai tỉ lệ null và emptyReason; khác với có lịch và tỉ lệ 0,0 %.
- Lỗi ngày không thay số liệu/khoảng ngày lần xem thành công trước.

## Kiểm tra

`dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj`

`node SalonManagement.Tests/appointment-analytics-ui.test.cjs` kiểm tra định dạng, giữ dữ liệu khi lỗi và phân biệt mẫu rỗng với tỉ lệ 0.

Chạy ứng dụng với cấu hình SQL Server của dự án bằng `dotnet run --project SalonManagement`. Đăng nhập Owner rồi mở màn hình theo URL trên. Không tự nạp dữ liệu demo vào database dùng chung. Để demo 7,5 % / 5,0 %, database cần 80 lịch trong tháng 09/2026, trong đó 6 NoShow và 4 Cancelled; test đã tạo bộ dữ liệu này trong database InMemory riêng.

Không có biểu đồ khung giờ hay so sánh ngày thường/cuối tuần trong lát này.
