import { Link, useNavigate } from 'react-router-dom'
import Icon from '../components/Icon'
import { Empty, PageHeader, StatCard, StatusBadge } from '../components/UI'
import { BarChart, Donut } from '../components/Charts'
import { statusInfo, statusOf, toHs } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { daysUntil, downloadCsv, fromNow, money, moneyShort } from '../utils/format'

// Thứ tự + màu các phần của biểu đồ tròn
const DONUT_KEYS = ['ACTIVE', 'EXPIRING', 'PENDING', 'DRAFT', 'REJECTED', 'EXPIRED', 'TERMINATED', 'CANCELLED']

export default function Dashboard() {
  const { user, can } = useAuth()
  const toast = useToast()
  const nav = useNavigate()
  // Mọi con số được đếm sẵn ở server (không tải toàn bộ hồ sơ về trình duyệt)
  const { data: o, error, reload } = useApi(() => api.overview(), [])

  if (error && !o) return <div className="card"><Empty icon="alert" title="Không tải được số liệu" text={error} action={<button className="btn btn-ghost" onClick={reload}>Thử lại</button>} /></div>
  if (!o) return <div className="card"><Empty icon="refresh" title="Đang tải số liệu…" /></div>

  const recent = o.recent.map(toHs)
  const donut = DONUT_KEYS.map((k) => ({ label: statusInfo(k).label, value: o.statusCounts[k] || 0, color: statusInfo(k).color })).filter((x) => x.value > 0)

  const tasks = [
    ...(can('hoso.approve') ? o.queue.map(toHs).map((x) => ({ id: x.id, color: '#F59E0B', bg: '#FFF8E6', title: `Duyệt hồ sơ ${x.code}`, sub: `Gửi bởi ${x.ownerName} · ${fromNow(x.updatedAt)}` })) : []),
    ...o.expiringSoon.map(toHs).map((x) => ({ id: x.id, color: '#EA580C', bg: '#FFF1EA', title: `${x.code} hết hạn sau ${daysUntil(x.endDate)} ngày`, sub: `${x.customerName} · liên hệ gia hạn` })),
    ...o.myDrafts.map(toHs).map((x) => ({ id: x.id, color: '#64748B', bg: '#F1F5F9', title: `Hoàn thiện hồ sơ nháp ${x.code}`, sub: `${x.docCount} tài liệu · ${x.customerName}` }))
  ]

  const exportCsv = async () => {
    try {
      const rows = (await api.contractsExport({})).map(toHs)
      downloadCsv('tong-quan-ho-so.csv', rows.map((x) => ({
        'Mã hồ sơ': x.code, 'Khách hàng': x.customerName, 'Loại': x.type, 'Giá trị (VNĐ)': x.amount, 'Trạng thái': statusOf(x).label, 'Chi nhánh': x.branch
      })))
    } catch (e) { toast(e.message, 'error') }
  }

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user.name.split(' ').pop()}`}
        subtitle={`Tình hình hợp đồng & hồ sơ tháng ${new Date().getMonth() + 1}/${new Date().getFullYear()}`}
        actions={<>
          <button className="btn btn-ghost" onClick={exportCsv}><Icon name="download" size={16} />Xuất Excel</button>
          {can('hoso.create') && <Link className="btn btn-primary" to="/ho-so/tao-moi"><Icon name="plus" size={16} stroke={2.4} />Tạo hồ sơ mới</Link>}
        </>}
      />

      <div className="grid-stats">
        <StatCard label="Tổng hợp đồng / hồ sơ" value={o.total.toLocaleString('vi-VN')} icon="file" tone="info" note={`Của ${o.customers.toLocaleString('vi-VN')} khách hàng`} />
        <StatCard label="Hồ sơ chờ duyệt" value={o.pending} icon="clock" tone="wait" note={o.overduePending ? `${o.overduePending} hồ sơ chờ quá 48 giờ` : 'Không có hồ sơ quá hạn duyệt'} noteTone={o.overduePending ? 'wait' : undefined} />
        <StatCard label="Sắp hết hạn (30 ngày)" value={o.expiring} icon="alert" tone="exp" note="Cần liên hệ gia hạn" />
        <StatCard label="Giá trị đang hiệu lực" value={moneyShort(o.activeValue).replace(' tỷ', '')} unit={o.activeValue >= 1e9 ? 'tỷ ₫' : '₫'} icon="trend" tone="ok" note={`${o.activeCount} hợp đồng hiệu lực`} />
      </div>

      <div className="grid-2">
        <section className="card card-pad span-2 stack">
          <h2>Hợp đồng ký mới theo tháng</h2>
          <BarChart data={o.monthly} series={[{ key: 'credit', label: 'Tín dụng', color: '#0B2545' }, { key: 'deposit', label: 'Tiền gửi & dịch vụ', color: '#14A3A0' }]} />
        </section>
        <section className="card card-pad stack">
          <h2>Hồ sơ theo trạng thái</h2>
          <Donut items={donut} />
        </section>
      </div>

      <div className="grid-2">
        <section className="card span-2" style={{ overflow: 'hidden' }}>
          <div className="card-head"><h2>Hồ sơ mới cập nhật</h2><Link to="/ho-so" className="right" style={{ fontWeight: 600 }}>Xem tất cả →</Link></div>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Mã hồ sơ</th><th>Khách hàng</th><th className="right">Giá trị</th><th>Trạng thái</th><th>Cập nhật</th></tr></thead>
              <tbody>
                {recent.map((x) => (
                  <tr key={x.id} onClick={() => nav(`/ho-so/${x.id}`)} style={{ cursor: 'pointer' }}>
                    <td><Link to={`/ho-so/${x.id}`} style={{ fontWeight: 600 }}>{x.code}</Link></td>
                    <td><span className="cell-2"><span>{x.customerName}</span><small>{x.type}</small></span></td>
                    <td className="right num">{money(x.amount)}</td>
                    <td><StatusBadge hs={x} /></td>
                    <td className="muted">{fromNow(x.updatedAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="card card-pad stack" style={{ gap: 12 }}>
          <h2>Cần xử lý hôm nay</h2>
          {tasks.length === 0 && <p className="muted">Không có việc cần xử lý. </p>}
          {tasks.map((t) => (
            <Link key={t.title} to={`/ho-so/${t.id}`} className="task" style={{ background: t.bg, color: 'var(--text)' }}>
              <i className="dot" style={{ background: t.color, marginTop: 6 }} />
              <span className="cell-2"><span className="title">{t.title}</span><small>{t.sub}</small></span>
            </Link>
          ))}
        </section>
      </div>
    </>
  )
}
