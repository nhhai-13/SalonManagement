# S2-03 — Danh sách dịch vụ công khai

## Demo
- Mở `/Services` khi chưa đăng nhập; `/Services/Public` luôn chọn giao diện danh sách công khai, kể cả với Owner.
- Trang hiển thị tên nhóm trước các dịch vụ, tên dịch vụ, thời lượng phút và giá theo định dạng `150.000 VND`.
- Nhóm không còn dịch vụ hợp lệ tự ẩn; khi không có dịch vụ nào, hiển thị “Chưa có dịch vụ nào để hiển thị.”.
- Dịch vụ chưa gán nhóm nằm trong “Chưa phân nhóm”, cuối danh sách.

## Thứ tự đề xuất — chờ PO xác nhận
- Nhóm: `DisplayOrder` tăng dần, rồi `ServiceGroupId` để thứ tự ổn định khi trùng số.
- Dịch vụ: tên tăng dần theo collation của database, rồi `ServiceId` khi trùng tên.
- Owner chỉnh thứ tự nhóm tại `/ServiceGroups`. Chưa có thứ tự dịch vụ tùy chỉnh.

## Phạm vi
- Chỉ hiển thị dịch vụ đang bán và có ít nhất một thợ đang làm việc đảm nhận. Dịch vụ ngừng bán, chưa có thợ hoặc toàn bộ thợ đã ngưng làm việc đều bị ẩn.
- Trang chủ và lựa chọn dịch vụ đặt lịch áp dụng cùng điều kiện lọc. Không hiển thị dịch vụ mẫu khi danh sách trống. Trạng thái được cập nhật mỗi lần tải trang.
- Tìm theo toàn bộ hoặc một phần tên, không phân biệt hoa/thường và dấu tiếng Việt (bao gồm đ/d). Nhập từ khóa rồi nhấn Tìm kiếm hoặc Enter; Xóa từ khóa để khôi phục danh sách hợp lệ. Nhóm không có kết quả tự ẩn; không có kết quả sẽ có thông báo riêng.
- Chưa nghiệm thu màn hình 360px hoặc thời gian tải dưới 2 giây trên mạng 4G.
- Không thay đổi schema hoặc dữ liệu production.

## Kiểm thử
Chạy `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj`.

`PublicServiceCatalogTests` khởi động MVC/Razor thật trên HTTP với database InMemory, chính sách mặc định yêu cầu đăng nhập và client không có cookie. Kiểm tra cả `/Services` và `/Services/Public`: HTTP 200, không nhóm/không dịch vụ, nhóm rỗng, một nhóm nhiều dịch vụ, nhiều nhóm, thứ tự nhóm và dịch vụ, tên, phút, dấu phân cách hàng nghìn và VND. Bao gồm dịch vụ ngừng bán/chưa có thợ và dịch vụ chưa phân nhóm.

`ServiceGroupsControllerTests` kiểm tra thay đổi thứ tự nhóm được phản ánh trên danh sách công khai, đồng thời trang chủ áp dụng cùng bộ lọc khả dụng.

Các test dùng InMemory, chưa thay thế kiểm thử trên SQL Server staging hoặc nghiệm thu giao diện bằng trình duyệt.

`PublicServiceAvailabilityTests` kiểm tra dịch vụ đang bán/ngừng bán, không có thợ, tất cả thợ ngưng làm việc, nhiều thợ có ít nhất một người đang làm việc, nhóm rỗng sau lọc, cập nhật trạng thái và gỡ phân công. Owner vẫn xem đầy đủ danh sách quản lý.

`PublicServiceSearchTests` kiểm tra tên đầy đủ, tên một phần, hoa/thường, có dấu/không dấu, Unicode tổ hợp, đ/d, không có kết quả và xóa từ khóa. Kiểm thử HTTP xác nhận form, giá trị từ khóa, thông báo rỗng và khôi phục danh sách trên cả hai đường dẫn. Tìm kiếm chuẩn hóa trong bộ nhớ sau khi truy vấn các dịch vụ hợp lệ để không phụ thuộc collation; chưa đánh giá hiệu năng với danh mục lớn.
