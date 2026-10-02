# S2-06 — Lát 3: Ưu tiên thợ ít lịch trong ngày

## Quy tắc bản review (chờ PO xác nhận)

- Đếm số lịch hẹn, không dùng tổng phút.
- Chỉ tính `Pending` và `Confirmed` trong đúng ngày Việt Nam khách chọn; không tính `Cancelled`, `Completed` hoặc ngày khác.
- Cùng số lịch: `StylistId` tăng dần để ổn định.
- Chỉ xếp hạng thợ đang làm, đủ toàn bộ kỹ năng và rảnh trọn slot (thời lượng tính từ DB). Thợ ít lịch nhưng bận, thiếu kỹ năng, ngoài ca hoặc nghỉ việc đều bị loại trước khi xếp hạng.
- Lịch không bị hủy vẫn chặn giờ theo quy tắc khả dụng các lát trước; trạng thái được đếm để xếp hạng là quy tắc riêng.

## Luồng

Chọn Thợ bất kỳ → chọn giờ → Xác nhận lựa chọn. `GET /booking/assignment?serviceIds=...&stylistId=0&date=YYYY-MM-DD&start=HH:mm` kiểm tra lại dữ liệu và trả `stylistId`, tên, ngày, giờ bắt đầu/kết thúc. Nếu chọn đích danh thì giữ đúng người đó nếu còn phù hợp, không đổi sang người khác. Không ai còn rảnh trả 409; dữ liệu lỗi trả 400.

Khối xác nhận hiển thị tên thợ và giờ. Đổi dịch vụ, thợ, ngày hoặc giờ xóa kết quả cũ; request đến muộn không ghi đè lựa chọn mới. API no-store, không dùng danh sách ứng viên hay số lịch do client gửi lên.

**Giới hạn:** code hiện có chưa có chức năng lưu lịch hẹn/thông tin khách. Lát này thực hiện quyết định gán và hiển thị ở bước xác nhận, không giữ chỗ hoặc ghi lịch hẹn. Giao diện nói rõ điều đó. Khi nối với chức năng tạo lịch hẹn, phải chạy lại kiểm tra/gán trong transaction có cơ chế chống đặt trùng; kết quả GET không bảo đảm chỗ còn trống ở lần ghi sau. Chưa chốt nghiệm thu toàn story hoặc quy tắc PO.

## Demo và kiểm thử

Seed Development opt-in dùng `Seed__StylistBookingDemo=true`. Ngày thứ ba: A có 2 lịch, B 1, C 0; lúc 14:00 chọn chỉ cắt thì C, cắt + gội thì B vì C thiếu kỹ năng. Ngày thứ tư: cắt + gội, A và B bằng lịch, chọn ID nhỏ hơn. Bộ seed bổ sung có marker và không nhân bản khi chạy lại.

`StylistAssignmentTests` kiểm tra ít nhất trong ba người, đổi workload, ít lịch nhưng bận, chỉ còn một người, hòa, trạng thái, khác ngày, đếm số thay vì phút, thiếu kỹ năng, nghỉ việc, slot sai và phản hồi conflict. `tools/booking-stylist-check.cjs` kiểm tra giao diện chọn bất kỳ, xác nhận C/B/A theo dữ liệu, xóa xác nhận khi đổi lựa chọn; dùng `tools/BookingProbe` để chạy trên InMemory, không tác động database thật.

Chạy `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`; sau đó khởi động BookingProbe và chạy script Node như tài liệu Lát 1. Chưa có xác minh trên SQL Server staging.
