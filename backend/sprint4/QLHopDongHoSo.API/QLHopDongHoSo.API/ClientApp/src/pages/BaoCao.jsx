import { useState } from 'react'
import Icon from '../components/Icon'
import { Empty, PageHeader, StatCard } from '../components/UI'
import { BarChart, Donut, HBars } from '../components/Charts'
import { statusInfo, useData } from '../context/DataContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { downloadCsv, money, moneyShort } from '../utils/format'

const PERIODS = { 30: '30 ngày qua', 90: 'Quý này', 365: '12 tháng', 0: 'Toàn bộ' }
const STATUS_KEYS = ['ACTIVE', 'EXPIRING', 'PENDING', 'DRAFT', 'REJECTED', 'EXPIRED', 'TERMINATED', 'CANCELLED']
const BRANCH_COLORS = ['#0B2545', '#14A3A0', '#5B8DB8', '#F59E0B', '#94A3B8']

export default function BaoCao() {
  const { branchList } = useData()
  const [branchId, setBranchId] = useState('')
  const [period, setPeriod] = useState('0')

  // Toàn bộ số liệu (tỷ lệ duyệt, thời gian duyệt TB, theo loại/chi nhánh/chuyên viên) tính bằng GROUP BY ở server
  const { data: r, error, reload } = useApi(() => api.report({ branchId, days: Number(period) }), [branchId, period])

  const exportCsv = () => r && downloadCsv('bao-cao-theo-chuyen-vien.csv', r.staff.map((s) => ({
    'Chuyên viên': s.name, 'Tổng hồ sơ': s.total, 'Hiệu lực': s.active, 'Chờ duyệt': s.pending, 'Từ chối': s.rejected, 'Giá trị hiệu lực (VNĐ)': s.value
  })))

  const avgH = r?.avgApprovalHours ?? 0
  const statusItems = r ? STATUS_KEYS.map((k) => ({ label: statusInfo(k).label, value: r.statusCounts[k] || 0, color: statusInfo(k).color })).filter((x) => x.value > 0) : []

  return (
    <>
      <PageHeader title="Báo cáo" subtitle="Thống kê hợp đồng, hồ sơ và hiệu suất xử lý"
        actions={<>
          <select className="select" aria-label="Kỳ báo cáo" value={period} onChange={(e) => setPeriod(e.target.value)} style={{ width: 'auto' }}>
            {Object.entries(PERIODS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
          <select className="select" aria-label="Chi nhánh" value={branchId} onChange={(e) => setBranchId(e.target.value)} style={{ width: 'auto' }}>
            <option value="">Tất cả chi nhánh</option>{branchList.map((b) => <option key={b.branchId} value={b.branchId}>{b.branchName}</option>)}
          </select>
          <button className="btn btn-ghost" onClick={() => window.print()}><Icon name="print" size={16} />In</button>
          <button className="btn btn-primary" onClick={exportCsv} disabled={!r}><Icon name="download" size={16} />Xuất Excel</button>
        </>} />

      {!r ? (error
        ? <div className="card"><Empty icon="alert" title="Không tải được báo cáo" text={error} action={<button className="btn btn-ghost" onClick={reload}>Thử lại</button>} /></div>
        : <div className="card"><Empty icon="refresh" title="Đang tính báo cáo…" /></div>) : <>
        <div className="grid-stats">
          <StatCard label="Số hồ sơ trong kỳ" value={r.count.toLocaleString('vi-VN')} icon="file" tone="info" />
          <StatCard label="Tổng giá trị đề nghị" value={moneyShort(r.totalValue)} unit="₫" icon="trend" tone="ok" />
          <StatCard label="Tỷ lệ phê duyệt" value={`${r.approvalRate}%`} icon="check" tone="ok" note={`${r.approved}/${r.decided} hồ sơ đã quyết định`} />
          <StatCard label="Thời gian duyệt TB" value={(avgH >= 24 ? avgH / 24 : avgH).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} unit={avgH >= 24 ? 'ngày' : 'giờ'} icon="clock" tone="wait" note="Từ lúc gửi đến khi có quyết định" />
        </div>

        <div className="grid-2">
          <section className="card card-pad span-2 stack"><h2>Hợp đồng ký mới theo tháng</h2>
            <BarChart data={r.monthly} series={[{ key: 'credit', label: 'Tín dụng', color: '#0B2545' }, { key: 'deposit', label: 'Tiền gửi & dịch vụ', color: '#14A3A0' }]} />
          </section>
          <section className="card card-pad stack"><h2>Cơ cấu trạng thái</h2><Donut items={statusItems} /></section>
        </div>

        <div className="grid-2">
          <section className="card card-pad stack"><h2>Giá trị theo loại hợp đồng</h2>
            <HBars items={r.byType.map((t) => ({ label: t.name, value: Number(t.value), color: '#14A3A0' }))} format={moneyShort} />
          </section>
          <section className="card card-pad stack"><h2>Số hồ sơ theo chi nhánh</h2>
            <HBars items={r.byBranch.map((b, i) => ({ label: b.name, value: Number(b.value), color: BRANCH_COLORS[i % BRANCH_COLORS.length] }))} />
          </section>
        </div>

        <section className="card" style={{ overflow: 'hidden' }}>
          <div className="card-head"><h2>Hiệu suất theo chuyên viên</h2></div>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Chuyên viên</th><th className="right">Tổng hồ sơ</th><th className="right">Hiệu lực</th><th className="right">Chờ duyệt</th><th className="right">Từ chối</th><th className="right">Giá trị hiệu lực</th></tr></thead>
              <tbody>{r.staff.map((s) => (
                <tr key={s.userId}><td style={{ fontWeight: 500 }}>{s.name}</td><td className="right num">{s.total}</td><td className="right num">{s.active}</td><td className="right num">{s.pending}</td><td className="right num">{s.rejected}</td><td className="right num">{money(Number(s.value))}</td></tr>
              ))}</tbody>
            </table>
          </div>
        </section>
      </>}
    </>
  )
}
