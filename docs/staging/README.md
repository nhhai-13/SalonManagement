# Gói staging nghiệm thu

Môi trường `Staging` tự nạp dữ liệu nghiệm thu khi khởi động. Cấu hình này nằm trong `SalonManagement/appsettings.Staging.json` với `Seed:StagingDemo = true`; seed an toàn khi chạy lại và không nhân bản dữ liệu vì dùng mã lịch `STG-*` cố định.

## Chạy bản staging

1. Đặt biến môi trường `ASPNETCORE_ENVIRONMENT=Staging`.
2. Khởi động dự án `SalonManagement` theo quy trình triển khai hiện có.
3. Chờ ứng dụng khởi động xong để bộ seed tạo tài khoản, lịch mẫu, hóa đơn đã thanh toán, mã `DEMO10`, lời mời đánh giá và thông báo cho thợ.
4. Mở `/owner/operations-dashboard` để xác nhận có đủ sáu trạng thái lịch; mở `/owner/revenue-report` và lọc tháng trước để xác nhận số liệu không rỗng.

Tệp SQLite staging mặc định là `salon-management.staging.db`. Chỉ dùng dữ liệu và mật khẩu demo trong tài liệu này; thay chuỗi kết nối, khóa JWT và SMTP bằng secret của môi trường trước khi công khai URL staging.

## Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Khách hàng | `customer_demo@luminol.vn` | `Demo123!` |
| Lễ tân | `letan_demo@luminol.vn` | `Demo123!` |
| Thợ | `stylist_demo@luminol.vn` | `Demo123!` |
| Chủ tiệm | `owner_demo@luminol.vn` | `Demo123!` |

Xem [ma trận RBAC](RBAC-MATRIX.md), [kịch bản demo](DEMO-RUNBOOK.md) và [nhật ký diễn tập](DRY-RUN-LOG.md) trước khi nghiệm thu.
