// =============================================================
// Danh mục cố định của giao diện. Dữ liệu thật (hồ sơ, khách hàng, người dùng,
// chi nhánh, loại hợp đồng, nhật ký) lấy từ API – xem context/DataContext.jsx.
// =============================================================

// Vai trò hiển thị ở FE ↔ RoleName ở backend (AppRoles)
export const ROLES = {
  ADMIN: { key: 'ADMIN', api: 'Admin', name: 'Quản trị hệ thống', tone: 'info' },
  MANAGER: { key: 'MANAGER', api: 'Manager', name: 'Quản lý phê duyệt', tone: 'ok' },
  STAFF: { key: 'STAFF', api: 'Officer', name: 'Chuyên viên hồ sơ', tone: 'draft' },
  AUDITOR: { key: 'AUDITOR', api: 'Auditor', name: 'Kiểm soát viên', tone: 'wait' },
  DIRECTOR: { key: 'DIRECTOR', api: 'Director', name: 'Giám đốc CN / QLRR', tone: 'rej' }
}

export const roleFromApi = (roleName) =>
  Object.values(ROLES).find((r) => r.api === roleName)?.key || 'AUDITOR'

// Quyền theo vai trò (RBAC) – chỉ để ẩn/hiện menu và nút; backend kiểm tra lại toàn bộ.
// Thao tác ghi còn bị giới hạn theo chi nhánh của người dùng (Admin không giới hạn).
export const PERMISSIONS = [
  { key: 'dashboard.view', name: 'Xem tổng quan', roles: ['ADMIN', 'MANAGER', 'STAFF', 'AUDITOR', 'DIRECTOR'] },
  { key: 'hoso.view', name: 'Xem hợp đồng/hồ sơ', roles: ['ADMIN', 'MANAGER', 'STAFF', 'AUDITOR', 'DIRECTOR'] },
  { key: 'hoso.create', name: 'Tạo & sửa hồ sơ, lập tờ trình nợ chứng từ', roles: ['ADMIN', 'STAFF'] },
  { key: 'hoso.approve', name: 'Phê duyệt / từ chối hồ sơ (không duyệt hồ sơ do mình tạo)', roles: ['ADMIN', 'MANAGER'] },
  { key: 'deferral.view', name: 'Xem tờ trình xin nợ chứng từ', roles: ['ADMIN', 'MANAGER', 'STAFF', 'AUDITOR', 'DIRECTOR'] },
  { key: 'deferral.approve', name: 'Duyệt ngoại lệ (tờ trình xin nợ chứng từ)', roles: ['DIRECTOR'] },
  { key: 'customer.manage', name: 'Quản lý khách hàng', roles: ['ADMIN', 'STAFF'] },
  { key: 'document.manage', name: 'Tải lên / xóa tài liệu', roles: ['ADMIN', 'STAFF'] },
  { key: 'document.verify', name: 'Xác minh tài liệu', roles: ['ADMIN', 'MANAGER'] },
  { key: 'report.view', name: 'Xem & xuất báo cáo', roles: ['ADMIN', 'MANAGER', 'AUDITOR'] },
  { key: 'user.manage', name: 'Quản lý người dùng & phân quyền', roles: ['ADMIN'] },
  { key: 'audit.view', name: 'Xem nhật ký hệ thống', roles: ['ADMIN', 'AUDITOR'] }
]

// Trạng thái hồ sơ (khớp ContractStatuses ở backend; PENDING = PENDING_APPROVAL)
export const STATUS = {
  DRAFT: { key: 'DRAFT', label: 'Nháp', tone: 'draft', color: '#94A3B8' },
  PENDING: { key: 'PENDING', label: 'Chờ duyệt', tone: 'wait', color: '#F59E0B' },
  ACTIVE: { key: 'ACTIVE', label: 'Hiệu lực', tone: 'ok', color: '#14A3A0' },
  REJECTED: { key: 'REJECTED', label: 'Từ chối', tone: 'rej', color: '#DC2626' },
  EXPIRED: { key: 'EXPIRED', label: 'Hết hạn', tone: 'draft', color: '#64748B' },
  TERMINATED: { key: 'TERMINATED', label: 'Đã thanh lý', tone: 'info', color: '#5B8DB8' },
  CANCELLED: { key: 'CANCELLED', label: 'Đã hủy', tone: 'draft', color: '#CBD5E1' }
}

export const statusFromApi = (s) => (s === 'PENDING_APPROVAL' ? 'PENDING' : s)
export const statusToApi = (s) => (s === 'PENDING' ? 'PENDING_APPROVAL' : s)

export const AUDIT_ACTIONS = {
  LOGIN: { label: 'Đăng nhập', tone: 'info' },
  LOGOUT: { label: 'Đăng xuất', tone: 'draft' },
  LOGIN_FAIL: { label: 'Đăng nhập lỗi', tone: 'rej' },
  CREATE: { label: 'Tạo mới', tone: 'ok' },
  UPDATE: { label: 'Cập nhật', tone: 'info' },
  SUBMIT: { label: 'Gửi duyệt', tone: 'wait' },
  APPROVE: { label: 'Phê duyệt', tone: 'ok' },
  REJECT: { label: 'Từ chối', tone: 'rej' },
  UPLOAD: { label: 'Tải tài liệu', tone: 'info' },
  DELETE: { label: 'Xóa', tone: 'rej' },
  ROLE: { label: 'Phân quyền', tone: 'wait' }
}

// Loại tài liệu (DocumentType ở backend)
export const DOC_TYPES = {
  CCCD: 'CCCD / Hộ chiếu',
  APPLICATION_FORM: 'Đơn đề nghị',
  INCOME_PROOF: 'Chứng minh thu nhập',
  BUSINESS_LICENSE: 'Giấy phép kinh doanh',
  COLLATERAL: 'Hồ sơ tài sản bảo đảm',
  CONTRACT_SIGNED: 'Hợp đồng đã ký',
  ATTACHMENT: 'Tài liệu khác'
}

// N09 – danh mục tài liệu theo hồ sơ (khớp DocumentRequirements ở backend; backend kiểm tra lại khi trình/duyệt)
// Bắt buộc: Đơn đề nghị + CCCD (cá nhân) / GPKD (doanh nghiệp) – thiếu thì không được trình (ERR_DOC_01).
// Bổ sung sau: Chứng minh thu nhập (cá nhân, HĐ tín dụng/vay), Hồ sơ TSBĐ (có TSBĐ, trừ tín chấp/ký quỹ) –
// thiếu thì cần Tờ trình xin nợ chứng từ được Giám đốc CN / Trưởng phòng QLRR duyệt (WARN_DOC_02).
export const REQUIREMENT = {
  MANDATORY: { label: 'Bắt buộc', tone: 'rej' },
  DEFERRED: { label: 'Bổ sung sau', tone: 'wait' }
}

export function docRequirements(customerKind, typeName, collateral) {
  const corporate = customerKind === 'DN'
  const list = [
    { type: 'APPLICATION_FORM', requirement: 'MANDATORY' },
    { type: corporate ? 'BUSINESS_LICENSE' : 'CCCD', requirement: 'MANDATORY' }
  ]
  const name = (typeName || '').toLowerCase()
  if (!corporate && (name.includes('tín dụng') || name.includes('vay'))) list.push({ type: 'INCOME_PROOF', requirement: 'DEFERRED' })
  const col = (collateral || '').trim().toLowerCase()
  if (col && !col.startsWith('tín chấp') && !col.startsWith('ký quỹ') && !col.startsWith('không')) list.push({ type: 'COLLATERAL', requirement: 'DEFERRED' })
  return list
}

export const DEFERRAL_STATUS = {
  PENDING: { label: 'Chờ duyệt', tone: 'wait' },
  APPROVED: { label: 'Đã duyệt', tone: 'ok' },
  REJECTED: { label: 'Từ chối', tone: 'rej' },
  CANCELLED: { label: 'Hết hiệu lực', tone: 'draft' }
}

// Thời hạn nợ chứng từ tối đa (khớp DocumentComplianceService.MaxDeferralDays)
export const MAX_DEFERRAL_DAYS = 90

// Giới hạn upload – khớp FileUpload trong appsettings.json của backend
export const UPLOAD_MAX_MB = 10
export const UPLOAD_EXTS = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.jpg', '.jpeg', '.png']

// Mật khẩu mặc định khi Admin tạo tài khoản mới (backend yêu cầu ≥ 8 ký tự)
export const DEFAULT_PASSWORD = 'Demo@123'
