# Kiểm chứng sửa lỗi web

Lần sửa này cập nhật các lỗi đã nêu trong báo cáo Web-Smoke-Test và một số tiêu chí Sprint 2:

- Form đặt nhanh chuyển qua POST có antiforgery token, kiểm tra điện thoại trên máy chủ, chuyển vào `/booking/select-services` với dịch vụ đã chọn. Không báo đã nhận/lưu lịch khi chưa tạo lịch. Điện thoại được chuyển bằng TempData sang ô thông tin khách, không đưa điện thoại vào URL.
- Trang chủ hiển thị lịch hoạt động từ BusinessHours, gồm trạng thái đóng cửa và giờ riêng từng ngày. Kiểm tra trên SQL Server thử nghiệm hiển thị 09:00–17:00, khớp kiểm tra giờ đặt lịch.
- Danh mục ở 360px cho phép thanh điều hướng xuống hàng. Phép đo trình duyệt sau sửa: viewport 360px, nội dung 345px, không tràn ngang.
- Tạo ngày nghỉ tiệm trả HTTP409 và danh sách lịch ảnh hưởng nếu ngày đó còn lịch Pending/Confirmed/InProgress, không tự lưu ngày nghỉ làm mất khả năng thực hiện lịch hiện hữu.
- Khi xác nhận gặp conflict yêu cầu làm mới, JavaScript kiểm tra lại giờ hiện tại với máy chủ; các giá trị thông tin khách được giữ để nhập lại giờ và xác nhận.

Kiểm thử: **233/233 đạt**, file local `TestResults/web-fixes-complete.trx`. Bổ sung kiểm thử ngày nghỉ trùng lịch hẹn và hiển thị giờ hoạt động theo cấu hình. Chạy web đầy đủ với SQL Server thử riêng tại localhost:5187; không thay dữ liệu thật của nhóm.

Các giới hạn vẫn còn: chưa có staging để đo dưới 2 giây trên 4G; chưa kiểm chứng 20 yêu cầu giữa nhiều instance SQL Server; chưa chạy đầy đủ thao tác chủ tiệm bằng tài khoản thử. Không kết luận toàn bộ Sprint 2 hoàn tất chỉ từ kết quả này.
