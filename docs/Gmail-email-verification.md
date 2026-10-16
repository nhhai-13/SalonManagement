# Gửi mã xác thực qua Gmail

Ứng dụng đã dùng Gmail SMTP (`smtp.gmail.com`, cổng `587`, TLS). Để gửi mã thật, cần tạo **App Password** riêng cho tài khoản Gmail dùng để gửi thư. Không dùng và không lưu mật khẩu Gmail chính trong mã nguồn hoặc GitHub.

## Thiết lập một lần trên máy chạy ứng dụng

1. Bật Xác minh 2 bước cho tài khoản Gmail gửi thư.
2. Tạo một App Password cho ứng dụng Mail, sau đó sao chép mật khẩu 16 ký tự.
3. Trong Visual Studio, bấm chuột phải dự án **SalonManagement** rồi chọn **Manage User Secrets**.
4. Dán cấu hình dưới đây và thay các giá trị trong ngoặc. App Password chỉ nằm trên máy cục bộ, không được đưa vào Git.

```json
{
  "SmtpSettings": {
    "SenderEmail": "dia-chi-gui@gmail.com",
    "Username": "dia-chi-gui@gmail.com",
    "Password": "app-password-16-ky-tu"
  }
}
```

5. Khởi động lại ứng dụng, đăng ký thử bằng một email khác và kiểm tra Hộp thư đến / Spam.

Mỗi mã xác minh gồm 6 chữ số, chỉ dùng một lần và hết hạn sau 10 phút.
