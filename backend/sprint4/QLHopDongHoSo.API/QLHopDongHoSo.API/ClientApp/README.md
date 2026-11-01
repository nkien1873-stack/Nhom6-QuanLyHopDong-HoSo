# Code&Chill – Phần mềm Quản lý Hợp đồng/Hồ sơ (Frontend)

Giao diện web cho đề tài **Quản lý hợp đồng/hồ sơ tích hợp Dashboard dữ liệu cho ngân hàng/tài chính** – Nhóm 6, môn Quản lý dự án CNTT.

React 18 + Vite + React Router. Không dùng thư viện UI, style nằm trong `src/styles/global.css`.
Dữ liệu lấy **trực tiếp từ backend ASP.NET Core** (`backend/sprint3`) qua `/api`, không còn dữ liệu giả.

## Chạy thử

Cần backend chạy trước (cổng 5215), xem `docs/DEMO.md` ở thư mục gốc.

```bash
npm install
npm run dev        # http://localhost:5173 – /api được proxy sang http://localhost:5215
```

| Lệnh | Tác dụng |
|---|---|
| `npm run build` | Build production vào `dist/` (script `scripts/demo/build-demo.ps1` chép vào `wwwroot` của backend) |
| `npm run lint` | Kiểm tra code bằng oxlint |
| `npm run preview` | Chạy thử bản build |

Đổi backend khác: đặt biến môi trường `VITE_API_PROXY` (khi dev) hoặc `VITE_API_URL` (khi build).

## Tài khoản demo (mật khẩu `Demo@123`)

| Tên đăng nhập | Vai trò | Thấy được |
|---|---|---|
| `admin` | Quản trị hệ thống | Tất cả, gồm Người dùng & quyền, Nhật ký hệ thống |
| `manager_hn`, `manager_hcm` | Quản lý phê duyệt | Phê duyệt, Báo cáo (chỉ duyệt hồ sơ của chi nhánh mình) |
| `officer_hn01`, `officer_hcm01`, `officer_dn01` | Chuyên viên hồ sơ | Tạo/sửa hồ sơ, khách hàng, tài liệu (chi nhánh mình) |
| `auditor` | Kiểm soát viên | Chỉ xem, Báo cáo, Nhật ký hệ thống |

## Các màn hình

| Đường dẫn | Màn hình | Chức năng chính |
|---|---|---|
| `/dang-nhap` | Đăng nhập | JWT, báo lỗi sai mật khẩu / tài khoản bị khóa |
| `/` | Tổng quan | 4 chỉ số, hợp đồng ký mới theo tháng, biểu đồ trạng thái, hồ sơ mới, việc cần xử lý |
| `/ho-so` | Hợp đồng & hồ sơ | Tab trạng thái, tìm kiếm không dấu, lọc loại/chi nhánh, sắp xếp, phân trang, xuất CSV |
| `/ho-so/tao-moi` | Tạo hồ sơ | Chọn hoặc thêm khách hàng, validate, kéo thả tài liệu, lưu nháp / gửi duyệt (mã do hệ thống cấp) |
| `/ho-so/:id` | Chi tiết hồ sơ | Tiến trình 4 bước, xem/tải/xác minh tài liệu, phê duyệt/từ chối (bắt buộc lý do), lịch sử |
| `/ho-so/:id/sua` | Sửa hồ sơ | Chỉ hồ sơ Nháp hoặc Bị từ chối, thuộc chi nhánh mình |
| `/khach-hang` | Khách hàng | Doanh nghiệp/cá nhân, chi tiết kèm hồ sơ, thêm/sửa (kiểm tra trùng CCCD/MST ở backend) |
| `/phe-duyet` | Phê duyệt | Hàng chờ duyệt của chi nhánh mình (cảnh báo quá 48 giờ), lịch sử đã xử lý |
| `/tai-lieu` | Tài liệu | Toàn bộ tài liệu, lọc theo loại tệp, xem/tải file thật |
| `/bao-cao` | Báo cáo | Lọc theo kỳ/chi nhánh, tỷ lệ duyệt, thời gian duyệt TB, cơ cấu, hiệu suất chuyên viên |
| `/nguoi-dung` | Người dùng & quyền | Thêm/sửa/khóa tài khoản, ma trận phân quyền |
| `/nhat-ky` | Nhật ký hệ thống | Audit log do backend ghi (đăng nhập, tạo, sửa, duyệt, tải tệp, phân quyền), xuất CSV |

## Cấu trúc thư mục

```
src/
├── main.jsx                 # Điểm khởi động, gắn các Provider, BrowserRouter
├── App.jsx                  # Route + Guard (chặn khi chưa đăng nhập / thiếu quyền) + chờ tải dữ liệu
├── components/              # Layout (sidebar, topbar), UI (Logo, Badge, Modal...), Charts, Icon
├── context/
│   ├── AuthContext.jsx      # Đăng nhập qua API, lưu JWT, can('hoso.approve'), canWriteBranch(id)
│   ├── DataContext.jsx      # Tải dữ liệu từ API, chuyển về dạng màn hình dùng, các thao tác gọi API
│   └── ToastContext.jsx     # Thông báo nhỏ góc màn hình
├── data/constants.js        # Vai trò, ma trận quyền, trạng thái, nhãn nhật ký, giới hạn upload
├── services/api.js          # API client (fetch + JWT, báo lỗi tiếng Việt từ backend)
├── pages/                   # Mỗi màn hình 1 file
├── utils/                   # Định dạng tiền/ngày, tìm kiếm không dấu, CSV, thống kê theo tháng
└── styles/global.css        # Design tokens (màu Code&Chill) + style toàn bộ component
```

Logo nằm ở `public/brand/` (favicon, sidebar, trang đăng nhập).

## Quy ước

- Trạng thái: `DRAFT` (Nháp) → `PENDING` (Chờ duyệt, backend là `PENDING_APPROVAL`) → `ACTIVE` (Hiệu lực) hoặc `REJECTED` (Từ chối, sửa rồi gửi lại); ngoài ra `TERMINATED` (Đã thanh lý), `CANCELLED` (Đã hủy).
  "Sắp hết hạn" không lưu trong DB: hồ sơ `ACTIVE` còn ≤ 30 ngày hiển thị như vậy; quá hạn hiển thị "Hết hạn".
- Ma trận quyền `PERMISSIONS` trong `data/constants.js` chỉ để ẩn/hiện nút; backend kiểm tra lại toàn bộ (vai trò + chi nhánh).

## Màu thương hiệu

| Token | Mã màu | Dùng cho |
|---|---|---|
| `--navy` | `#0B2545` | Sidebar, tiêu đề, nút chính |
| `--teal` | `#14A3A0` | Menu đang chọn, điểm nhấn |
| `--mint` | `#8EF0D6` | Chi tiết logo |
| `--bg` | `#F4F6F9` | Nền trang |
