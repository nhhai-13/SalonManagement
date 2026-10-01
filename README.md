# Salon Management System - Staging & Setup Guide

Dự án quản lý Salon hỗ trợ khởi chạy môi trường Staging/Development bằng một lệnh.

---

## 1. Hướng dẫn chạy ứng dụng

Mở Terminal tại thư mục gốc của dự án và chạy:

```powershell
dotnet run --project SalonManagement --launch-profile Staging
```

Ứng dụng sẽ tự động áp dụng migration, nạp dữ liệu mẫu và tạo các tài khoản demo nếu dữ liệu chưa tồn tại.

---

## 2. Thông tin Staging và tài khoản Demo

Staging cục bộ: **http://localhost:5129**. Đăng nhập cả bốn vai trò tại **http://localhost:5129/admin/login**. Chưa có địa chỉ staging công khai được triển khai.

| Vai trò | Email | Mật khẩu |
| :--- | :--- | :--- |
| **Admin** | admin@salon.local | Admin123! |
| **Owner** | owner@salon.local | Owner123! |
| **Receptionist** | receptionist@salon.local | Reception123! |
| **Stylist** | stylist@salon.local | Stylist123! |

> Các tài khoản trên chỉ được sử dụng cho môi trường phát triển và thử nghiệm.

---

## 3. Dùng chung cơ sở dữ liệu SQL Server

Dự án chỉ sử dụng SQL Server. Khi ứng dụng khởi động, Entity Framework Core sẽ tự áp dụng các migration còn thiếu và nạp dữ liệu mẫu nếu dữ liệu chưa tồn tại.

Không lưu tài khoản hoặc mật khẩu SQL Server vào `appsettings*.json`. Mỗi thành viên cấu hình cùng một chuỗi kết nối bằng .NET User Secrets:

```powershell
cd SalonManagement
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=TEN_MAY_CHU,1433;Database=SalonManagementDB;User Id=TEN_DANG_NHAP;Password=MAT_KHAU;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run
```

Thay `TEN_MAY_CHU`, `TEN_DANG_NHAP` và `MAT_KHAU` bằng thông tin của SQL Server dùng chung. Quản trị viên SQL Server cần bật kết nối TCP/IP, mở cổng — thường là `1433` — và cấp quyền trên cơ sở dữ liệu `SalonManagementDB` cho tài khoản của nhóm.

Trên máy chủ triển khai, có thể dùng biến môi trường thay cho User Secrets:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=TEN_MAY_CHU,1433;Database=SalonManagementDB;User Id=TEN_DANG_NHAP;Password=MAT_KHAU;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run --project SalonManagement --launch-profile Staging
```

Giá trị trong `appsettings.json` và `appsettings.Development.json` chỉ là cấu hình SQL Server LocalDB dự phòng cho một máy. Để tất cả thành viên nhìn thấy cùng dữ liệu, mọi người phải cấu hình cùng một địa chỉ SQL Server dùng chung theo một trong hai cách trên.
