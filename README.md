# Salon Studio — Quản lý nhóm dịch vụ

Triển khai phạm vi quản lý nhóm dịch vụ bằng **Node.js + NestJS (REST API), React 18 + TypeScript + Vite, PostgreSQL 15, JWT + bcrypt**. Mã nguồn .NET cũ vẫn nằm trong `SalonManagement/`; bản TypeScript chạy độc lập tại `backend/` và `frontend/`, chưa chuyển dữ liệu từ hệ thống .NET.

## Chức năng

- Đăng nhập tài khoản Chủ tiệm; mật khẩu băm bcrypt cost 12, JWT hết hạn sau 8 giờ; giới hạn đăng nhập 5 lần/phút/IP.
- Thêm, sửa tên và thứ tự nhóm; tìm kiếm tên; danh sách sắp xếp thứ tự tăng dần.
- Xác nhận trước khi xoá; kiểm tra tại API và chặn nhóm còn dịch vụ đang bán, thông báo số lượng.
- Xoá nhóm chỉ có dịch vụ ngừng bán: giữ nguyên dịch vụ, chuyển chúng sang chưa phân nhóm.
- Có trạng thái tải, lỗi, danh sách trống, phản hồi thành công và giao diện cho điện thoại.
- Trang công khai `/dat-lich` không cần đăng nhập, hiển thị dịch vụ đang bán theo nhóm và thứ tự Chủ tiệm đã lưu; ẩn nhóm rỗng hoặc chỉ có dịch vụ ngừng bán.
- Trang khách đang mở tự cập nhật qua Server-Sent Events và PostgreSQL LISTEN/NOTIFY sau khi thay đổi được commit. Có tự kết nối lại, tải lại khi quay về tab và kiểm tra dự phòng mỗi 30 giây.

## Quy tắc cần PO xác nhận

Các quy tắc sau là mặc định triển khai, chưa phải quyết định đã được PO phê duyệt:

1. Tên có 1–100 ký tự; bỏ khoảng trắng đầu/cuối và gộp khoảng trắng liên tiếp; không trùng tên bất kể chữ hoa/thường. Dấu tiếng Việt vẫn phân biệt.
2. Thứ tự là số nguyên 0–9999. Cho phép trùng thứ tự; khi trùng, nhóm tạo trước đứng trước.
3. Áp dụng yêu cầu “không còn dịch vụ đang bán”: dịch vụ ngừng bán không chặn xoá, được giữ lại với nhóm trống. Phần yêu cầu “chưa có kiểm soát xoá” được giải quyết bằng kiểm tra đầy đủ để bảo vệ dữ liệu.
4. Bản này phục vụ một tiệm. Chưa có quản lý nhiều tiệm, giao diện quản lý từng dịch vụ hoặc quy trình chọn giờ/gửi yêu cầu đặt lịch.

## Chạy trên Windows

Cần Node.js 22+, pnpm 11+ và Docker Desktop đang chạy Linux containers (hoặc PostgreSQL 15 riêng).

```powershell
cd C:\Users\Admin\Documents\Codex\SalonManagement
Copy-Item backend/.env.example backend/.env
```

Sửa `backend/.env`: đặt `JWT_SECRET` ngẫu nhiên ít nhất 32 ký tự, `OWNER_EMAIL`, `OWNER_PASSWORD` riêng (ít nhất 12 ký tự, không quá 72 byte UTF-8). Có thể tạo JWT_SECRET bằng `node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"`.

```powershell
docker compose up -d db
pnpm install
pnpm dev
```

Mở http://localhost:5173 và đăng nhập bằng tài khoản trong `.env`. Tài khoản được tạo khi API khởi động lần đầu; thay đổi mật khẩu trong `.env` sau đó không đổi mật khẩu tài khoản đã có.

Máy này có thể chạy `powershell -ExecutionPolicy Bypass -File .\start.ps1`: script sử dụng Node/pnpm đã cài hoặc runtime Codex trên máy, kiểm tra cấu hình và chạy hai ứng dụng.

API chạy http://127.0.0.1:3000/api. Vite chuyển `/api` về backend. PostgreSQL lưu dữ liệu trong Docker volume `salon_pg15`. `docker compose stop` dừng cơ sở dữ liệu nhưng giữ dữ liệu; tránh xoá volume nếu muốn giữ dữ liệu.

## Kiểm tra

```powershell
pnpm build
pnpm test
$env:TEST_DATABASE_URL='postgresql://salon:salon_local@localhost:5432/salon_test'
pnpm test:e2e
```

Kiểm thử API dùng PostgreSQL thật và **xoá dữ liệu trong database `salon_test`**, không dùng database `salon`. File `database/init-test.sql` tạo database test khi khởi tạo volume lần đầu. Nếu dùng volume có sẵn, tạo `salon_test` riêng trước khi chạy test.

Các tình huống kiểm thử: đăng nhập sai/đúng, JWT thiếu/giả/hết hạn, quyền Chủ tiệm, tên rỗng/trùng, thứ tự sai, thêm ba nhóm mẫu, sửa, lưu PostgreSQL, xoá nhóm trống, chặn xoá nhóm đang bán, giữ dịch vụ ngừng bán, ID sai/không tồn tại.

## API

| Phương thức | Đường dẫn | Nội dung |
|---|---|---|
| POST | `/api/auth/login` | `{ "email": "…", "password": "…" }` → accessToken |
| GET | `/api/service-groups` | Danh sách có `serviceCount`, `activeServiceCount` |
| POST | `/api/service-groups` | `{ "name": "Tóc", "displayOrder": 1 }` |
| PUT | `/api/service-groups/:id` | Cập nhật tên và thứ tự |
| DELETE | `/api/service-groups/:id` | Xoá nếu không còn dịch vụ đang bán |
| GET | `/api/service-groups/:id/deletion-check` | Chủ tiệm: kiểm tra tổng số dịch vụ, số đang bán và quyền xoá hiện tại |
| GET | `/api/public/service-groups` | Công khai: nhóm và các dịch vụ đang bán, theo thứ tự hiển thị |
| GET | `/api/public/catalog-events` | Công khai: luồng SSE báo danh mục thay đổi |

Các API nhóm yêu cầu `Authorization: Bearer <accessToken>`. Mã lỗi: 400 dữ liệu không hợp lệ, 401 chưa đăng nhập, 403 không có quyền, 404 không tồn tại, 409 tên trùng/nhóm còn dịch vụ đang bán, 429 quá giới hạn.

Hai API `/api/public/*` không yêu cầu token. API danh mục dùng `Cache-Control: no-store`, chỉ trả ID, tên, thứ tự nhóm và ID/tên dịch vụ. Khi triển khai qua reverse proxy cần cho phép `text/event-stream`, tắt buffering cho `/api/public/catalog-events` và tăng timeout lớn hơn heartbeat 25 giây.

## Chặn xoá nhóm có dịch vụ đang bán

Hộp thoại xoá lấy lại dữ liệu từ máy chủ khi mở, tự cập nhật khi dịch vụ thay đổi và có nút **Kiểm tra lại**. Hiển thị riêng tổng số dịch vụ và số đang bán (ví dụ 7 dịch vụ tổng cộng, 5 đang bán). Chưa kiểm tra được hoặc còn dịch vụ đang bán thì nút xác nhận bị khoá.

`DELETE` luôn đếm lại trong transaction, khoá nhóm và các dịch vụ liên quan để chống thay đổi đồng thời. Khi bị chặn, trả HTTP 409 với `code: GROUP_HAS_ACTIVE_SERVICES`, `serviceCount`, `activeServiceCount`, `canDelete: false` và thông báo cụ thể; transaction rollback giữ nguyên dữ liệu. Khi còn 0 dịch vụ đang bán, nhóm được xoá và dịch vụ ngừng bán được giữ lại, bỏ liên kết nhóm.

Đã kiểm thử PostgreSQL với 1, 5, 0 dịch vụ đang bán, 5 đang bán + 2 ngừng bán; so sánh toàn bộ dữ liệu trước/sau xoá bị chặn; kiểm tra dịch vụ được bật bán trong một transaction đồng thời sau precheck. Kiểm thử trình duyệt xác nhận số lượng mới dù danh sách cũ, trạng thái 5 → 1 → 0, xung đột tại thời điểm xác nhận, dữ liệu được giữ lại, desktop và mobile.

## Demo trang công khai

Sau khi API khởi tạo schema, tạo dữ liệu mẫu (lệnh này chủ động đặt lại thứ tự ba nhóm mẫu nhưng không xoá dữ liệu):

```powershell
cd backend
node scripts/seed-booking-demo.cjs
```

Script thêm tám dịch vụ mẫu vào Chăm sóc da (1), Tóc (2), Gội dưỡng (3), không thêm lại dịch vụ cùng tên đã có trong nhóm. Không chạy tự động khi khởi động ứng dụng.

1. Mở `http://localhost:5173/dat-lich` trong cửa sổ khách không đăng nhập.
2. Mở trang Chủ tiệm ở cửa sổ khác, đổi thứ tự rồi lưu.
3. Xác nhận trang khách tự đổi thứ tự mà không tải lại; sau khi mở lại vẫn đúng thứ tự đã lưu.
4. Nhóm không có dịch vụ đang bán không xuất hiện; dịch vụ ngừng bán và dịch vụ chưa phân nhóm không xuất hiện.

Kiểm thử PostgreSQL trong `pnpm test:e2e` bao phủ thứ tự ba nhóm, tên dịch vụ đúng nhóm, trạng thái đang bán, chuyển nhóm, nhóm trống, thứ tự trùng, danh mục rỗng, cache header và SSE sau khi lưu. Đã kiểm thử trình duyệt desktop/mobile với hai phiên độc lập, đổi thứ tự trực tiếp, tìm kiếm, lỗi/tải lại và danh mục rỗng.

## Demo nghiệm thu

1. Đăng nhập Chủ tiệm. Thêm Tóc (3), Gội dưỡng (1), Chăm sóc da (2).
2. Kiểm tra thứ tự Gội dưỡng → Chăm sóc da → Tóc.
3. Sửa Tóc thành Tóc cao cấp, thứ tự 0; nhóm lên đầu danh sách.
4. Thử lưu tên chỉ có khoảng trắng và tên trùng để kiểm tra thông báo.
5. Chọn xoá nhóm trống, bấm Huỷ: nhóm còn nguyên. Xoá lại và xác nhận: nhóm biến mất.
6. Tải lại trang: thay đổi vẫn được lưu. Kiểm thử API bao phủ nhóm có dịch vụ đang bán/ngừng bán.

## Phạm vi triển khai

Đây là bản chạy cục bộ của tính năng yêu cầu, chưa phải toàn bộ hệ thống salon. Trước khi triển khai công khai cần HTTPS, cấu hình phục vụ frontend với proxy `/api`, quản lý bí mật, backup PostgreSQL và giới hạn đăng nhập dùng kho chung nếu chạy nhiều API instance. Schema được khởi tạo idempotent ở lần chạy đầu; các thay đổi schema tiếp theo cần migration được kiểm soát.
