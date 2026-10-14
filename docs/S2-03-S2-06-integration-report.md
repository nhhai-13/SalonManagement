# Kiểm thử tích hợp S2-03 và S2-06 — 2026-10-02

Đã fetch tất cả remote, kiểm tra chung trên nhánh cục bộ `test/s2-03-s2-06-integration`, HEAD `4a8f483`. Các nhánh tính năng vẫn riêng biệt, không force-push hoặc sửa lịch sử của nhóm.

| Lát | Nhánh | Commit tính năng |
|---|---|---|
| S2-03 danh sách nhóm | feature/s2-03-public-service-catalog | fce9c81 |
| S2-03 lọc khả dụng | fix/s2-03-service-availability | b9f5a20 |
| S2-03 tìm kiếm | feature/s2-03-vietnamese-service-search | 815490b |
| S2-03 360px/hiệu năng | feature/s2-03-mobile-performance | 422192b |
| S2-06 đích danh | feature/s2-06-specific-stylist | 4d2ea38 |
| S2-06 bất kỳ | feature/s2-06-any-stylist | 47edfbc |
| S2-06 tự gán | feature/s2-06-auto-assign-stylist | a3f5ac2 |

Develop tại thời điểm kiểm thử: `d9282f4`; sáu nhánh cũ đã được chứa trong develop. Nhánh tích hợp bổ sung Lát 3 tự gán và xác nhận đủ các nhánh trên, không có xung đột merge.

## Kết quả

- Toàn bộ test .NET: **151 passed, 0 failed, 0 skipped**.
- Booking browser (Edge headless): giao kỹ năng, giờ từng thợ, hợp giờ, A kín/B trống, đổi dịch vụ/ngày/thợ, request cũ, xác nhận C/B/A theo workload đều đạt; không có lỗi JavaScript.
- Catalog browser: 360px, 100 dịch vụ/10 nhóm; không tràn ngang, tên dài/giá/thời lượng không bị cắt, tìm kiếm và xóa từ khóa đạt.
- Mạng mô phỏng 1,6 Mbps down / 750 Kbps up / 150ms, browser cache trống: 954,4 / 623,7 / 615,9 / 618,6 / 633,3 ms; 2 request và 70.955 byte/lượt.
- Đã quay lại nhánh tính năng tự gán để push PR riêng; không push nhánh tích hợp vào develop.

## Giới hạn

Test dùng EF InMemory, host localhost và Edge headless. Chưa nghiệm thu SQL Server staging/điện thoại thật; bộ mẫu hiệu năng và quy tắc gán thợ chưa có PO xác nhận. Tự gán hiện trả quyết định cho màn hình xác nhận, chưa lưu lịch hẹn/giữ chỗ. Không thể kết luận toàn story đã nghiệm thu từ kết quả này.
