# S2-03 — Lát 4: 360px và hiệu năng

## Thay đổi

- Trang công khai dùng layout và CSS riêng, font hệ thống; không tải Bootstrap, jQuery, icon, Google Fonts hay JavaScript.
- Grid một cột ở 360px, hai cột từ 640px, ba cột từ 960px. Ô tìm kiếm co giãn, nút cao tối thiểu 44px. Tên dài và chuỗi không có khoảng trắng tự xuống dòng; không dùng overflow hidden để che lỗi.
- Query chỉ lấy ID, nhóm, tên, mô tả, thời lượng và giá. Giữ bộ lọc trạng thái dịch vụ/thợ, tìm kiếm không dấu và thứ tự nhóm.

## Kết quả đo thử ngày 2026-10-01

Chưa có xác nhận PO về kích thước dữ liệu nghiệm thu. Dùng **100 dịch vụ / 10 nhóm** giả lập, có tên dịch vụ dài 208 ký tự, tên nhóm 100 ký tự liền nhau, giá 20.000.000 VND và thời lượng 240 phút.

- Edge Chromium 154.0.4258.37 headless; viewport 360 × 800; cache trình duyệt trống mỗi lượt.
- Mạng mô phỏng: download 1,6 Mbps, upload 750 Kbps, latency 150ms. CPU không throttle.
- Host localhost chạy MVC/Razor thật và tài nguyên thật, EF InMemory; server đã khởi động và làm nóng. Không đại diện độ trễ SQL Server/hosting sản xuất hay thiết bị điện thoại thật.
- Mốc hoàn tất: navigation start đến sau load, fonts ready, phần tử dịch vụ cuối có mặt và hai requestAnimationFrame. Toàn bộ danh sách được render server, không lazy load.
- Năm lượt: **588,7 / 587,4 / 572,5 / 560,7 / 562,4 ms**; đều dưới 2 giây trong môi trường đo thử.
- 2 request/lượt, tổng transferSize 70.890 byte. Không tải tài nguyên ngoài origin.
- 100 thẻ, 10 nhóm; document.scrollWidth = innerWidth = 360; không phần tử tràn ngang. Tên dài, giá, thời lượng không bị cắt. Tìm `cat toc` và xóa từ khóa đạt.
- 118/118 kiểm thử .NET đạt; kiểm thử HTTP xác nhận layout không tải framework/font/script dư thừa.

## Chạy lại

1. Chạy `dotnet run --project tools/CatalogProbe/CatalogProbe.csproj --no-launch-profile` từ thư mục repository. Host chỉ tạo dữ liệu InMemory, không sửa database thật. Có thể đặt `CATALOG_PROBE_SERVICES` và `CATALOG_PROBE_GROUPS` trước khi chạy.
2. Với Node.js, Playwright và Microsoft Edge có sẵn, chạy `node tools/catalog-mobile-check.cjs`. Nếu Playwright không nằm trong node_modules cục bộ, đặt `NODE_PATH` đến thư mục chứa package. Dùng cùng biến số lượng mẫu cho script.
3. Kết quả và ảnh nằm trong `artifacts/catalog-mobile/`. Script báo lỗi nếu tràn/cắt nội dung, sai số thẻ/nhóm, tìm kiếm lỗi hoặc có lượt vượt 2 giây.

## Nghiệm thu còn chờ

PO cần xác nhận số dịch vụ, số nhóm, độ dài nội dung và cấu hình mạng/thiết bị nghiệm thu. Sau đó chạy trên staging với SQL Server và dữ liệu đã chốt, ghi lại các lượt tải từ lúc mở trang đến hiển thị đủ danh sách (bao gồm trường hợp server lạnh nếu nằm trong phạm vi). **Chưa đánh dấu AC tốc độ hoặc toàn bộ User Story hoàn tất** chỉ từ phép đo giả lập này.
