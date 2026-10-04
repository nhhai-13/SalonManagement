# Đối chiếu S2-01–S2-09 với file Excel

Nguồn: `3. Hệ thống đặt lịch Salon Spa.xlsx`, sheet `4. Product Backlog`. Bản code: `4c2d943`. Chạy lại ngày 04/10/2026: 233/233 test tự động đạt (gồm cả kiểm thử ngoài Sprint 2). Bằng chứng local: `TestResults/s2-01-to-s2-09-final.trx`.

| Mục | Kết luận với đề gốc |
|---|---|
| S2-01 | Các ca tự động tạo/sửa, chồng lấn, giờ hoạt động, sao chép tuần và chặn xóa ca có lịch đạt; chưa xác nhận toàn bộ thao tác chủ tiệm trên trình duyệt. |
| S2-02 | Kiểm tra ngày nghỉ/giờ nghỉ và lịch ảnh hưởng đạt; thiếu khai báo ngày nghỉ toàn tiệm theo khoảng ngày (API hiện nhận một HolidayDate). Chặn khi có lịch là biện pháp bảo vệ hiện tại, chưa có thao tác duyệt cảnh báo rồi lưu khoảng nghỉ. |
| S2-03 | Logic danh mục/tìm không dấu đạt; đã đo không tràn ở 360px sau sửa. Tải dưới 2 giây trên 4G chưa kiểm chứng vì chưa có staging. |
| S2-04 | Bộ kiểm thử tổng tiền/thời lượng, giới hạn 5 và cảnh báo vượt ca đạt. |
| S2-05 | Backend khung giờ 15 phút, nghỉ, lead time và ngày gợi ý được kiểm thử; UI đã đổi sang nhập giờ theo yêu cầu người dùng, không còn danh sách gợi ý và chưa hiển thị hai ngày gần nhất khi hết chỗ. Không đánh dấu đạt toàn bộ đề gốc. |
| S2-06 | Đủ kỹ năng, chọn thợ cụ thể/bất kỳ và ưu tiên ít lịch đạt; UI nhập giờ thay cho danh sách giờ là thay đổi đã được người dùng yêu cầu. |
| S2-07 | Đặt không cần tài khoản, trường khách, mã tra cứu, giới hạn lịch/IP được kiểm thử; luồng lưu và tra cứu đã thử trên SQL Server riêng. Tóm tắt thành công cần kiểm tra bổ sung việc hiển thị tên tất cả dịch vụ (hiện thợ, ngày giờ và mã; dịch vụ ở phần đã chọn). |
| S2-08 | Trigger SQL đã thử chặn trùng và 20 request có đúng 1 thành công; **thiếu giao dịch có khóa bản ghi thợ đúng yêu cầu Excel**. Semaphore trong process không thay thế khóa database. Chưa kiểm thử nhiều process SQL. UI kiểm tra lại giờ hiện tại khi conflict, không nạp danh sách gợi ý do thay đổi sang nhập giờ. |
| S2-09 | Tra mã/điện thoại, chi tiết, lọc lịch đã kết thúc và chặn tra sai được kiểm thử; tra cứu lịch đã lưu trên SQL Server đã thử thành công. |

Các kết luận phía trên là bản đối chiếu trước khi bổ sung code. Kết quả mới bên dưới thay thế các ghi nhận thiếu ở S2-02, S2-05 và S2-08.

## Bổ sung S2-02, S2-03, S2-05, S2-08

- S2-02: bổ sung API chủ tiệm `POST /api/shop-holidays/range`, nhận `startDate`, `endDate`, `reason`, `confirmAffectedAppointments`. Khoảng ngày bao gồm cả hai đầu. Khi có lịch đang hoạt động, trả HTTP 409 cùng danh sách lịch ảnh hưởng trước khi lưu; gửi lại với xác nhận mới lưu. Từ chối khoảng đảo ngược, lý do trống và ngày nghỉ trùng. Đây là bổ sung backend; chưa có form quản trị khoảng ngày nghỉ trên web.
- S2-03: thêm nén phản hồi và cache tài nguyên tĩnh một giờ. Đã kiểm tra header cache trên localhost. Chưa có staging nên chưa xác nhận tiêu chí dưới hai giây trên mạng 4G.
- S2-05: khi ngày của thợ đã chọn không còn khung giờ khả dụng, hiển thị hai ngày gần nhất trong 14 ngày tiếp theo. Đã thử trên trình duyệt: ngày 04/10 hết chỗ, hiển thị 05/10 và 06/10; chọn 05/10 cập nhật ngày đúng. Giữ ô nhập giờ thủ công theo yêu cầu người dùng, không khôi phục danh sách giờ gợi ý của đề gốc.
- S2-08: xác nhận lịch trong transaction SQL Server, khóa dòng thợ bằng `UPDLOCK, HOLDLOCK`, kiểm tra lại tính khả dụng và trùng lịch trước khi lưu. Giữ trigger chống trùng ở database. Thử 20 yêu cầu đồng thời chia đều qua hai tiến trình web dùng chung database kiểm thử: 1 HTTP 200, 9 HTTP 409, 10 HTTP 429; database chỉ có một lịch tại giờ được thử. HTTP 429 là giới hạn IP hiện có.

Kiểm thử tự động: **235/235 đạt**, gồm cả test ngoài Sprint 2. Bằng chứng local: `TestResults/sprint2-supplements-verified.trx`, `TestResults/s208-two-process-sql.json` và log hai tiến trình. Database kiểm thử riêng: `SalonManagement_S206_Test_20261004`; không thay dữ liệu production. Các file TestResults không đưa lên GitHub.

## Form S2-02 (05/10/2026)

Đã bổ sung `/owner/holidays`, chỉ tài khoản Owner truy cập, có lối vào từ khu vực chủ tiệm và menu quản lý salon. Form gồm khoảng ngày, lý do, danh sách ngày nghỉ và danh sách lịch bị ảnh hưởng với checkbox xác nhận. Thay đổi ngày/lý do xóa xác nhận cũ; khóa nhập và nút lưu trong lúc gửi yêu cầu. Dữ liệu từ API hiển thị bằng textContent.

Build thành công, không warning/error. Kiểm tra trình duyệt với tài khoản Owner trên database riêng: cảnh báo bảy lịch ngày 05/10 trước khi lưu; đổi sang 01–02/01/2031 xóa cảnh báo cũ; lưu thành công hai ngày và danh sách cập nhật. Truy cập khi chưa đăng nhập chuyển đến trang đăng nhập. Đây là kiểm tra form bổ sung; không chạy lại toàn bộ 235 test vì backend nghiệp vụ không đổi.

## Kiểm tra tiếp form (05/10/2026)

- Không tích xác nhận: form chặn lưu và yêu cầu xác nhận.
- Tích xác nhận: lưu thành công ngày nghỉ 05/10 trên database kiểm thử riêng, giữ nguyên các lịch đã có.
- Tạo ngày trùng: hiển thị thông báo khoảng ngày có ngày nghỉ đã thiết lập.
- Màn hình 360px: scrollWidth 345px, không tràn ngang; hai phần form/danh sách xếp dọc.
- Bổ sung kiểm tra lý do chỉ có khoảng trắng và khoảng ngày vượt 366 ngày ngay trên form; cả hai thông báo đã kiểm tra trên trình duyệt.
- Chạy lại 235 test: tất cả đạt, không skip. Bằng chứng local: TestResults/holiday-form-retest.trx. Có cảnh báo NU1900 do không truy cập được feed kiểm tra lỗ hổng NuGet; không có lỗi test.
