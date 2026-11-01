import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import Icon from '../components/Icon'
import { Empty, Modal, PageHeader, Pagination, StatusBadge, Tabs } from '../components/UI'
import { statusOf, toHs, useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { searchTerm, useApi, useDebounced } from '../hooks/useApi'
import { api } from '../services/api'
import { date, downloadCsv, moneyNum } from '../utils/format'

const PAGE = 8
const TABS = [
  { key: 'ALL', label: 'Tất cả' },
  { key: 'PENDING', label: 'Chờ duyệt' },
  { key: 'ACTIVE', label: 'Hiệu lực' },
  { key: 'EXPIRING', label: 'Sắp hết hạn' },
  { key: 'DRAFT', label: 'Nháp' },
  { key: 'REJECTED', label: 'Từ chối' },
  { key: 'EXPIRED', label: 'Hết hạn' },
  { key: 'TERMINATED', label: 'Đã thanh lý' },
  { key: 'CANCELLED', label: 'Đã hủy' }
]

export default function HoSoList() {
  const { deleteHoSo, branchList, typeList } = useData()
  const { can, canWriteBranch } = useAuth()
  const toast = useToast()
  const nav = useNavigate()
  const [params, setParams] = useSearchParams()
  const q = params.get('q') || ''
  const [tab, setTab] = useState(params.get('tt') || 'ALL')
  const [typeId, setTypeId] = useState('')
  const [branchId, setBranchId] = useState('')
  const [mine, setMine] = useState(false)
  const [sort, setSort] = useState('updated')
  const [page, setPage] = useState(1)
  const [toDelete, setToDelete] = useState(null)
  const [busy, setBusy] = useState(false)

  // Lọc, đếm theo tab và phân trang đều làm ở server; chỉ 1 trang được tải về
  const dq = useDebounced(searchTerm(q))
  const filters = { q: dq, typeId, branchId, mine, sort }
  const { data, loading, error } = useApi(() => api.contracts({ ...filters, tab, page, pageSize: PAGE }), [dq, typeId, branchId, mine, sort, tab, page])
  const rows = (data?.items || []).map(toHs)
  const counts = data?.counts || {}
  const total = data?.totalItems ?? 0

  const reset = () => { setTypeId(''); setBranchId(''); setMine(false); setParams({}); setPage(1) }
  const exportCsv = async () => {
    try {
      const list = (await api.contractsExport({ ...filters, tab })).map(toHs)
      downloadCsv('danh-sach-ho-so.csv', list.map((x) => ({
        'Mã hồ sơ': x.code, 'Khách hàng': x.customerName, 'CCCD/MST': x.customerIdNo, 'Loại': x.type,
        'Giá trị (VNĐ)': x.amount, 'Ngày hiệu lực': date(x.startDate), 'Ngày hết hạn': date(x.endDate), 'Phụ trách': x.ownerName, 'Trạng thái': statusOf(x).label
      })))
    } catch (e) { toast(e.message, 'error') }
  }

  return (
    <>
      <PageHeader
        title="Hợp đồng & hồ sơ"
        subtitle="Quản lý toàn bộ hồ sơ khách hàng của các chi nhánh"
        actions={<>
          <button className="btn btn-ghost" onClick={exportCsv}><Icon name="download" size={16} />Xuất Excel</button>
          {can('hoso.create') && <Link className="btn btn-primary" to="/ho-so/tao-moi"><Icon name="plus" size={16} stroke={2.4} />Tạo hồ sơ mới</Link>}
        </>}
      />

      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Lọc theo trạng thái" value={tab} onChange={(k) => { setTab(k); setPage(1) }} items={TABS.map((t) => ({ ...t, count: counts[t.key] }))} />

        <div className="filters">
          <div className="search">
            <Icon name="search" size={16} />
            <input type="search" aria-label="Tìm kiếm hồ sơ" placeholder="Tìm theo mã, khách hàng, CCCD/MST…" value={q} maxLength={100}
              onChange={(e) => { setParams(e.target.value ? { q: e.target.value } : {}); setPage(1) }} />
          </div>
          <select className="select" aria-label="Loại hợp đồng" value={typeId} onChange={(e) => { setTypeId(e.target.value); setPage(1) }}>
            <option value="">Tất cả loại</option>{typeList.map((t) => <option key={t.contractTypeId} value={t.contractTypeId}>{t.typeName}</option>)}
          </select>
          <select className="select" aria-label="Chi nhánh" value={branchId} onChange={(e) => { setBranchId(e.target.value); setPage(1) }}>
            <option value="">Tất cả chi nhánh</option>{branchList.map((b) => <option key={b.branchId} value={b.branchId}>{b.branchName}</option>)}
          </select>
          <select className="select" aria-label="Sắp xếp" value={sort} onChange={(e) => { setSort(e.target.value); setPage(1) }}>
            <option value="updated">Mới cập nhật</option><option value="created">Mới tạo</option><option value="amount">Giá trị cao nhất</option>
          </select>
          <label className="check"><input type="checkbox" checked={mine} onChange={(e) => { setMine(e.target.checked); setPage(1) }} />Hồ sơ của tôi</label>
          <button className="btn btn-ghost" style={{ marginLeft: 'auto' }} onClick={reset}>Xóa bộ lọc</button>
        </div>

        {error && !data ? <Empty icon="alert" title="Không tải được danh sách" text={error} />
          : !data ? <Empty icon="refresh" title="Đang tải…" />
          : rows.length === 0 ? <Empty title="Không tìm thấy hồ sơ phù hợp" text="Thử đổi từ khóa hoặc bỏ bớt bộ lọc." action={<button className="btn btn-ghost" onClick={reset}>Xóa bộ lọc</button>} /> : (
          <>
            <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined} aria-busy={loading}>
              <table className="table">
                <thead><tr><th>Mã hồ sơ</th><th>Khách hàng</th><th>Loại hợp đồng</th><th className="right">Giá trị (₫)</th><th>Thời gian hiệu lực</th><th>Phụ trách</th><th>Trạng thái</th><th className="right">Thao tác</th></tr></thead>
                <tbody>
                  {rows.map((x) => {
                    const editable = can('hoso.create') && canWriteBranch(x.branchId) && ['DRAFT', 'REJECTED'].includes(x.status)
                    return (
                      <tr key={x.id}>
                        <td><Link to={`/ho-so/${x.id}`} style={{ fontWeight: 600 }}>{x.code}</Link></td>
                        <td><span className="cell-2"><span style={{ fontWeight: 500 }}>{x.customerName}</span><small>{x.customerKind === 'DN' ? 'MST' : 'CCCD'} {x.customerKind === 'DN' ? x.customerIdNo : `••• ${x.customerIdNo.slice(-4)}`}</small></span></td>
                        <td>{x.type}</td>
                        <td className="right num">{moneyNum(x.amount)}</td>
                        <td>{x.startDate ? <span className="cell-2"><span>{date(x.startDate)}</span><small>đến {date(x.endDate)}</small></span> : <span className="muted">—</span>}</td>
                        <td>{x.ownerName.split(' ').slice(-2).join(' ')}</td>
                        <td><StatusBadge hs={x} /></td>
                        <td className="right">
                          <button className="icon-btn" onClick={() => nav(`/ho-so/${x.id}`)} aria-label={`Xem ${x.code}`}><Icon name="eye" /></button>
                          {editable && <button className="icon-btn" onClick={() => nav(`/ho-so/${x.id}/sua`)} aria-label={`Sửa ${x.code}`}><Icon name="edit" /></button>}
                          {editable && x.status === 'DRAFT' && <button className="icon-btn" onClick={() => setToDelete(x)} aria-label={`Xóa ${x.code}`}><Icon name="trash" /></button>}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
            <Pagination page={page} total={total} pageSize={PAGE} onChange={setPage} label="hồ sơ" />
          </>
        )}
      </section>

      {toDelete && (
        <Modal title="Xóa hồ sơ nháp?" onClose={() => setToDelete(null)} width={440}
          footer={<>
            <button className="btn btn-ghost" onClick={() => setToDelete(null)}>Hủy</button>
            <button className="btn btn-danger" disabled={busy} onClick={async () => {
              setBusy(true)
              try { await deleteHoSo(toDelete.id); toast(`Đã xóa ${toDelete.code}`); setToDelete(null) } catch (e) { toast(e.message, 'error') } finally { setBusy(false) }
            }}>Xóa hồ sơ</button>
          </>}>
          <p>Hồ sơ <b>{toDelete.code}</b> và các tài liệu đính kèm sẽ bị xóa. Thao tác này được ghi vào nhật ký hệ thống.</p>
        </Modal>
      )}
    </>
  )
}
