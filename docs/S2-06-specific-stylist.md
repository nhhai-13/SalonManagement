# S2-06 — Chọn thợ cụ thể và giờ trống riêng (AC1, AC3)

## Phạm vi và phụ thuộc

Nhánh này tích hợp nhánh `feature/s2-04/select-services-and-calculate-totals` (PR #26) để dùng màn hình `/booking`. Nên merge PR #26 vào develop trước khi merge S2-06. Không thêm tùy chọn thợ bất kỳ, tự gán thợ hoặc tạo lịch hẹn thật. Việc xác nhận đặt lịch sau này phải kiểm tra lại khả dụng trong transaction để tránh đặt trùng.

## Quyết định chờ PO

Bản review đang dùng đề xuất, chưa có xác nhận PO:

- Không ai đủ kỹ năng: báo không có thợ thực hiện toàn bộ dịch vụ, hướng dẫn bỏ bớt/đổi dịch vụ.
- Thẻ thợ: tên, ảnh hồ sơ nếu có và chuyên môn. Không tạo đánh giá giả.
- Dịch vụ thay đổi: bỏ thợ nếu không còn phù hợp; giữ nếu vẫn đủ kỹ năng. Luôn bỏ giờ cũ và tải lại giờ theo tổng thời lượng mới.

## Hành vi

- `GET /booking/stylists?serviceIds=1&serviceIds=2`: chỉ thợ đang làm việc có **tất cả** dịch vụ. Dịch vụ không tồn tại/ngừng bán hoặc lựa chọn rỗng trả 400, không âm thầm bỏ dịch vụ lỗi.
- `GET /booking/slots?serviceIds=1&serviceIds=2&stylistId=3&date=2030-01-07`: xác minh thợ, tính tổng thời lượng từ DB. Trả giờ của đúng thợ, không fallback sang thợ khác. Không nhận tổng thời lượng từ trình duyệt.
- Giờ Việt Nam; bắt đầu mỗi 15 phút. Cắt ca theo giờ mở cửa; không vượt giờ đóng cửa, khoảng nghỉ hay qua ngày. Gộp ca liền/đè nhau để không trùng slot; không có ca Working hoặc ngày đóng cửa thì rỗng. Thiếu cấu hình giờ mở cửa cũng rỗng.
- Trừ các lịch hẹn của thợ/ngày đó, trừ trạng thái `Cancelled`. Khoảng thời gian nửa mở: cuộc hẹn kết thúc đúng lúc slot bắt đầu không xung đột. Không trả giờ đã qua.
- Đổi ngày, thợ, dịch vụ sẽ tải lại và xóa slot cũ. Hủy request trước và kiểm tra phiên bản để phản hồi cũ không ghi đè lựa chọn mới. API không cache.
- Dữ liệu tên/chuyên môn được đưa vào DOM bằng textContent; ảnh dùng đường dẫn nội bộ.

## Demo

Trong môi trường **Development**, bật cấu hình `Seed:StylistBookingDemo=true` (biến môi trường `Seed__StylistBookingDemo=true`). Tạo một lần nhóm Demo S2-06 và bảy ngày lịch bắt đầu từ ngày mai, không sửa thợ/lịch thật:

- A làm cắt + gội, bận 09:00–10:00.
- B làm cắt + gội + nhuộm, bận 10:00–11:00.
- C chỉ làm cắt.
- Ca 09:00–17:00; tôn trọng giờ salon đã có (không ghi đè ngày đóng cửa). Khởi tạo giờ mẫu nếu chưa có.

Chọn cắt + gội: chỉ A/B; A có 10:00–11:00 còn B có 09:00–10:00. Thêm nhuộm: chỉ B. Seed có thể chạy lặp không nhân bản, không tự dời ngày demo cũ.

## Kiểm thử

- `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`: giao kỹ năng, thợ nghỉ, dịch vụ lỗi, tổng thời lượng, lịch riêng, kín lịch A nhưng B còn trống, biên lịch hẹn/ca, khoảng nghỉ, ngày đóng cửa, giờ quá khứ, ca chồng nhau/không làm, seed lặp.
- `dotnet run --project tools/BookingProbe/BookingProbe.csproj --no-launch-profile`: host MVC/Razor thật trên localhost:5184, InMemory, dữ liệu giả; không đụng SQL Server.
- Với Node.js/Playwright/Edge: `node tools/booking-stylist-check.cjs` (đặt NODE_PATH nếu package nằm ngoài repository). Kiểm tra chọn/đổi thợ, đổi dịch vụ, đổi ngày, bỏ lựa chọn không hợp lệ, rỗng, clear và request đến chậm. Ảnh tại `artifacts/booking/stylist-selection.png`.
- Chưa nghiệm thu với PO hoặc SQL Server staging; công cụ browser không thay thế nghiệm thu production.
