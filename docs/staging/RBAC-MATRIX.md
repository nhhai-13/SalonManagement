# Ma trận phân quyền API staging

| Bề mặt API / màn hình | Khách hàng | Lễ tân | Thợ | Chủ tiệm |
|---|:---:|:---:|:---:|:---:|
| Đặt lịch, tra cứu lịch, kiểm tra thợ trống | ✓ công khai | ✓ | ✓ | ✓ |
| Biểu mẫu đánh giá công khai (`/reviews`) | ✓ công khai | ✓ | ✓ | ✓ |
| Lịch ngày, lịch chờ duyệt, đặt lịch tại quầy, nhật ký email | — | ✓ | — | — |
| Hồ sơ khách hàng, lịch ngày (`/reception/daily-schedule`) | — | ✓ | — | ✓ |
| Cổng thợ, lịch cá nhân, thông báo, ca làm, xin nghỉ | — | — | ✓ | — |
| Dashboard vận hành, doanh thu, cao điểm, mã giảm giá, đánh giá thợ | — | — | — | ✓ |

## Endpoint được kiểm thử tự động

| Nhóm | Endpoint gốc | Vai trò yêu cầu |
|---|---|---|
| Lễ tân | `/reception/counter-appointments`, `/api/reception/pending-appointments`, `/api/reception/email-notifications` | `Receptionist` |
| Lịch ngày chia sẻ | `/reception/daily-schedule` | `Receptionist` hoặc `Owner` |
| Thợ | `/api/stylist/command`, `/api/stylist/notifications`, `/stylist/schedule`, `/api/stylist/leave-requests` | `Stylist` |
| Chủ tiệm | `/api/owner/operations-dashboard`, `/api/owner/revenue-report`, `/api/owner/peak-hours-report`, `/api/owner/promotions`, `/api/owner/reviews` | `Owner` |
| Công khai | `/booking`, `/booking/lookup`, `/booking/stylists`, `/reviews` | `AllowAnonymous` |

`SalonManagement.Tests/StagingRbacMatrixTests.cs` khóa các quy tắc này bằng kiểm thử tự động, bao gồm hồi quy `BUG-01`: API thông báo thợ phải chỉ cho phép role `Stylist`, không được trả lỗi 403 cho thợ đã đăng nhập hợp lệ.
