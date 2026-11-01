import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { ROLES, STATUS, roleFromApi, statusFromApi, statusToApi } from '../data/constants'
import { api, markFresh } from '../services/api'
import { daysUntil } from '../utils/format'
import { useAuth } from './AuthContext'

// =============================================================
// Dữ liệu dùng chung: chỉ giữ các danh mục nhỏ (chi nhánh, loại hợp đồng, vai trò, danh bạ người dùng).
// Hồ sơ, khách hàng, tài liệu, nhật ký do từng màn hình tự lấy theo trang từ API (lọc + phân trang
// phía server) – không tải cả bảng về trình duyệt nữa.
// Sau mỗi thao tác ghi, `version` tăng 1 => các màn hình đang mở tự tải lại đúng phần mình cần.
// =============================================================
const DataContext = createContext(null)

export const EXPIRING = { key: 'EXPIRING', label: 'Sắp hết hạn', tone: 'exp', color: '#EA580C' }

/** Trạng thái hiển thị: hồ sơ Hiệu lực còn ≤ 30 ngày → "Sắp hết hạn", quá hạn → "Hết hạn" */
export const statusOf = (hs) => {
  if (hs.status === 'ACTIVE') {
    const d = daysUntil(hs.endDate)
    if (d != null && d < 0) return STATUS.EXPIRED
    if (d != null && d <= 30) return EXPIRING
  }
  return STATUS[hs.status] || { key: hs.status, label: hs.status, tone: 'draft', color: '#94A3B8' }
}

/** Thông tin hiển thị của một khóa trạng thái (dùng cho số đếm tính sẵn ở server) */
export const statusInfo = (key) => (key === 'EXPIRING' ? EXPIRING : STATUS[key])

export const kindOf = (name = '') => {
  const ext = name.split('.').pop().toLowerCase()
  if (ext === 'pdf') return 'PDF'
  if (['xls', 'xlsx', 'csv'].includes(ext)) return 'XLS'
  if (['doc', 'docx'].includes(ext)) return 'DOC'
  if (['jpg', 'jpeg', 'png', 'gif', 'webp'].includes(ext)) return 'IMG'
  return 'FILE'
}

// Lịch sử trạng thái (backend) → câu mô tả hiển thị ở giao diện
export const historyText = (h) => {
  switch (h.newStatus) {
    case 'DRAFT': return h.oldStatus ? 'Trả về nháp' : 'Tạo hồ sơ (nháp)'
    case 'PENDING_APPROVAL': return 'Gửi phê duyệt'
    case 'ACTIVE': return 'Phê duyệt hồ sơ'
    case 'REJECTED': return 'Từ chối hồ sơ'
    case 'EXPIRED': return 'Hết hạn hợp đồng'
    case 'TERMINATED': return 'Thanh lý hợp đồng'
    case 'CANCELLED': return 'Hủy hồ sơ'
    default: return `Chuyển trạng thái → ${h.newStatus}`
  }
}
// Ghi chú hiển thị cho các bước có ý kiến của người xử lý
export const NOTE_STATUSES = ['ACTIVE', 'REJECTED', 'TERMINATED', 'CANCELLED', 'EXPIRED']

/** Hợp đồng từ API (dòng danh sách hoặc chi tiết) → dạng các màn hình dùng */
export const toHs = (c) => ({
  id: String(c.contractId),
  code: c.contractCode,
  applicationId: c.applicationId,
  applicationCode: c.applicationCode,
  customerId: c.customerId != null ? String(c.customerId) : '',
  customerName: c.customerName || '—',
  customerKind: c.customerKind,
  customerIdNo: c.customerIdNo || '',
  type: c.contractTypeName,
  typeId: c.contractTypeId,
  // null = chưa có giá trị (vd hợp đồng chuyển từ DB cũ) => hiển thị "—"
  amount: c.contractValue != null ? Number(c.contractValue) : null,
  termMonths: c.termMonths,
  rate: c.interestRate,
  purpose: c.purpose,
  collateral: c.collateral,
  status: statusFromApi(c.status),
  ownerId: String(c.createdBy),
  ownerName: c.createdByName || '—',
  branch: c.branchName,
  branchId: c.branchId,
  startDate: c.effectiveDate,
  endDate: c.expiryDate,
  signedDate: c.signedDate,
  createdAt: c.createdAt,
  updatedAt: c.updatedAt || c.createdAt,
  docCount: c.documentCount ?? 0,
  rowVersion: c.rowVersion
})

/** Khách hàng từ API → dạng giao diện */
export const toCustomer = (c) => ({
  id: String(c.customerId),
  code: c.customerCode,
  kind: c.kind,
  name: c.name,
  idNo: c.idNo || '',
  rep: c.representative || '',
  phone: c.phone || '',
  email: c.email || '',
  address: c.address || '',
  branch: c.branchName || '—',
  createdAt: c.createdAt,
  contractCount: c.contractCount ?? 0,
  activeValue: Number(c.activeValue ?? 0),
  rowVersion: c.rowVersion
})

/** Tài liệu từ API → dạng giao diện */
export const toDoc = (d) => ({
  id: String(d.documentId),
  name: d.documentName,
  size: d.fileSize ?? 0,
  kind: kindOf(d.documentName),
  type: d.documentType,
  verification: d.verificationStatus,
  uploadedBy: String(d.uploadedBy),
  uploadedByName: d.uploadedByName,
  uploadedAt: d.uploadedAt,
  contractId: d.linkedContractId != null ? String(d.linkedContractId) : null,
  contractCode: d.contractCode,
  customerName: d.customerName
})

const EMPTY = { directory: [], users: [], branches: [], contractTypes: [], roles: [] }

export function DataProvider({ children }) {
  const { user } = useAuth()
  const [raw, setRaw] = useState(EMPTY)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState('')
  const [version, setVersion] = useState(0)

  const isAdmin = user?.role === 'ADMIN'

  // Danh mục nhỏ, ít đổi: tải một lần sau đăng nhập (và khi bấm "Tải lại dữ liệu")
  const loadLookups = useCallback(async () => {
    if (!user) return
    try {
      const [directory, branches, contractTypes, roles, users] = await Promise.all([
        api.userDirectory(), api.branches(), api.contractTypes(), api.roles(),
        isAdmin ? api.users() : Promise.resolve([])
      ])
      setRaw({ directory, users, branches, contractTypes, roles })
      setError('')
    } catch (e) {
      setError(e.message)
    } finally {
      setLoaded(true)
    }
  }, [user, isAdmin])

  useEffect(() => {
    if (user) {
      setLoaded(false)
      loadLookups()
    } else {
      setRaw(EMPTY)
      setLoaded(false)
    }
  }, [user, loadLookups])

  // Báo các màn hình tải lại dữ liệu của mình
  const bump = useCallback(() => setVersion((v) => v + 1), [])
  const reload = useCallback(async () => { markFresh(); await loadLookups(); bump() }, [loadLookups, bump])

  const data = useMemo(() => {
    // Danh bạ cho mọi người; Admin có thêm email + lần đăng nhập gần nhất
    const details = Object.fromEntries(raw.users.map((u) => [u.userId, u]))
    const users = raw.directory.map((u) => ({
      id: String(u.userId),
      username: u.username,
      name: u.fullName,
      email: details[u.userId]?.email || '',
      phone: details[u.userId]?.phone || '',
      role: roleFromApi(u.roleName),
      branch: u.branchName || '—',
      branchId: u.branchId,
      active: u.status === 'ACTIVE',
      lastLogin: details[u.userId]?.lastLoginAt || null
    }))
    const uMap = Object.fromEntries(users.map((u) => [u.id, u]))

    return {
      users,
      userOf: (id) => uMap[String(id)],
      branches: raw.branches.map((b) => b.branchName),
      branchList: raw.branches,
      contractTypes: raw.contractTypes.map((t) => t.typeName),
      typeList: raw.contractTypes
    }
  }, [raw])

  // ---------- Thao tác (gọi API rồi báo tải lại; lỗi ném ra để màn hình báo toast) ----------
  // files: File hoặc { file, type } (type = DocumentType; mặc định "Tài liệu khác")
  const uploadAll = async (applicationId, files) => {
    const failed = []
    for (const x of files) {
      const f = x.file || x
      try { await api.uploadDocument(applicationId, f, x.type || 'ATTACHMENT') } catch (e) { failed.push(`${f.name}: ${e.message}`) }
    }
    return failed
  }

  // Lưu xong mới gửi duyệt: gửi bị chặn (thiếu tài liệu – ERR_DOC_01/02) thì hồ sơ vẫn đã lưu nháp,
  // trả lỗi để màn hình báo và mở trang chi tiết thay vì để người dùng lưu lại thành hồ sơ trùng
  const trySubmit = async (id) => {
    try {
      const r = await api.changeStatus(id, 'PENDING_APPROVAL', 'Gửi duyệt')
      return { warnings: r?.warnings || [] }
    } catch (e) {
      return { submitError: e.message, warnings: [] }
    }
  }

  const toContractBody = (p) => ({
    contractTypeId: p.typeId,
    contractValue: p.amount,
    termMonths: p.termMonths,
    interestRate: p.rate,
    purpose: p.purpose,
    collateral: p.collateral
  })

  // Chạy thao tác rồi báo tải lại kể cả khi lỗi giữa chừng (vd đã lưu nhưng upload hỏng)
  const write = async (fn) => {
    try { return await fn() } finally { markFresh(); bump() }
  }

  const actions = {
    reload,
    bump,

    createHoSo: (payload, files, submit) => write(async () => {
      const c = await api.createContract({ ...toContractBody(payload), customerId: Number(payload.customerId), branchId: payload.branchId })
      const failed = await uploadAll(c.applicationId, files)
      const sub = submit ? await trySubmit(c.contractId) : { warnings: [] }
      return { id: String(c.contractId), code: c.contractCode, failed, ...sub }
    }),
    // hs: hồ sơ đang sửa (cần applicationId + rowVersion lúc mở form để phát hiện sửa chồng)
    updateHoSo: (hs, payload, files, submit) => write(async () => {
      await api.updateContract(hs.id, { ...toContractBody(payload), rowVersion: hs.rowVersion })
      const failed = await uploadAll(hs.applicationId, files)
      const sub = submit ? await trySubmit(hs.id) : { warnings: [] }
      return { failed, ...sub }
    }),
    submitHoSo: (id) => write(() => api.changeStatus(id, statusToApi('PENDING'), 'Gửi duyệt')),
    approveHoSo: (id, note) => write(() => api.changeStatus(id, 'ACTIVE', note || 'Đồng ý phê duyệt')),
    rejectHoSo: (id, note) => write(() => api.changeStatus(id, 'REJECTED', note)),
    deleteHoSo: (id) => write(() => api.deleteContract(id)),

    addDocs: (applicationId, files, type) => write(() => uploadAll(applicationId, [...files].map((file) => ({ file, type })))),
    requestDeferral: (contractId, body) => write(() => api.createDeferral(contractId, body)),
    decideDeferral: (id, approve, note) => write(() => api.decideDeferral(id, approve, note)),
    removeDoc: (docId) => write(() => api.deleteDocument(docId)),
    verifyDoc: (docId, status) => write(() => api.verifyDocument(docId, status)),
    // preview = true: mở tab mới (PDF, ảnh); false: tải về máy
    openDoc: async (doc, preview = false) => {
      const blob = await api.downloadDocument(doc.id)
      const url = URL.createObjectURL(blob)
      if (preview) {
        window.open(url, '_blank', 'noopener')
      } else {
        const a = document.createElement('a')
        a.href = url
        a.download = doc.name
        a.click()
      }
      setTimeout(() => URL.revokeObjectURL(url), 60000)
    },

    saveCustomer: (c) => write(async () => {
      // rowVersion lúc mở form: người khác vừa sửa khách hàng này thì server báo 409 thay vì ghi đè
      const body = { kind: c.kind, name: c.name, idNo: c.idNo, representative: c.rep, phone: c.phone, email: c.email, address: c.address, rowVersion: c.rowVersion }
      const r = c.id ? await api.updateCustomer(c.id, body) : await api.createCustomer(body)
      return { id: String(r.customerId) }
    }),

    saveUser: async (u) => {
      const roleId = raw.roles.find((r) => r.roleName === ROLES[u.role].api)?.roleId
      const branchId = raw.branches.find((b) => b.branchName === u.branch)?.branchId ?? null
      const body = { fullName: u.name, email: u.email, phone: u.phone || null, roleId, branchId }
      if (u.id) await api.updateUser(u.id, body)
      else await api.createUser({ ...body, username: u.username, password: u.password })
      await loadLookups()
    },
    toggleUser: async (u) => { await api.setUserStatus(u.id, u.active ? 'LOCKED' : 'ACTIVE'); await loadLookups() }
  }

  return (
    <DataContext.Provider value={{ ...data, ...actions, version, loaded, error }}>
      {children}
    </DataContext.Provider>
  )
}

export const useData = () => useContext(DataContext)
