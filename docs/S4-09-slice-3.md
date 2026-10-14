# S4-09 — Lát 3 (AC3)

Quy tắc đã chốt: ngày thường gồm thứ Hai–thứ Sáu; ngày lễ vẫn xếp theo thứ trong tuần. Thứ Bảy và Chủ nhật tách riêng. Đếm mọi trạng thái theo ngày và giờ bắt đầu hiện tại UTC+7, mỗi lịch một lần như lát 1/2.

API thêm dayTypes với số ngày, số lịch, số không đến/huỷ, tỉ lệ, trung bình mỗi ngày và đủ 24 khung của mỗi nhóm có ngày. Trung bình lấy số lượt chia mọi ngày trong nhóm (cả ngày không có lịch). Biểu đồ dùng giá trị chưa làm tròn để so độ cao, nhãn hiển thị một chữ số thập phân. Tỉ lệ nhóm tính trên tổng lịch của nhóm, làm tròn nửa lên như lát 1. Không có ngày: phân bố và trung bình null; không có lịch: tỉ lệ null kèm lý do, khác với 0,0 %.

Ba biểu đồ đặt cạnh nhau, chung thang trục dọc tính từ tất cả nhóm. Cao điểm tính theo số lượt chưa làm tròn; đồng hạng được đánh dấu tất cả, toàn 0 không có cao điểm. Khung cao điểm màu nhấn, nhãn Cao điểm, chi tiết khi rê/chạm/Tab. Bảng ba cột có số ngày, tổng lịch, trung bình/ngày, tỉ lệ không đến và huỷ kèm số lịch căn cứ. Nhóm không có thứ Bảy/Chủ nhật có dòng giải thích.

Toàn bộ chỉ số, biểu đồ tổng, so sánh được tổng hợp từ một truy vấn và cập nhật cùng lần Xem. Lỗi bộ lọc giữ dữ liệu lần thành công trước. Quyền Owner của API/màn hình tiếp tục bảo vệ toàn bộ báo cáo.

Demo: chọn 01/10/2026–31/10/2026 (22 ngày thường, 5 thứ Bảy, 4 Chủ nhật). Chọn 02/10/2026 để kiểm tra nhóm cuối tuần rỗng. Test: dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj; node SalonManagement.Tests/appointment-analytics-ui.test.cjs.

Không tự thêm hay sửa lịch trong database dự án gốc; dùng database demo riêng khi cần số liệu minh họa.
