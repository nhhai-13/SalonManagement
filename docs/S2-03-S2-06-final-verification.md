# S2-03 / S2-06 — Kết quả đối chiếu yêu cầu

Đã fetch cập nhật GitHub, kiểm tra trên nhánh codex/sprint2-booking-fixes. Không đưa thiết kế giao diện đang chờ trưởng nhóm vào thay đổi.

S2-03: có nhóm dịch vụ, tên, thời lượng, giá VND; lọc dịch vụ đang bán có thợ hoạt động; tìm không dấu. Test PublicService* có trong bộ test. Kiểm tra 360px đã thực hiện trên host dữ liệu giả lập. Tiêu chí dưới 2 giây trên mạng 4G chưa kiểm chứng trên staging.

S2-06: lọc thợ đủ toàn bộ kỹ năng, giờ theo thợ cụ thể và hợp giờ của thợ bất kỳ; assignment ưu tiên ít lịch đang xử lý trong ngày rồi ID nhỏ hơn. Đã bổ sung lịch InProgress vào ranking, loại ngày nghỉ tiệm/nghỉ thợ/giờ nghỉ và khung giờ dưới 60 phút. API xác nhận nhận StylistId, giữ lựa chọn thợ cụ thể hoặc tự gán người rảnh. Test xác nhận kiểm tra không âm thầm thay thợ không đủ kỹ năng.

Bằng chứng chạy test: TestResults/s2-final.trx. Test bổ sung: StylistAssignmentTests và Sprint2AuditTests. Các test sử dụng EF InMemory; chưa thay thế kiểm thử tích hợp SQL Server.

Giới hạn còn lại: giao diện chọn thợ hiện là bước xem trước, chưa có đầy đủ form tạo lịch S2-07 trong trang hiện tại; API xác nhận đã hỗ trợ StylistId, giao diện chốt cuối cần truyền trường đó khi nhóm tích hợp. Chưa xác nhận toàn bộ luồng E2E trên SQL Server do kết nối database local lỗi. Không khẳng định nghiệm thu toàn bộ khi các kiểm tra staging/4G/E2E chưa chạy.
