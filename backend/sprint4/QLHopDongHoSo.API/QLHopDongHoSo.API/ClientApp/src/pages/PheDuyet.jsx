import { useState } from 'react'
import { Link } from 'react-router-dom'
import Icon from '../components/Icon'
import { Avatar, Empty, Modal, PageHeader, Pagination, StatCard, StatusBadge, Tabs } from '../components/UI'
import { toHs, useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { dateTime, fromNow, money } from '../utils/format'

const PAGE = 10

export default function PheDuyet() {
  const { approveHoSo, rejectHoSo } = useData()
  const { user } = useAuth()
  const toast = useToast()
  const [tab, setTab] = useState('PENDING')
  const [page, setPage] = useState(1)
  const [act, setAct] = useState(null) // { hs, type }
  const [note, setNote] = useState('')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  // Hàng chờ của người duyệt: hồ sơ chờ duyệt thuộc chi nhánh mình (Admin: tất cả), gửi sớm nhất trước
  const scopeBranch = user.role === 'ADMIN' ? '' : user.branchId
  const { data: stats } = useApi(() => api.approvalStats(), [])
  const { data: list, loading } = useApi(() => (tab === 'PENDING'
    // notMine: hồ sơ do chính mình tạo không nằm trong hàng đợi (không được tự duyệt – ERR_SOD_01)
    ? api.contracts({ tab: 'PENDING', branchId: scopeBranch, notMine: true, sort: 'oldest', page, pageSize: PAGE })
    : api.decisions({ page, pageSize: PAGE })).then((r) => ({ ...r, tab })), [tab, page, scopeBranch])
  // Vừa đổi tab: dữ liệu cũ thuộc tab kia => chờ tải xong mới hiển thị
  const shown = list?.tab === tab ? list : null
  const waitHours = (x) => (Date.now() - new Date(x.updatedAt)) / 3600000

  const confirm = async () => {
    if (act.type === 'reject' && !note.trim()) return setErr('Vui lòng nhập lý do từ chối.')
    setBusy(true)
    try {
      if (act.type === 'approve') {
        const r = await approveHoSo(act.hs.id, note.trim())
        toast(`Đã phê duyệt ${act.hs.code}`)
        ;(r?.warnings || []).forEach((w) => toast(w, 'warn'))
      }
      else { await rejectHoSo(act.hs.id, note.trim()); toast(`Đã từ chối ${act.hs.code}`, 'error') }
      setAct(null); setNote(''); setErr('')
    } catch (e) {
      // 409: người khác vừa xử lý hồ sơ này => danh sách đã tự tải lại
      setErr(e.message)
    } finally {
      setBusy(false)
    }
  }

  const pending = shown && tab === 'PENDING' ? shown.items.map(toHs) : []
  const decided = shown && tab === 'DONE' ? shown.items : []

  return (
    <>
      <PageHeader title="Phê duyệt" subtitle="Hồ sơ chờ quyết định của bạn (phê duyệt 1 cấp, Maker ≠ Checker), xử lý theo thứ tự gửi sớm nhất" />

      <div className="grid-stats">
        <StatCard label="Đang chờ duyệt" value={stats?.pending ?? '…'} icon="clock" tone="wait" />
        <StatCard label="Chờ quá 48 giờ" value={stats?.late ?? '…'} icon="alert" tone="exp" note="Ưu tiên xử lý" />
        <StatCard label="Đã duyệt hôm nay" value={stats?.approvedToday ?? '…'} icon="check" tone="ok" />
        <StatCard label="Tổng giá trị chờ duyệt" value={stats ? money(stats.pendingValue).replace(' ₫', '') : '…'} unit="₫" icon="trend" tone="info" />
      </div>

      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Phê duyệt" value={tab} onChange={(k) => { setTab(k); setPage(1) }} items={[
          { key: 'PENDING', label: 'Chờ duyệt', count: stats?.pending },
          { key: 'DONE', label: 'Đã xử lý', count: stats?.decided }
        ]} />

        {!shown ? <Empty icon="refresh" title="Đang tải…" />
          : tab === 'PENDING' ? (pending.length === 0
            ? <Empty icon="check" title="Không còn hồ sơ chờ duyệt" text="Bạn đã xử lý hết. Hồ sơ mới gửi sẽ xuất hiện tại đây." />
            : <div className="stack" style={{ padding: 16, gap: 12, opacity: loading ? 0.6 : 1 }}>
              {pending.map((x) => {
                const late = waitHours(x) > 48
                return (
                  <article key={x.id} className="card" style={{ padding: 18, display: 'flex', flexWrap: 'wrap', gap: 16, alignItems: 'center' }}>
                    <div className="cell-2" style={{ flex: '1 1 260px', gap: 4 }}>
                      <div className="row" style={{ gap: 10 }}><Link to={`/ho-so/${x.id}`} style={{ fontWeight: 700, fontSize: 16 }}>{x.code}</Link><StatusBadge hs={x} />{late && <span className="badge tone-exp">Quá 48 giờ</span>}</div>
                      <span style={{ fontWeight: 500 }}>{x.customerName}</span>
                      <small>{x.type} · {x.termMonths ? `${x.termMonths} tháng · ` : ''}{x.docCount} tài liệu · {x.branch}</small>
                    </div>
                    <div className="cell-2" style={{ flex: '0 1 180px' }}><small>Số tiền</small><b className="num" style={{ fontSize: 17, color: 'var(--navy)' }}>{money(x.amount)}</b></div>
                    <div className="user-chip" style={{ flex: '0 1 200px' }}><Avatar name={x.ownerName} size="sm" /><span className="cell-2"><span style={{ fontWeight: 500 }}>{x.ownerName}</span><small>gửi {fromNow(x.updatedAt)}</small></span></div>
                    <div className="row" style={{ marginLeft: 'auto', gap: 8 }}>
                      <Link className="btn btn-ghost" to={`/ho-so/${x.id}`}><Icon name="eye" size={16} />Xem</Link>
                      {x.ownerId === user.id
                        ? <span className="badge tone-rej" title="ERR_SOD_01: Người tạo hồ sơ không có quyền phê duyệt chính hồ sơ này.">Hồ sơ do bạn tạo – cần Checker khác</span>
                        : <>
                          <button className="btn btn-danger" onClick={() => { setAct({ hs: x, type: 'reject' }); setNote(''); setErr('') }}>Từ chối</button>
                          <button className="btn btn-success" onClick={() => { setAct({ hs: x, type: 'approve' }); setNote(''); setErr('') }}><Icon name="check" size={16} stroke={2.4} />Duyệt</button>
                        </>}
                    </div>
                  </article>
                )
              })}
            </div>)
          : decided.length === 0 ? <Empty title="Chưa có hồ sơ đã xử lý" /> : (
            <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined}>
              <table className="table">
                <thead><tr><th>Mã hồ sơ</th><th>Khách hàng</th><th className="right">Giá trị</th><th>Kết quả</th><th>Ý kiến</th><th>Người duyệt</th><th>Thời gian</th></tr></thead>
                <tbody>{decided.map((h) => (
                  <tr key={h.historyId}>
                    <td><Link to={`/ho-so/${h.contractId}`} style={{ fontWeight: 600 }}>{h.contractCode}</Link></td>
                    <td>{h.customerName}</td>
                    <td className="right num">{money(h.contractValue != null ? Number(h.contractValue) : null)}</td>
                    <td>{h.newStatus === 'ACTIVE' ? <span className="badge tone-ok">Đã duyệt</span> : <span className="badge tone-rej">Từ chối</span>}</td>
                    <td style={{ whiteSpace: 'normal', minWidth: 200 }}>{h.reason || <span className="muted">—</span>}</td>
                    <td>{h.changedByName}</td>
                    <td className="muted">{dateTime(h.changedAt)}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          )}
        {shown && shown.totalItems > 0 && <Pagination page={page} total={shown.totalItems} pageSize={PAGE} onChange={setPage} label={tab === 'PENDING' ? 'hồ sơ' : 'quyết định'} />}
      </section>

      {act && (
        <Modal title={act.type === 'approve' ? `Phê duyệt ${act.hs.code}` : `Từ chối ${act.hs.code}`} onClose={() => setAct(null)} width={500}
          footer={<><button className="btn btn-ghost" onClick={() => setAct(null)}>Hủy</button>
            <button className={`btn ${act.type === 'approve' ? 'btn-success' : 'btn-danger'}`} disabled={busy} onClick={confirm}>{act.type === 'approve' ? 'Xác nhận phê duyệt' : 'Xác nhận từ chối'}</button></>}>
          <p>{act.hs.customerName} · {act.hs.type} · <b>{money(act.hs.amount)}</b></p>
          <label className="field">{act.type === 'approve' ? 'Ý kiến (không bắt buộc)' : 'Lý do từ chối *'}
            <textarea className="textarea" value={note} onChange={(e) => { setNote(e.target.value); setErr('') }} rows={4} />
            {err && <span className="err">{err}</span>}
          </label>
        </Modal>
      )}
    </>
  )
}
