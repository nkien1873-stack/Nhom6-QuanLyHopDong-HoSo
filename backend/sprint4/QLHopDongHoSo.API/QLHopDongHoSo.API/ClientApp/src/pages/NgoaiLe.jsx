import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, Empty, Modal, PageHeader, Pagination, Tabs } from '../components/UI'
import { useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { DEFERRAL_STATUS } from '../data/constants'
import { date, dateTime } from '../utils/format'

const PAGE = 10

// Tờ trình xin nợ chứng từ (tài liệu "Bổ sung sau"): Giám đốc CN / Trưởng phòng QLRR duyệt ngoại lệ.
// Giám đốc có chi nhánh chỉ thấy/duyệt tờ trình của chi nhánh mình; vai trò khác chỉ xem.
export default function NgoaiLe() {
  const { decideDeferral } = useData()
  const { user, can } = useAuth()
  const toast = useToast()
  const [tab, setTab] = useState('PENDING')
  const [page, setPage] = useState(1)
  const [act, setAct] = useState(null) // { d, approve }
  const [note, setNote] = useState('')
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const canDecide = can('deferral.approve')
  const scopeBranch = canDecide && user.branchId != null ? user.branchId : ''
  const { data: list, loading } = useApi(() => api.deferrals({ status: tab === 'ALL' ? '' : tab, branchId: scopeBranch, page, pageSize: PAGE })
    .then((r) => ({ ...r, tab })), [tab, page, scopeBranch])
  const shown = list?.tab === tab ? list : null

  const confirm = async () => {
    if (!act.approve && !note.trim()) return setErr('Vui lòng nhập lý do từ chối tờ trình.')
    setBusy(true)
    try {
      await decideDeferral(act.d.deferralId, act.approve, note.trim() || null)
      toast(act.approve ? `Đã duyệt tờ trình của ${act.d.contractCode}` : `Đã từ chối tờ trình của ${act.d.contractCode}`, act.approve ? undefined : 'error')
      setAct(null); setNote(''); setErr('')
    } catch (e) {
      setErr(e.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader title="Duyệt ngoại lệ" subtitle={canDecide
        ? `Tờ trình xin nợ chứng từ${scopeBranch ? ` của ${user.branch}` : ''} chờ cấp ngoại lệ (Giám đốc chi nhánh / Trưởng phòng QLRR) quyết định`
        : 'Tờ trình xin nợ chứng từ và kết quả duyệt ngoại lệ (chỉ xem)'} />

      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Tờ trình" value={tab} onChange={(k) => { setTab(k); setPage(1) }} items={[
          { key: 'PENDING', label: 'Chờ duyệt', count: tab === 'PENDING' ? shown?.totalItems : undefined },
          { key: 'APPROVED', label: 'Đã duyệt' },
          { key: 'REJECTED', label: 'Từ chối' },
          { key: 'ALL', label: 'Tất cả' }
        ]} />

        {!shown ? <Empty icon="refresh" title="Đang tải…" />
          : shown.items.length === 0 ? <Empty icon="check" title={tab === 'PENDING' ? 'Không có tờ trình chờ duyệt' : 'Chưa có tờ trình'} />
            : (
              <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined}>
                <table className="table">
                  <thead><tr><th>Hồ sơ</th><th>Khách hàng</th><th>Chứng từ nợ</th><th>Hạn hoàn thiện</th><th>Lý do</th><th>Người lập</th><th>Trạng thái</th>{canDecide && tab === 'PENDING' && <th />}</tr></thead>
                  <tbody>{shown.items.map((d) => (
                    <tr key={d.deferralId}>
                      <td><Link to={`/ho-so/${d.contractId}`} style={{ fontWeight: 600 }}>{d.contractCode}</Link><br /><small className="muted">{d.branchName}</small></td>
                      <td>{d.customerName}</td>
                      <td>{d.documentLabel}</td>
                      <td>{date(d.dueDate)}{d.overdue && <><br /><Badge tone="rej">Quá hạn</Badge></>}</td>
                      <td style={{ whiteSpace: 'normal', minWidth: 220 }}>{d.reason}
                        {d.decisionNote && <><br /><small className="muted">Ý kiến: “{d.decisionNote}”</small></>}</td>
                      <td>{d.requestedByName}<br /><small className="muted">{dateTime(d.requestedAt)}</small></td>
                      <td><Badge tone={DEFERRAL_STATUS[d.status].tone}>{DEFERRAL_STATUS[d.status].label}</Badge>
                        {d.decidedByName && <><br /><small className="muted">{d.decidedByName}</small></>}</td>
                      {canDecide && tab === 'PENDING' && (
                        <td><div className="row" style={{ gap: 8, flexWrap: 'nowrap' }}>
                          {/* Hồ sơ đã trình / hủy thì tờ trình treo chỉ còn từ chối được */}
                          <button className="btn btn-danger" onClick={() => { setAct({ d, approve: false }); setNote(''); setErr('') }}>Từ chối</button>
                          {['DRAFT', 'REJECTED'].includes(d.contractStatus) && <button className="btn btn-success" onClick={() => { setAct({ d, approve: true }); setNote(''); setErr('') }}>Duyệt</button>}
                        </div></td>
                      )}
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            )}
        {shown && shown.totalItems > 0 && <Pagination page={page} total={shown.totalItems} pageSize={PAGE} onChange={setPage} label="tờ trình" />}
      </section>

      {act && (
        <Modal title={act.approve ? `Duyệt ngoại lệ ${act.d.contractCode}` : `Từ chối tờ trình ${act.d.contractCode}`} onClose={() => setAct(null)} width={500}
          footer={<><button className="btn btn-ghost" onClick={() => setAct(null)}>Hủy</button>
            <button className={`btn ${act.approve ? 'btn-success' : 'btn-danger'}`} disabled={busy} onClick={confirm}>{act.approve ? 'Xác nhận duyệt' : 'Xác nhận từ chối'}</button></>}>
          <p>{act.approve ? 'Cho phép' : 'Không cho phép'} hồ sơ <b>{act.d.contractCode}</b> ({act.d.customerName}) nợ <b>{act.d.documentLabel}</b> đến <b>{date(act.d.dueDate)}</b>.</p>
          <label className="field">{act.approve ? 'Ý kiến (không bắt buộc)' : 'Lý do từ chối *'}
            <textarea className="textarea" value={note} onChange={(e) => { setNote(e.target.value); setErr('') }} rows={4} maxLength={400} />
            {err && <span className="err">{err}</span>}
          </label>
        </Modal>
      )}
    </>
  )
}
