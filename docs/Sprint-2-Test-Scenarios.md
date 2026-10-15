# Kịch bản kiểm thử Sprint 2

## 1. Phạm vi và chuẩn bị

Tài liệu này bao phủ Product Backlog S2-01 đến S2-09 của ứng dụng .NET.

- Chạy ứng dụng bằng profile Development/Staging với dữ liệu demo.
- Tạo tối thiểu hai thợ đang hoạt động, hai dịch vụ đang bán, ca làm việc và tài khoản khách thử nghiệm.
- Mỗi ca có mã riêng; ghi **Pass/Fail**, dữ liệu thực tế và ảnh chụp nếu thất bại.
- Những ca mang nhãn **Tự động** được chạy bằng `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj`.

## S2-01 — Lịch làm việc theo tuần của thợ

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S201-01 | Tạo ca hợp lệ | Chọn thợ, tuần và thêm ca 08:00–12:00 trong ngày làm việc. | Ca xuất hiện đúng ngày, giờ và thợ. |
| S201-02 | Chặn ca chồng lấn | Với ca 08:00–12:00, thêm ca 11:00–13:00 cùng ngày/cùng thợ. | Lưu bị từ chối, không phát sinh ca thứ hai. |
| S201-03 | Chặn ngoài giờ hoạt động | Thêm ca nằm ngoài giờ mở cửa hoặc ngày tiệm đóng cửa. | Hiển thị lỗi nghiệp vụ; không lưu. |
| S201-04 | Sao chép tuần | Sao chép lịch tuần có nhiều ca sang tuần kế tiếp. | Các ca được sao chép đúng ngày tương ứng; ngày có ca xung đột được báo rõ. |
| S201-05 | Xóa ca có lịch hẹn | Tạo lịch hẹn nằm trong ca rồi yêu cầu xóa ca. | Hệ thống chặn xóa và hiển thị lịch liên quan. |

## S2-02 — Ngày nghỉ tiệm và nghỉ của thợ

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S202-01 | Tạo ngày nghỉ tiệm | Thêm một ngày nghỉ cho tiệm. | Ngày nghỉ được lưu và không còn khung giờ đặt lịch. |
| S202-02 | Trùng ngày nghỉ tiệm | Thêm lại cùng ngày nghỉ. | Bị từ chối hoặc cập nhật rõ ràng; không có bản ghi trùng. |
| S202-03 | Nghỉ của một thợ | Đăng ký nghỉ cho thợ A, giữ thợ B làm việc. | Thợ A không xuất hiện/không có giờ trống; thợ B vẫn đặt được. |
| S202-04 | Khoảng nghỉ không hợp lệ | Nhập ngày kết thúc trước ngày bắt đầu hoặc thiếu lý do bắt buộc. | Kiểm tra dữ liệu chặn lưu và nêu lỗi. |
| S202-05 | Ngày nghỉ khi đang có lịch | Tạo ngày nghỉ trùng lịch hẹn đang giữ chỗ. | Hệ thống xử lý theo quy tắc nghiệp vụ, không âm thầm làm mất lịch. |

## S2-03 — Danh mục dịch vụ công khai

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S203-01 | Hiển thị theo nhóm | Mở danh mục khi có nhiều nhóm/dịch vụ đang bán. | Chỉ nhóm và dịch vụ đang bán hiển thị, thứ tự đúng cấu hình. |
| S203-02 | Ẩn dịch vụ không khả dụng | Ngừng bán dịch vụ hoặc vô hiệu toàn bộ thợ thực hiện. | Dịch vụ không xuất hiện ở trang công khai. |
| S203-03 | Tìm không dấu | Tìm chuỗi không dấu của tên dịch vụ tiếng Việt. | Trả đúng dịch vụ có dấu tương ứng. |
| S203-04 | Trạng thái rỗng | Không có dịch vụ công khai. | Hiển thị thông báo rỗng dễ hiểu, không lỗi trang. |
| S203-05 | Mobile 360px | Mở trang ở độ rộng 360px, tìm và chọn dịch vụ. | Không tràn ngang; thao tác và thông tin vẫn đọc được. |

## S2-04 — Chọn dịch vụ và tính tổng

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S204-01 | Chọn một/nhiều dịch vụ | Chọn rồi bỏ chọn các dịch vụ. | Danh sách đã chọn, tổng thời lượng và tổng tiền cập nhật tức thời. |
| S204-02 | Tối đa năm dịch vụ | Chọn năm dịch vụ, thử chọn dịch vụ thứ sáu. | Dịch vụ thứ sáu bị chặn; thông báo giới hạn hiển thị. |
| S204-03 | Xác minh tổng ở máy chủ | Gọi lại tính tổng sau khi thay đổi lựa chọn. | Tổng máy chủ khớp dữ liệu dịch vụ đang bán; ID không hợp lệ bị từ chối. |
| S204-04 | Cảnh báo ca dài nhất | Chọn dịch vụ có tổng thời lượng lớn hơn ca dài nhất. | Có cảnh báo nhưng không làm sai tổng tiền/thời lượng. |
| S204-05 | Dịch vụ đổi trạng thái | Chọn dịch vụ rồi chuyển dịch vụ sang ngừng bán, gửi lại yêu cầu. | Máy chủ yêu cầu chọn lại, không tính dịch vụ đã ngừng bán. |

## S2-05 — Khung giờ khả dụng

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S205-01 | Tính khung giờ theo thời lượng | Chọn dịch vụ 30 phút trong ca 08:00–10:00. | Chỉ trả các giờ bắt đầu cách 15 phút và kết thúc trong ca. |
| S205-02 | Loại giờ đã đặt | Có lịch 09:00–09:30, tải giờ trống. | Các giờ chồng toàn phần hoặc một phần với lịch bị loại. |
| S205-03 | Giờ mở cửa/nghỉ | Tải lịch ngày đóng cửa, giờ nghỉ hoặc ngoài giờ hoạt động. | Không trả giờ trống không hợp lệ. |
| S205-04 | Thời gian chuẩn bị | Thử chọn giờ sát thời điểm hiện tại hơn thời gian chuẩn bị. | Giờ đó không xuất hiện. |
| S205-05 | Gợi ý ngày | Chọn ngày hết chỗ. | Hiển thị các ngày gần nhất còn chỗ, có giờ sớm nhất. |

## S2-06 — Chọn thợ và tự gán thợ

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S206-01 | Lọc thợ đủ kỹ năng | Chọn hai dịch vụ; chỉ thợ A thực hiện đủ cả hai. | Chỉ thợ A và lựa chọn “Thợ bất kỳ” hợp lệ hiển thị. |
| S206-02 | Thợ cụ thể | Chọn thợ A, ngày và giờ trống. | Chỉ giờ trống thuộc ca của A hiển thị. |
| S206-03 | Thợ bất kỳ | Chọn “Thợ bất kỳ” với hai thợ có giờ khác nhau. | Danh sách là hợp giờ trống của ít nhất một thợ, không ghép thời gian giữa hai thợ. |
| S206-04 | Tự gán | Chọn giờ thuộc nhiều thợ. | Gán thợ theo số lịch trong ngày thấp nhất, sau đó ID nhỏ hơn. |
| S206-05 | Thợ bị thay đổi | Thợ được chọn nghỉ việc/không còn đủ kỹ năng trước khi xác nhận. | Thông báo chọn lại, không giữ lựa chọn cũ. |

## S2-07 — Xác nhận đặt lịch

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S207-01 | Dữ liệu khách hợp lệ | Gửi họ tên, số điện thoại 10 số, dịch vụ, ngày/giờ hợp lệ. | Tạo lịch và trả mã tra cứu 8 ký tự. |
| S207-02 | Xác thực dữ liệu | Thử tên rỗng/quá dài, điện thoại sai, email sai, ghi chú quá 300 ký tự. | Trả lỗi theo trường, không tạo lịch. |
| S207-03 | Giới hạn lịch chưa hoàn tất | Cùng số điện thoại đã có ba lịch Pending/Confirmed/InProgress. | Lịch thứ tư bị từ chối; lịch Cancelled/Completed/NoShow không tính. |
| S207-04 | Chống spam | Gửi quá năm yêu cầu đặt từ một IP trong một giờ. | Yêu cầu vượt giới hạn trả HTTP 429. |
| S207-05 | Mã tra cứu duy nhất | Tạo nhiều lịch hợp lệ. | Mỗi lịch có mã duy nhất và có thể dùng để tra cứu. |

## S2-08 — Tranh chấp khung giờ đồng thời

Các ca S208-01 đến S208-04 đối chiếu với [SCRUM-105](https://ictu-team-n3.atlassian.net/browse/SCRUM-105) đến [SCRUM-108](https://ictu-team-n3.atlassian.net/browse/SCRUM-108).

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S208-01 | Hai khách cùng giờ/cùng thợ | Gửi đồng thời hai yêu cầu vào cùng khoảng thời gian. | Đúng một yêu cầu thành công; yêu cầu còn lại nhận `slot_unavailable`; không có lịch dở dang. |
| S208-02 | Chạm đầu-cuối | Tạo lịch 09:00–09:30, sau đó 09:30–10:00 cùng thợ. | Cả hai thành công; không coi là chồng lấn. |
| S208-03 | Ràng buộc SQL Server | Bỏ qua tầng ứng dụng và chèn trực tiếp hai lịch chồng cùng thợ. | Trigger cơ sở dữ liệu từ chối lịch thứ hai; thợ khác vẫn được phép. |
| S208-04 | Hai mươi yêu cầu đồng thời — **Tự động** | Phát 20 yêu cầu cùng thợ/cùng giờ. | 1 thành công, 19 `slot_unavailable`, cơ sở dữ liệu có đúng 1 lịch. |
| S208-05 | Làm mới giờ trống | Nhận lỗi `slot_unavailable` khi chốt lịch. | API trả `reloadSlots=true`; giao diện tải lại giờ và giữ dữ liệu khách đã nhập. |

## S2-09 — Tra cứu lịch hẹn

| Mã | Kịch bản | Bước thực hiện | Kết quả mong đợi |
|---|---|---|---|
| S209-01 | Tra mã tham chiếu | Nhập mã lịch 8 ký tự hợp lệ. | Trả đúng ngày, giờ, thợ, dịch vụ và trạng thái. |
| S209-02 | Tra số điện thoại | Nhập số điện thoại hợp lệ của khách có nhiều lịch. | Trả các lịch chưa hoàn tất, sắp theo ngày/giờ. |
| S209-03 | Dữ liệu sai/không có | Nhập mã sai định dạng hoặc số không tồn tại. | Thông báo rõ, không lộ dữ liệu khác. |
| S209-04 | Loại lịch đã kết thúc | Tra điện thoại có lịch Cancelled/Completed/NoShow. | Các lịch này không xuất hiện trong danh sách đang xử lý. |
| S209-05 | Chặn tra cứu sai liên tiếp | Gửi nhiều tra cứu không hợp lệ từ một IP. | Bị chặn tạm thời với HTTP 429 và thời điểm thử lại. |

## 3. Tiêu chí hoàn tất

1. Toàn bộ ca trọng yếu Pass trên Chrome desktop và màn hình 360px.
2. Bộ kiểm thử .NET hoàn tất không lỗi.
3. Các ca S208-03 chạy với SQL Server thật; InMemory không kiểm chứng được trigger SQL Server.
4. Lưu ảnh lỗi, request/response API và mã lịch hẹn mẫu cho các ca thất bại.
