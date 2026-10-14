# S4-09 — Lát 2 (AC1)

Quy tắc đã được người dùng chốt: 24 khung cố định, 60 phút từ 00:00 đến 24:00. Mỗi lịch được đếm một lần theo giờ bắt đầu hiện tại theo UTC+7; gồm mọi trạng thái, cả huỷ và không đến. Lịch dài không nhân số lượt. 09:59 thuộc 09:00–10:00; 10:00 thuộc 10:00–11:00. Các lịch trước/sau giờ mở cửa vẫn được tính trong khung tương ứng. Giờ lưu sai ngoài [00:00,24:00) được báo riêng qua unassignedTimeCount, không bỏ âm thầm.

API hiện có trả thêm hourlyBuckets, luôn đủ 24 khung kể cả khung rỗng. Tổng các cột bằng total khi giờ lưu hợp lệ. Hai chỉ số và biểu đồ được tổng hợp từ một truy vấn duy nhất, dùng chung bộ lọc ngày và quyền Owner của lát 1.

Biểu đồ SVG không cần thư viện/CDN, có trục giờ và số lượt, số trên mỗi cột. Rê/chạm/Tab vào cột để xem chi tiết. Khoảng không có lịch dùng dòng giải thích hiện có thay cho biểu đồ. Lỗi bộ lọc giữ nguyên chỉ số, khoảng ngày và biểu đồ lần xem thành công.

Kiểm thử: dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj; node SalonManagement.Tests/appointment-analytics-ui.test.cjs.

Demo: đăng nhập Owner, mở /admin/appointment-analytics, chọn khoảng có lịch và bấm Xem. Rê/chạm cột 10:00 để xem số lượt; thay khoảng ngày để kiểm tra cả hai chỉ số và biểu đồ. Chưa có so sánh ngày thường, thứ Bảy, Chủ nhật.
