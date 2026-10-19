# Kịch bản demo nghiệm thu end-to-end

## Chuẩn bị

- Mở staging và xác nhận seed đã tạo lịch `STG-*`, mã giảm giá `DEMO10` và tài khoản trong [README](README.md).
- Mở song song bốn cửa sổ/phiên riêng tư để tránh lẫn phiên đăng nhập giữa các vai trò.
- Các bước sau ưu tiên tạo một lịch mới để chứng minh luồng vận hành; dữ liệu `STG-*` chỉ là phương án dự phòng để trình bày báo cáo và trạng thái.

| Bước | Hành động | Vai trò / tài khoản | Kết quả cần thấy |
|---:|---|---|---|
| 1 | Đặt lịch trực tuyến, chọn dịch vụ và Thợ Demo | `customer_demo@luminol.vn` / `Demo123!` | Có mã tra cứu và lịch ở trạng thái chờ xác nhận. |
| 2 | Duyệt lịch, mở lịch ngày, check-in khách | `letan_demo@luminol.vn` / `Demo123!` | Lịch chuyển sang đã đến; hóa đơn nháp được tạo ngay khi check-in. |
| 3 | Mở chuông thông báo, đi vào đúng lịch, bắt đầu phục vụ | `stylist_demo@luminol.vn` / `Demo123!` | Thông báo check-in không trả 403; deep link mở đúng lịch cá nhân. |
| 4 | Thêm dịch vụ phát sinh vào hóa đơn, áp `DEMO10`, chọn thanh toán và xác nhận | `letan_demo@luminol.vn` / `Demo123!` | Hiện tổng gốc, giảm giá, tổng thực thu; mã giao dịch tự sinh và phiếu đóng. |
| 5 | Mở biểu mẫu đánh giá bằng mã tra cứu; gửi điểm và nhận xét | Khách hàng | Thang 1–5 sao và giới hạn 500 ký tự hoạt động; link/mã dùng một lần. |
| 6 | Mở dashboard, doanh thu tháng này/tháng trước và báo cáo cao điểm | `owner_demo@luminol.vn` / `Demo123!` | Có số liệu, drill-down trạng thái, CSV UTF-8 và dữ liệu tháng trước không rỗng. |

## Dữ liệu dự phòng sẵn có

| Mã | Mục đích |
|---|---|
| `STG-PENDING`, `STG-CONFIRMED`, `STG-ARRIVED`, `STG-PAID`, `STG-NOSHOW`, `STG-CANCELLED` | Sáu trạng thái trên dashboard vận hành hôm nay. |
| `STG-PREV-01` đến `STG-PREV-03` | Doanh thu đã thanh toán tháng trước. |
| `DEMO10` | Mã giảm 10% còn hiệu lực. |
| `STG-PAID` | Mã lịch mở biểu mẫu đánh giá và thông báo check-in cho Thợ Demo. |

## Tiêu chí kết thúc diễn tập

Toàn bộ bước hoàn thành, không có lỗi 403 với tài khoản đúng vai trò, không có khu vực báo cáo rỗng bất thường và không có thông báo lỗi JavaScript/HTTP 5xx trên trình duyệt.
