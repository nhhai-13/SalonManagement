# Nhật ký diễn tập staging

| Mã lỗi | Dấu hiệu cũ | Khắc phục | Trạng thái kiểm chứng |
|---|---|---|---|
| BUG-01 | Thợ mở lịch từ thông báo S4-06 nhận `403 Forbidden`. | API `/api/stylist/notifications` và cổng thợ yêu cầu đúng role `Stylist`; thêm hồi quy RBAC. | `FIXED` — được kiểm bởi `StagingRbacMatrixTests`. |
| BUG-02 | Báo cáo doanh thu rỗng khi lọc tháng trước. | Seed staging có ba phiếu đã thanh toán `STG-PREV-*` trong tháng trước. | `FIXED` — kiểm tra tại `/owner/revenue-report` với bộ lọc tháng trước. |
| BUG-03 | Email xác nhận có thể chậm hơn 2 phút. | Worker hàng đợi xử lý chu kỳ 30 giây; trạng thái gửi/lần thử lại lưu trên lịch. | `FIXED` — xác nhận worker và SMTP staging đang hoạt động trước buổi demo. |

## Bằng chứng cần ghi sau lần dry-run chính thức

- Thời điểm bắt đầu/kết thúc, URL staging và người chạy.
- Kết quả `dotnet test` của toàn bộ bộ kiểm thử.
- Ảnh hoặc mã lịch của từng bước trong `DEMO-RUNBOOK.md`.
- Trạng thái email xác nhận thực tế trong vòng 2 phút và log retry (nếu có).

> Ghi chú: nhật ký này mô tả trạng thái mã nguồn đã được chuẩn bị. Việc xác nhận SMTP và triển khai trên máy chủ staging thật phải được chạy một lần với secret/hạ tầng của môi trường trước nghiệm thu.
