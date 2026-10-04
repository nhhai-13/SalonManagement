# Báo cáo test lại S2-01 đến S2-09

Code kiểm tra: `8788815` cùng 3 kiểm thử bổ sung. Ngày kiểm tra: 04/10/2026.

## Môi trường và bằng chứng

- .NET: `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj --no-restore`.
- Kết quả: **231 đạt, 0 thất bại, 0 bỏ qua**. Đây là tổng toàn bộ bộ test, bao gồm Sprint 1 và đăng nhập; không phải 231 tiêu chí riêng của Sprint 2.
- TRX: `TestResults/sprint2-expanded-retest.trx` (local, không đưa vào Git).
- HTTP demo tại localhost:5186 dùng EF InMemory, không dùng database thật của nhóm.
- SQL Server: database thử riêng `SalonManagement_S206_Test_20261004`; chèn thử trong transaction rồi rollback.
- Cảnh báo NU1900: không tải được dữ liệu cảnh báo bảo mật NuGet; test vẫn chạy thành công.

## Kết quả theo phần

| Phần | Đã kiểm tra | Kết luận |
|---|---|---|
| S2-01 | Tạo/sửa ca, ca chồng lấn, chạm biên, giờ mở cửa/ngày đóng cửa, sao chép tuần, xem trước và xóa ca có lịch hẹn bằng controller tests | Đạt các ca tự động; chưa kiểm tra toàn bộ giao diện chủ tiệm với tài khoản thật |
| S2-02 | Ngày nghỉ tiệm loại giờ trống và chặn đặt lịch; trùng ngày nghỉ; khoảng nghỉ sai/trùng; nghỉ cả ngày và nghỉ một phần của thợ | Đạt các ca tự động; xử lý ngày nghỉ tiệm đang có lịch hẹn cần bổ sung kiểm chứng (xem bên dưới) |
| S2-03 | Nhóm dịch vụ, dịch vụ đang bán/có thợ, tìm tiếng Việt không dấu, dữ liệu rỗng; xem trang thật ở 360px | Logic đạt; **mobile không đạt**; tốc độ 4G chưa kiểm chứng |
| S2-04 | Tổng thời lượng/giá, chọn dịch vụ, giới hạn 5 dịch vụ, cảnh báo vượt ca làm | Đạt các ca tự động |
| S2-05 | Giờ trống, lịch bận, ngày nghỉ/giờ nghỉ, thời gian đặt trước 60 phút, ngày gợi ý; giờ nhập theo phút | Đạt các ca tự động |
| S2-06 | Đủ kỹ năng, thợ cụ thể/bất kỳ, tự gán theo số lịch đang hoạt động và ID, thay đổi dịch vụ xoá lựa chọn cũ | Đạt các ca tự động và luồng trình duyệt đã thử |
| S2-07 | Xác thực thông tin, giới hạn lịch chưa hoàn tất, mã lịch; trình duyệt gửi xác nhận và hiện mã HB2ZSCPV ở lần thử trước trên cùng code | Đạt các ca tự động và luồng demo; thông báo hiện trên trang, không phải email/SMS |
| S2-08 | 20 lần gọi service đồng thời: 1 thành công/19 trùng giờ; SQL trigger chặn chèn trực tiếp; 20 POST HTTP đồng thời | Service và trigger đạt; HTTP: 1 thành công, 3 HTTP409, 16 HTTP429; chưa đạt tiêu chí 19 phản hồi trùng giờ ở tầng HTTP |
| S2-09 | Tra mã không phân biệt hoa/thường, chi tiết thợ/dịch vụ/thời lượng; tra điện thoại lọc lịch kết thúc và sắp theo giờ; dữ liệu không tồn tại/sai; giới hạn tra sai | Đạt các ca tự động; chưa chạy đầy đủ UI tra cứu trên SQL Server |

## Lỗi và khoảng trống kiểm chứng

1. **S2-03: cuộn ngang ở 360px.** Trên `/Services/Public`, `innerWidth=360`, `document.documentElement.scrollWidth=454`. Thanh điều hướng vượt khung hình. Không sửa giao diện trong lần test này.
2. **S2-08: phản hồi test HTTP không giống tiêu chí service.** 20 request đồng thời cùng IP, thợ 2, ngày 05/10/2026, giờ 15:07: 1 HTTP200, 3 HTTP409, 16 HTTP429. File chi tiết: `TestResults/sprint2-http-concurrency.json`. Không có bằng chứng tạo nhiều hơn một lịch. Giới hạn IP đã ngăn đa số yêu cầu trước tầng đặt lịch; cần bài test nhiều IP hoặc harness phù hợp để kiểm chứng tranh chấp HTTP độc lập với rate limit.
3. **S2-08: chưa kiểm chứng nhiều instance/process.** `AppointmentBookingService` dùng semaphore static trong một process. Trigger SQL tồn tại và chặn chèn tuần tự trùng lịch (lỗi 51000), nhưng kết quả này chưa chứng minh xử lý tranh chấp giữa nhiều instance SQL Server. Chưa đo đồng thời SQL Server với 20 request.
4. **S2-08: luồng lỗi cần kiểm tra thêm.** Khi API trả `reloadSlots`, JavaScript hiện giấu phần thông tin khách và yêu cầu nhập/chọn lại giờ; chưa tự kiểm tra lại giờ như yêu cầu kịch bản gốc. Giá trị khách vẫn còn trong DOM. Đây là nhận xét từ code, chưa tái hiện cuộc đua bằng hai trình duyệt.
5. **S2-02: ngày nghỉ tiệm đang có lịch hẹn.** `ShopHolidaysController.Create` chỉ kiểm tra trùng ngày rồi lưu; chưa thấy kiểm tra/cảnh báo lịch bị ảnh hưởng. Chưa chốt đạt tiêu chí xử lý lịch hiện hữu; lịch nghỉ của thợ có kiểm tra conflict riêng.
6. **S2-03: dưới 2 giây trên 4G.** Nhóm chưa có staging. Localhost không thay thế phép đo thực tế trên 4G, vì vậy không đánh dấu đạt.

## Kết luận

Bộ test tự động hiện có và các test bổ sung đều đạt. **Chưa thể xác nhận toàn bộ Sprint 2 đạt**, do lỗi mobile, khác biệt phản hồi concurrency HTTP và các tiêu chí còn thiếu môi trường/bằng chứng. Lần kiểm tra này chỉ bổ sung test và báo cáo, không sửa giao diện hoặc logic sản phẩm.
