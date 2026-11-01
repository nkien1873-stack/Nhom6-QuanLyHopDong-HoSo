import { useState } from 'react'
import { Link } from 'react-router-dom'
import Icon from '../components/Icon'
import { DocIcon, Empty, PageHeader, Pagination, Tabs } from '../components/UI'
import { toDoc, useData } from '../context/DataContext'
import { useToast } from '../context/ToastContext'
import { searchTerm, useApi, useDebounced } from '../hooks/useApi'
import { api } from '../services/api'
import { date, fileSize } from '../utils/format'

const PAGE = 10
const KINDS = [{ key: 'ALL', label: 'Tất cả' }, { key: 'PDF', label: 'PDF' }, { key: 'DOC', label: 'Word' }, { key: 'XLS', label: 'Excel' }, { key: 'IMG', label: 'Hình ảnh' }]

export default function TaiLieu() {
  const { openDoc } = useData()
  const toast = useToast()
  const [q, setQ] = useState('')
  const [kind, setKind] = useState('ALL')
  const [view, setView] = useState('list')
  const [page, setPage] = useState(1)

  // Lọc theo loại tệp + tìm kiếm + phân trang ở server; dung lượng lấy từ DB (không đọc ổ đĩa)
  const dq = useDebounced(searchTerm(q))
  const { data, loading } = useApi(() => api.documents({ q: dq, kind, page, pageSize: PAGE }), [dq, kind, page])
  const rows = (data?.items || []).map(toDoc)
  const counts = data?.counts || {}
  const open = (d, preview) => openDoc(d, preview).catch((e) => toast(e.message, 'error'))

  return (
    <>
      <PageHeader title="Tài liệu" subtitle={data ? `${(counts.ALL ?? 0).toLocaleString('vi-VN')} tài liệu · ${fileSize(data.totalSize)}${dq ? ' khớp tìm kiếm' : ' trên toàn hệ thống'}` : 'Đang tải…'} />
      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Loại tệp" value={kind} onChange={(k) => { setKind(k); setPage(1) }} items={KINDS.map((k) => ({ ...k, count: counts[k.key] }))} />
        <div className="filters">
          <div className="search"><Icon name="search" size={16} /><input type="search" aria-label="Tìm tài liệu" placeholder="Tìm tên tệp, mã hồ sơ, khách hàng…" value={q} maxLength={100} onChange={(e) => { setQ(e.target.value); setPage(1) }} /></div>
          <div className="row" style={{ marginLeft: 'auto', gap: 6 }} role="group" aria-label="Kiểu hiển thị">
            <button className={`btn ${view === 'list' ? 'btn-primary' : 'btn-ghost'}`} aria-pressed={view === 'list'} onClick={() => setView('list')}><Icon name="menu" size={16} />Danh sách</button>
            <button className={`btn ${view === 'grid' ? 'btn-primary' : 'btn-ghost'}`} aria-pressed={view === 'grid'} onClick={() => setView('grid')}><Icon name="dashboard" size={16} />Lưới</button>
          </div>
        </div>
        {!data ? <Empty icon="refresh" title="Đang tải…" /> : rows.length === 0 ? <Empty icon="clip" title="Không có tài liệu phù hợp" /> : view === 'list' ? (
          <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined} aria-busy={loading}>
            <table className="table">
              <thead><tr><th>Tên tài liệu</th><th>Hồ sơ</th><th>Khách hàng</th><th className="right">Dung lượng</th><th>Người tải lên</th><th>Ngày tải</th><th className="right">Thao tác</th></tr></thead>
              <tbody>{rows.map((d) => (
                <tr key={d.id}>
                  <td><span className="user-chip"><DocIcon kind={d.kind} /><span style={{ fontWeight: 500 }}>{d.name}</span></span></td>
                  <td>{d.contractId ? <Link to={`/ho-so/${d.contractId}`} style={{ fontWeight: 600 }}>{d.contractCode}</Link> : <span className="muted">—</span>}</td>
                  <td>{d.customerName}</td>
                  <td className="right num">{fileSize(d.size)}</td>
                  <td>{d.uploadedByName}</td>
                  <td>{date(d.uploadedAt)}</td>
                  <td className="right">
                    <button className="icon-btn" aria-label={`Xem trước ${d.name}`} onClick={() => open(d, true)}><Icon name="eye" /></button>
                    <button className="icon-btn" aria-label={`Tải xuống ${d.name}`} onClick={() => open(d)}><Icon name="download" /></button>
                  </td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 12, padding: '0 22px 8px', opacity: loading ? 0.6 : 1 }}>
            {rows.map((d) => (
              <div key={d.id} className="card" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
                <DocIcon kind={d.kind} />
                <span className="doc-name" title={d.name}>{d.name}</span>
                <small className="muted">{fileSize(d.size)} · {date(d.uploadedAt)}</small>
                {d.contractId && <Link to={`/ho-so/${d.contractId}`} style={{ fontSize: 13, fontWeight: 600 }}>{d.contractCode} · {d.customerName}</Link>}
              </div>
            ))}
          </div>
        )}
        {data && data.totalItems > 0 && <Pagination page={page} total={data.totalItems} pageSize={PAGE} onChange={setPage} label="tài liệu" />}
      </section>
    </>
  )
}
