# S2-03/S2-06 — kiểm thử SQL Server, 04/10/2026

Database cô lập: SalonManagement_S206_Test_20261004, SQL Server local. Không dùng database nhóm.

- GET /booking/stylists với dịch vụ cắt + gội: chỉ Thợ A và B có đủ kỹ năng.
- GET /booking/stylist-slots cho Thợ B: trả 26 giờ hợp lệ trước khi đặt.
- POST /booking/confirm với stylistId Thợ B, 14:00 ngày 05/10: HTTP 200, lịch 14:00–14:30 đúng Thợ B, mã HQVRJKQ7.
- POST lại cùng giờ/cùng thợ: HTTP 409, slot_unavailable, reloadSlots=true.
- POST thợ bất kỳ lúc 15:00: HTTP 200, Thợ C được chọn (ít lịch hơn A/B).
- Đã sửa demo S2-06 sinh mã tra cứu 8 ký tự cho mỗi lịch thay vì rỗng gây duplicate unique index. Test xác nhận mã không rỗng, duy nhất và seed chạy lại không thêm trùng.
- Bộ test: 220 pass, 0 fail, 0 skip; TRX local: TestResults/s2-sql-followup.trx. Các response HTTP lưu trong sql-http-results.json, sql-conflict-result.json, sql-any-stylist-result.json.

## Chạy host kiểm thử

```powershell
$env:BOOKING_PROBE_SQL='1'
$env:BOOKING_PROBE_URL='http://127.0.0.1:5185'
dotnet run --project tools/BookingProbe/BookingProbe.csproj --no-launch-profile
```

Host dùng Windows integrated authentication và tên database test cố định riêng; sẽ migrate và thêm dữ liệu tổng hợp. Không đặt BOOKING_PROBE_SQL thì dùng InMemory như trước. URL mặc định 5184. Cần dừng host cũ trước khi build lại.

## Chưa kiểm chứng / vấn đề ngoài phạm vi

- S2-03 tải <2 giây trên 4G: cần link staging và phép đo mạng; chưa có bằng chứng. Localhost không thay thế đo 4G.
- Thiết kế giao diện tiếp tục chờ; chưa đưa sửa giao diện local vào commit. Form chốt lịch do nhóm tích hợp cần gửi stylistId đến API; đã kiểm chứng hợp đồng API nhưng chưa nghiệm thu form E2E.
- SQL database sau MigrateAsync không có trigger chống chồng hẹn. Migration PreventOverlappingAppointments chưa được EF phát hiện (không có metadata Migration/DbContext). Phần này thuộc S2-08; việc chặn đặt lại qua API không chứng minh ràng buộc database hoặc an toàn đa tiến trình. Cần phối hợp người phụ trách S2-08 trước khi nghiệm thu toàn hệ thống.
