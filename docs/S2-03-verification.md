# S2-03 — Danh mục dịch vụ công khai: kiểm chứng

Phạm vi của LuuThiHue: S2-03 và S2-06. Tài liệu này ghi nhận kiểm tra mã hiện có, không thay đổi giao diện đang chờ bản thiết kế nhóm trưởng.

| Tiêu chí | Bằng chứng | Kết quả |
| --- | --- | --- |
| Nhóm dịch vụ, tên, phút, VND | PublicServiceCatalogTests và Views/Services/Public.cshtml | Bộ test pass |
| Chỉ dịch vụ đang bán và có thợ hoạt động | PublicServiceAvailabilityTests: các tổ hợp trạng thái và thay đổi phân công | Bộ test pass |
| Tìm không dấu | PublicServiceSearchTests; thử cat toc trên CatalogProbe | Pass |
| Không tràn ngang ở 360px | CatalogProbe, 100 dịch vụ/10 nhóm: innerWidth 360, scrollWidth 345 | Pass trên host dữ liệu giả lập |
| Tải dưới 2 giây trên 4G | Cần môi trường staging và mô phỏng/thiết bị mạng 4G | Chưa kiểm chứng; không dùng tốc độ localhost để kết luận |

Chạy lại test:

```powershell
dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --filter 'FullyQualifiedName~PublicService' --no-restore
```

Bằng chứng TRX và ảnh kiểm thử được lưu local trong TestResults. Không khẳng định nghiệm thu toàn bộ S2-03 cho đến khi có kết quả tốc độ 4G trên staging.
