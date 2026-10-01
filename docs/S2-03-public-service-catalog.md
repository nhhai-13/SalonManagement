# S2-03 — Danh sách dịch vụ công khai

## Demo
- Mở `/Services` khi chưa đăng nhập; `/Services/Public` luôn chọn giao diện danh sách công khai, kể cả với Owner.
- Trang hiển thị tên nhóm trước các dịch vụ, tên dịch vụ, thời lượng phút và giá theo định dạng `150.000 VND`.
- Nhóm rỗng hiển thị “Nhóm này chưa có dịch vụ.”; khi không có dịch vụ nào, hiển thị “Chưa có dịch vụ nào để hiển thị.”.
- Dịch vụ chưa gán nhóm nằm trong “Chưa phân nhóm”, cuối danh sách.

## Thứ tự đề xuất — chờ PO xác nhận
- Nhóm: `DisplayOrder` tăng dần, rồi `ServiceGroupId` để thứ tự ổn định khi trùng số.
- Dịch vụ: tên tăng dần theo collation của database, rồi `ServiceId` khi trùng tên.
- Owner chỉnh thứ tự nhóm tại `/ServiceGroups`. Chưa có thứ tự dịch vụ tùy chỉnh.

## Phạm vi
- Danh sách này không lọc theo trạng thái dịch vụ hoặc thợ; dịch vụ ngừng bán và chưa có thợ vẫn hiển thị theo phạm vi S2-03.
- Trang chủ giữ nguyên cách chọn dịch vụ nổi bật hiện có.
- Chưa triển khai tìm kiếm không dấu; chưa nghiệm thu màn hình 360px hoặc tốc độ tải.
- Không thay đổi schema hoặc dữ liệu production.

## Kiểm thử
Chạy `dotnet test SalonManagement.Tests/SalonManagement.Tests.csproj`.

`PublicServiceCatalogTests` khởi động MVC/Razor thật trên HTTP với database InMemory, chính sách mặc định yêu cầu đăng nhập và client không có cookie. Kiểm tra cả `/Services` và `/Services/Public`: HTTP 200, không nhóm/không dịch vụ, nhóm rỗng, một nhóm nhiều dịch vụ, nhiều nhóm, thứ tự nhóm và dịch vụ, tên, phút, dấu phân cách hàng nghìn và VND. Bao gồm dịch vụ ngừng bán/chưa có thợ và dịch vụ chưa phân nhóm.

`ServiceGroupsControllerTests` kiểm tra thay đổi thứ tự nhóm được phản ánh trên danh sách công khai, đồng thời trang chủ vẫn giữ bộ lọc hiện có.

Các test dùng InMemory, chưa thay thế kiểm thử trên SQL Server staging hoặc nghiệm thu giao diện bằng trình duyệt.
