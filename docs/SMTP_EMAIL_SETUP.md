# Cấu hình email xác thực và quên mật khẩu

Các mã xác thực đăng ký và mã OTP quên mật khẩu được gửi bằng SMTP. Thông tin đăng nhập SMTP **không** được lưu trong `appsettings*.json` hoặc Git.

## Gmail (dùng cho máy demo/local)

Sau khi đăng nhập bằng tài khoản **Chủ tiệm**, bạn có thể nhập các thông tin này trực tiếp tại **Email gửi mã** trong thanh menu, hoặc mở `/owner/email-settings`. Mật khẩu ứng dụng được mã hóa khi lưu và trang có nút gửi email thử.

1. Bật Xác minh 2 bước cho tài khoản Gmail sẽ dùng để gửi thư.
2. Trong trang Bảo mật của Google, tạo **Mật khẩu ứng dụng** cho Mail. Không dùng mật khẩu đăng nhập Gmail thông thường.
3. Mở PowerShell tại thư mục gốc dự án và chạy các lệnh sau. Thay địa chỉ bằng email của bạn; khi nhập App Password, giữ giá trị đó ở máy của bạn.

```powershell
dotnet user-secrets set "SmtpSettings:SenderEmail" "your-email@gmail.com" --project .\SalonManagement\SalonManagement.csproj
dotnet user-secrets set "SmtpSettings:Username" "your-email@gmail.com" --project .\SalonManagement\SalonManagement.csproj
dotnet user-secrets set "SmtpSettings:Password" "GMAIL_APP_PASSWORD" --project .\SalonManagement\SalonManagement.csproj
```

`Server`, `Port` và tên người gửi đã có sẵn trong `appsettings.Development.json` (`smtp.gmail.com`, cổng `587`). Khởi động lại ứng dụng sau khi cấu hình.

## Kiểm tra

1. Mở **Quên mật khẩu**, nhập chính email của bạn.
2. Hệ thống gửi một mã 6 chữ số vào hộp thư đó.
3. Nhập mã trong vòng 10 phút để mở trang đặt mật khẩu mới.

Nếu gửi thư không thành công, ứng dụng hiển thị lỗi thay vì in mã vào console. Hãy kiểm tra App Password, thư mục Spam/Rác và kết nối Internet.
