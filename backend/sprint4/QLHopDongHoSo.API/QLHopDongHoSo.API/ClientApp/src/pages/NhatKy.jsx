import { useState } from 'react'
import Icon from '../components/Icon'
import { Avatar, Badge, Empty, PageHeader, Pagination } from '../components/UI'
import { useData } from '../context/DataContext'
import { useToast } from '../context/ToastContext'
import { searchTerm, useApi, useDebounced } from '../hooks/useApi'
import { api, fetchAll } from '../services/api'
import { AUDIT_ACTIONS } from '../data/constants'
import { date, dateTime, downloadCsv } from '../utils/format'

const PAGE = 12

export default function NhatKy() {
  const { users } = useData()
  const toast = useToast()
  const [q, setQ] = useState('')
  const [action, setAction] = useState('')
  const [who, setWho] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)

  // Lọc + phân trang ở server (index CreatedAt giảm dần) thay vì tải 2.000 dòng rồi lọc ở trình duyệt
  const dq = useDebounced(searchTerm(q))
  const filters = { q: dq, action, userId: who, from, to }
  const { data, loading } = useApi(() => api.auditLogs({ ...filters, page, pageSize: PAGE }), [dq, action, who, from, to, page])
  const rows = data?.items || []
  const reset = () => { setQ(''); setAction(''); setWho(''); setFrom(''); setTo(''); setPage(1) }

  const exportCsv = async () => {
    try {
      const list = await fetchAll(api.auditLogs, filters)
      downloadCsv('nhat-ky-he-thong.csv', list.map((a) => ({
        'Thời gian': dateTime(a.createdAt), 'Người dùng': a.username || '—', 'Hành động': AUDIT_ACTIONS[a.action]?.label || a.action, 'Đối tượng': a.target, 'Chi tiết': a.detail, 'IP': a.ip
      })))
    } catch (e) { toast(e.message, 'error') }
  }

  return (
    <>
      <PageHeader title="Nhật ký hệ thống" subtitle="Audit log: mọi thao tác đăng nhập, tạo, sửa, phê duyệt và phân quyền đều được ghi lại"
        actions={<button className="btn btn-ghost" onClick={exportCsv}><Icon name="download" size={16} />Xuất Excel</button>} />
      <section className="card" style={{ overflow: 'hidden' }}>
        <div className="filters">
          <div className="search"><Icon name="search" size={16} /><input type="search" aria-label="Tìm trong nhật ký" placeholder="Tìm đối tượng, chi tiết, IP…" value={q} maxLength={100} onChange={(e) => { setQ(e.target.value); setPage(1) }} /></div>
          <select className="select" aria-label="Hành động" value={action} onChange={(e) => { setAction(e.target.value); setPage(1) }}>
            <option value="">Tất cả hành động</option>{Object.entries(AUDIT_ACTIONS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
          </select>
          <select className="select" aria-label="Người dùng" value={who} onChange={(e) => { setWho(e.target.value); setPage(1) }}>
            <option value="">Tất cả người dùng</option>{users.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </select>
          <label className="row" style={{ gap: 8, fontSize: 13 }}><span className="muted">Từ</span><input className="input" type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} style={{ width: 'auto' }} /></label>
          <label className="row" style={{ gap: 8, fontSize: 13 }}><span className="muted">Đến</span><input className="input" type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} style={{ width: 'auto' }} /></label>
          <button className="btn btn-ghost" style={{ marginLeft: 'auto' }} onClick={reset}>Xóa bộ lọc</button>
        </div>
        {(loading || data?.searchFrom) && (
          <div className="notice" role="status" style={{ margin: '0 22px 12px' }}>
            <Icon name={loading ? 'refresh' : 'clock'} />
            {loading ? 'Đang tìm trong nhật ký…' : ''}
            {!loading && data?.searchFrom && `Tìm theo từ khóa trong 30 ngày gần nhất (từ ${date(data.searchFrom)}). Chọn "Từ" ngày để tìm xa hơn.`}
          </div>
        )}
        {!data ? <Empty icon="refresh" title="Đang tải…" /> : rows.length === 0 ? <Empty icon="clock" title={loading ? 'Đang tìm…' : 'Không có bản ghi phù hợp'} /> : <>
          <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined} aria-busy={loading}>
            <table className="table">
              <thead><tr><th>Thời gian</th><th>Người dùng</th><th>Hành động</th><th>Đối tượng</th><th>Chi tiết</th><th>Địa chỉ IP</th></tr></thead>
              <tbody>{rows.map((a) => {
                const act = AUDIT_ACTIONS[a.action] || { label: a.action, tone: 'draft' }
                return (
                  <tr key={a.logId}>
                    <td className="num muted">{dateTime(a.createdAt)}</td>
                    <td>{a.fullName ? <span className="user-chip"><Avatar name={a.fullName} size="sm" /><span className="cell-2"><span style={{ fontWeight: 500 }}>{a.fullName}</span><small>{a.username}</small></span></span> : <span className="muted">Không xác định</span>}</td>
                    <td><Badge tone={act.tone}>{act.label}</Badge></td>
                    <td style={{ fontWeight: 500 }}>{a.target}</td>
                    <td style={{ whiteSpace: 'normal', minWidth: 220 }}>{a.detail}</td>
                    <td className="num muted">{a.ip || '—'}</td>
                  </tr>
                )
              })}</tbody>
            </table>
          </div>
          <Pagination page={page} total={data.totalItems} pageSize={PAGE} onChange={setPage} label="bản ghi" />
        </>}
      </section>
    </>
  )
}
