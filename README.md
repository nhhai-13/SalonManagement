# Salon Management

## Dùng chung cơ sở dữ liệu SQL Server

Dự án chỉ sử dụng SQL Server. Khi ứng dụng khởi động, Entity Framework Core sẽ tự áp dụng các migration còn thiếu và nạp dữ liệu mẫu nếu dữ liệu chưa tồn tại.

Không lưu tài khoản hoặc mật khẩu của SQL Server vào `appsettings*.json`. Mỗi thành viên cấu hình cùng một chuỗi kết nối bằng .NET User Secrets:

```powershell
cd SalonManagement
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=TEN_MAY_CHU,1433;Database=SalonManagementDB;User Id=TEN_DANG_NHAP;Password=MAT_KHAU;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run
```

Thay `TEN_MAY_CHU`, `TEN_DANG_NHAP` và `MAT_KHAU` bằng thông tin của SQL Server dùng chung. Quản trị viên SQL Server cần bật kết nối TCP/IP, mở cổng (thường là `1433`), cho phép xác thực tương ứng và cấp quyền trên database `SalonManagementDB` cho tài khoản của nhóm.

Trên máy chủ triển khai, có thể dùng biến môi trường thay cho User Secrets:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=TEN_MAY_CHU,1433;Database=SalonManagementDB;User Id=TEN_DANG_NHAP;Password=MAT_KHAU;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run --project SalonManagement
```

Giá trị trong `appsettings.json` và `appsettings.Development.json` chỉ là cấu hình SQL Server LocalDB dự phòng cho một máy. Để các thành viên nhìn thấy cùng dữ liệu, tất cả phải cấu hình cùng địa chỉ SQL Server dùng chung theo một trong hai cách trên.
