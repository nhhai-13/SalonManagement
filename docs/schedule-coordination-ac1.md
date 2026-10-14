# AC 1 — Chọn thợ nghỉ và liệt kê lịch cần xử lý

## Phạm vi

Lễ tân chọn một thợ và ngày nghỉ, sau đó xem lịch hẹn của đúng thợ trong đúng ngày. Màn hình chỉ đọc danh sách; đề xuất thợ thay thế, đổi giờ, chuyển lịch và gửi thông báo nằm ngoài AC 1.

## Quy tắc trạng thái đang áp dụng

Repo hiện lưu lịch hẹn với các trạng thái `Pending`, `Confirmed`, `Arrived`, `Completed`, `Cancelled` và `NoShow` ở nhánh phát triển. AC 1 tạm đưa `Pending`, `Confirmed`, `Arrived` vào danh sách cần xử lý; loại trừ `Completed`, `Cancelled`/`Canceled`, `NoShow` và trạng thái đang phục vụ (`InProgress`, `In Progress`, `InService`). Thời điểm bắt đầu đã qua không tự loại lịch; trạng thái hoàn tất/hủy/đang phục vụ mới quyết định lịch có cần xem xét hay không.

Đây là quy tắc triển khai tạm thời, chưa được PO xác nhận. Repo hiện tại không có trạng thái đang phục vụ riêng; khi trạng thái đó được thêm, cần giữ nó ngoài danh sách chuyển lịch.

## Dữ liệu và API

- Dùng các bảng hiện có `Appointments`, `Customers`, `Stylists`, `AppointmentServices`, `Services`; không tạo bảng mới.
- `GET /api/schedule-coordination/stylists` trả danh sách thợ đang hoạt động.
- `GET /api/schedule-coordination/appointments?stylistId={id}&date=YYYY-MM-DD` trả danh sách, tổng số và thông tin khách, dịch vụ, giờ bắt đầu, thời lượng, trạng thái, liên hệ.
- API giới hạn quyền cho nhóm lễ tân/quản lý (`RoleGroups.FrontDesk`).

## Hạn chế kiến trúc

Ảnh yêu cầu kiến trúc Node.js/NestJS, React 18/TypeScript/Vite và PostgreSQL 15, nhưng repository ở nhánh `main` hiện là ASP.NET Core MVC .NET 8 với Entity Framework và SQL Server. AC 1 được tích hợp theo kiến trúc hiện có để không thay toàn bộ ứng dụng. Cần PO xác nhận có yêu cầu chuyển stack hay không.
