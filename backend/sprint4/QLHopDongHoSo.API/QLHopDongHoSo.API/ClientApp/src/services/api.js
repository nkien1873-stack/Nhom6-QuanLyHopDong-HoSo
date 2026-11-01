// =============================================================
// API CLIENT – nối backend ASP.NET Core Web API (backend/sprint3).
// Mặc định gọi /api (cùng cổng khi chạy chung backend, hoặc qua proxy của Vite).
// =============================================================

const BASE = import.meta.env.VITE_API_URL || '/api'
const TOKEN_KEY = 'qlhd.token'

export const tokenStore = {
  get: () => { try { return sessionStorage.getItem(TOKEN_KEY) } catch { return null } },
  set: (t) => { try { sessionStorage.setItem(TOKEN_KEY, t) } catch { /* bỏ qua */ } },
  clear: () => { try { sessionStorage.removeItem(TOKEN_KEY) } catch { /* bỏ qua */ } }
}

// Gọi khi token hết hạn / không hợp lệ (AuthContext gắn hàm đăng xuất vào đây)
let onUnauthorized = () => {}
export const setUnauthorizedHandler = (fn) => { onUnauthorized = fn }

export class ApiError extends Error {
  constructor(message, status) {
    super(message)
    this.status = status
  }
}

// Vừa ghi dữ liệu xong => vài giây tới yêu cầu server trả số liệu tổng hợp mới nhất (không dùng bản cache ≤ 1 giây)
let freshUntil = 0
export const markFresh = (ms = 3000) => { freshUntil = Date.now() + ms }

async function request(path, { method = 'GET', body, form, raw } = {}) {
  const token = tokenStore.get()
  const headers = token ? { Authorization: `Bearer ${token}` } : {}
  if (Date.now() < freshUntil) headers['X-Fresh'] = '1'
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  const init = { method, headers }
  if (form) init.body = form
  else if (body !== undefined) init.body = JSON.stringify(body)

  let res
  try {
    res = await fetch(`${BASE}${path}`, init)
  } catch {
    throw new ApiError('Không kết nối được máy chủ. Kiểm tra backend đã chạy chưa.', 0)
  }

  if (res.status === 401 && !path.startsWith('/Auth/login')) {
    onUnauthorized()
    throw new ApiError('Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.', 401)
  }

  if (!res.ok) {
    let message = `Lỗi ${res.status}`
    try {
      const data = await res.json()
      message = data.message || (data.errors ? Object.values(data.errors).flat().join(' ') : message)
    } catch { /* không phải JSON */ }
    if (res.status === 403 && message === `Lỗi 403`) message = 'Bạn không có quyền thực hiện thao tác này.'
    throw new ApiError(message, res.status)
  }

  if (raw) return res
  return res.status === 204 ? null : res.json()
}

// { q: 'abc', page: 2, mine: false, x: '' } => "?q=abc&page=2" (bỏ giá trị rỗng/false/null)
const qs = (params = {}) => {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v === undefined || v === null || v === '' || v === false) continue
    p.append(k, String(v))
  }
  const s = p.toString()
  return s ? `?${s}` : ''
}

// Gom nhiều trang (100 dòng/trang) để xuất Excel, tối đa `max` dòng
export async function fetchAll(fn, params = {}, max = 5000) {
  const items = []
  for (let page = 1; items.length < max; page++) {
    const r = await fn({ ...params, page, pageSize: 100 })
    items.push(...r.items)
    if (page >= r.totalPages || r.items.length === 0) break
  }
  return items.slice(0, max)
}

export const api = {
  // Xác thực
  login: (username, password) => request('/Auth/login', { method: 'POST', body: { username, password } }),
  logout: () => request('/Auth/logout', { method: 'POST' }),
  demoAccounts: () => request('/Auth/demo-accounts'),

  // Danh mục
  branches: () => request('/Lookup/branches'),
  contractTypes: () => request('/Lookup/contract-types'),
  roles: () => request('/Lookup/roles'),
  userDirectory: () => request('/Lookup/users'),

  // Tổng hợp tính sẵn ở server
  overview: () => request('/Dashboard/overview'),
  approvalStats: () => request('/Dashboard/approvals'),
  notifications: () => request('/Dashboard/notifications'),
  report: (params) => request(`/Dashboard/report${qs(params)}`),

  // Hợp đồng / hồ sơ – lọc + phân trang phía server: { items, totalItems, counts }
  contracts: (params) => request(`/Contract${qs(params)}`),
  contractsExport: (params) => request(`/Contract/export${qs(params)}`),
  contract: (id) => request(`/Contract/${id}`),
  contractHistory: (id) => request(`/Contract/${id}/history`),
  decisions: (params) => request(`/Contract/decisions${qs(params)}`),
  createContract: (data) => request('/Contract', { method: 'POST', body: data }),
  updateContract: (id, data) => request(`/Contract/${id}`, { method: 'PUT', body: data }),
  changeStatus: (id, status, reason) => request(`/Contract/${id}/status`, { method: 'PATCH', body: { status, reason } }),
  deleteContract: (id) => request(`/Contract/${id}`, { method: 'DELETE' }),
  // N09: danh mục tài liệu bắt buộc / bổ sung sau + cảnh báo nợ chứng từ
  checklist: (id) => request(`/Contract/${id}/checklist`),

  // Tờ trình xin nợ chứng từ (Maker lập, Giám đốc CN / Trưởng phòng QLRR duyệt)
  deferrals: (params) => request(`/DocumentDeferral${qs(params)}`),
  createDeferral: (contractId, data) => request(`/DocumentDeferral/contract/${contractId}`, { method: 'POST', body: data }),
  decideDeferral: (id, approve, note) => request(`/DocumentDeferral/${id}/decision`, { method: 'PUT', body: { approve, note } }),

  // Tài liệu
  documents: (params) => request(`/Document${qs(params)}`),
  uploadDocument: (applicationId, file, documentType = 'ATTACHMENT') => {
    const form = new FormData()
    form.append('ApplicationId', applicationId)
    form.append('DocumentType', documentType)
    form.append('File', file)
    return request('/Document/upload', { method: 'POST', form })
  },
  deleteDocument: (id) => request(`/Document/${id}`, { method: 'DELETE' }),
  verifyDocument: (id, status) => request(`/Document/${id}/verify`, { method: 'PATCH', body: { status } }),
  downloadDocument: async (id) => (await request(`/Document/${id}/download`, { raw: true })).blob(),

  // Khách hàng
  customers: (params) => request(`/Customer${qs(params)}`),
  customer: (id) => request(`/Customer/${id}`),
  createCustomer: (data) => request('/Customer', { method: 'POST', body: data }),
  updateCustomer: (id, data) => request(`/Customer/${id}`, { method: 'PUT', body: data }),

  // Người dùng (Admin)
  users: () => request('/User'),
  createUser: (data) => request('/User', { method: 'POST', body: data }),
  updateUser: (id, data) => request(`/User/${id}`, { method: 'PUT', body: data }),
  setUserStatus: (id, status) => request(`/User/${id}/status`, { method: 'PATCH', body: { status } }),

  // Nhật ký hệ thống (Admin, Auditor)
  auditLogs: (params) => request(`/AuditLog${qs(params)}`)
}
