# Chạy thử web ngày 04/10/2026

Ứng dụng đầy đủ chạy tại localhost:5187, dùng SQL Server database thử nghiệm riêng `SalonManagement_S206_Test_20261004`. Bản BookingProbe tại localhost:5186 chỉ phục vụ demo luồng đặt lịch. Không thay đổi code sản phẩm trong lần kiểm tra này.

## Đã thử và đạt

- Trang chủ và các liên kết danh mục/đặt lịch mở được.
- Tìm `goi` trả dịch vụ `Demo Gội đầu`.
- Trang `/admin/login` của ứng dụng đầy đủ mở được. Email thử không tồn tại trả thông báo email/mật khẩu không chính xác.
- Chọn dịch vụ cắt tóc, thợ B, ngày 05/10/2026, nhập 16:45: báo dịch vụ kết thúc sau giờ đóng cửa, không cho xác nhận.
- Nhập 13:07: hợp lệ, hiện thông tin xác nhận và các trường khách hàng.
- Gửi tên/điện thoại thử nghiệm: SQL Server lưu thành công, trả mã `DJMJU7J8` cho thợ B, 13:07–13:37.
- Tra mã `DJMJU7J8` trên ứng dụng đầy đủ: hiện đúng thợ, ngày giờ, dịch vụ, thời lượng và trạng thái đã xác nhận.
- Tra mã `HB2ZSCPV` trên BookingProbe: hiện đúng lịch demo đã tạo trước.

## Sai sót tái hiện

### 1. Form đặt nhanh báo đã nhận nhưng không gửi/lưu dữ liệu

Trang chủ: nhập điện thoại thử, chọn `Demo Gội đầu`, bấm `Đặt lịch ngay`. Trang báo “Luminol đã ghi nhận yêu cầu và sẽ liên hệ để xác nhận lịch hẹn”, rồi xoá form.

Đối chiếu `wwwroot/js/site.js`: submit chỉ `preventDefault`, kiểm tra điện thoại, đặt thông báo và reset form; không có request API hay lưu dữ liệu. Khách có thể tưởng tiệm đã nhận yêu cầu trong khi chưa có yêu cầu nào được lưu.

### 2. Giờ hoạt động hiển thị không khớp cấu hình đặt lịch

Trang chủ ghi “Thứ Hai – Chủ Nhật: 09:00 – 21:00”. Khi đặt lịch ngày 05/10/2026, máy chủ áp dụng 09:00–17:00 và từ chối giờ kết thúc sau 17:00. Cần lấy nội dung giờ mở cửa từ cấu hình BusinessHours thay vì chuỗi cố định trên trang chủ.

### 3. Danh mục mobile bị cuộn ngang

Kết quả kiểm tra trình duyệt trước cùng phiên bản: viewport 360px, nội dung rộng 454px. Thanh điều hướng vượt màn hình. Chưa sửa vì người dùng yêu cầu giữ thiết kế của trưởng nhóm.

### 4. Trang đăng nhập không mở được trên BookingProbe

Link `/Account/Login` ở localhost:5186 trả lỗi server thiếu `IEmailService` khi tạo AccountController. Đây là thiếu dependency trong host demo; `/admin/login` trên ứng dụng đầy đủ localhost:5187 chạy được. Không kết luận ứng dụng đầy đủ bị lỗi đăng nhập dựa trên host demo.

## Chưa kiểm chứng

- Đăng nhập thành công và toàn bộ thao tác quản lý ca/ngày nghỉ/dịch vụ bằng tài khoản chủ tiệm: chưa có tài khoản thử hợp lệ trong phiên kiểm tra trình duyệt này.
- Email/SMS xác nhận: luồng đặt lịch đang hiển thị thông báo trên trang, chưa chứng minh gửi email/SMS.
- Tốc độ 4G và staging: chưa có môi trường.

Ưu tiên xử lý: form đặt nhanh báo nhận sai thực tế, sau đó đồng bộ giờ hoạt động. Luồng đặt lịch chính và tra cứu đã chạy thành công với SQL Server thật trong database thử riêng.
