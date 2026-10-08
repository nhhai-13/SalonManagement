# S2-06 — Lát 2: Thợ bất kỳ (phần lựa chọn của AC2)

## Hành vi

- “Thợ bất kỳ” ở đầu danh sách. Bản review dùng **khách chọn chủ động**, chưa có xác nhận PO về mặc định.
- Khi không có thợ đủ kỹ năng, tùy chọn này bị vô hiệu hóa và thông báo hướng dẫn đổi dịch vụ vẫn xuất hiện.
- `GET /booking/slots?...&stylistId=0&date=YYYY-MM-DD` trả hợp các giờ trống của thợ đang làm việc đủ **tất cả** kỹ năng. `stylistId > 0` giữ chế độ đích danh. Thiếu ID hoặc ID âm trả 400, không tự hiểu là bất kỳ.
- Đọc dịch vụ, thợ đủ kỹ năng, giờ mở cửa, ca và lịch hẹn theo lô; không query lại toàn bộ dữ liệu cho từng thợ.
- Tính khoảng đủ tổng thời lượng của từng thợ trước, sau đó bỏ trùng theo giờ bắt đầu/kết thúc và sắp tăng dần. Không ghép ca hay phần thời gian rảnh của hai thợ thành một slot.
- Vẫn áp dụng giờ Việt Nam, bước 15 phút, giờ mở cửa, ca Working, lịch hẹn và loại giờ quá khứ. Thợ nghỉ hoặc thiếu một kỹ năng không đóng góp giờ.
- Đổi chế độ luôn xóa giờ cũ và tải lại; phản hồi cũ không ghi đè kết quả mới. Chọn bất kỳ vẫn giữ giá trị 0 khi bấm giờ, **không gán thợ, không tạo lịch hẹn**.
- PO cần chốt thời điểm khách được biết tên thợ khi triển khai lát tự gán sau này; lát này chưa có tên thợ được gán để hiển thị.

## Demo

Dùng seed Development hoặc BookingProbe trong tài liệu Lát 1. Ngày thứ hai của bộ mẫu (ngày kia với seed mới): A kín 09:00–17:00, B bận 10:00–11:00, C chỉ làm cắt. Chọn cắt + gội: chọn A sẽ rỗng, chọn Thợ bất kỳ vẫn có giờ của B (ví dụ 09:00–10:00). Chuyển lại A phải rỗng, giờ vừa chọn bị xóa.

Seed cũ được bổ sung khoảng bận còn thiếu cho A chỉ khi tìm thấy bản ghi demo đúng ngày và khách mẫu; không sửa lịch hẹn gốc, không tạo trùng khi chạy lại. Nếu bộ ngày demo đã hết hạn, dùng BookingProbe để có dữ liệu giả mới mà không sửa DB thật.

## Xác minh

- Chạy `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`.
- `AnyStylistAvailabilityTests`: hợp chính xác có thứ tự/không trùng; giờ chỉ một thợ rảnh; không ai rảnh; A kín nhưng B rảnh; thiếu kỹ năng/nghỉ việc; không ghép ca khác người; dữ liệu đầu vào/ngày đóng cửa; seed nâng cấp; không ghi lịch hẹn khi xem giờ.
- `dotnet run --project tools/BookingProbe/BookingProbe.csproj --no-launch-profile --no-restore`, sau đó `node tools/booking-stylist-check.cjs` với Playwright trong NODE_PATH. Browser kiểm tra cả hai chế độ, ngày A kín, chọn giờ không gán thợ, đổi lại A xóa giờ, ngày/dịch vụ thay đổi và phản hồi chậm.
- Test dùng EF InMemory và Edge headless; chưa thay thế nghiệm thu PO/SQL Server staging.
