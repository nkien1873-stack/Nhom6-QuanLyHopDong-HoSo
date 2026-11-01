import { useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import Icon from '../components/Icon'
import { Avatar, Badge, DocIcon, Empty, Modal, PageHeader, StatusBadge } from '../components/UI'
import { NOTE_STATUSES, historyText, statusOf, toDoc, toHs, useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { date, dateTime, daysUntil, fileSize, money } from '../utils/format'
import { DEFERRAL_STATUS, DOC_TYPES, MAX_DEFERRAL_DAYS, REQUIREMENT, UPLOAD_EXTS, UPLOAD_MAX_MB } from '../data/constants'

// yyyy-mm-dd của hôm nay + n ngày (giới hạn ô chọn thời hạn nợ chứng từ)
const isoDay = (n) => {
  const d = new Date()
  d.setDate(d.getDate() + n)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const VERIFY = { VERIFIED: ['ok', 'Đã xác minh'], PENDING: ['wait', 'Chờ xác minh'], REJECTED: ['rej', 'Không hợp lệ'] }

const STEP_COLORS = { done: '#14A3A0', wait: '#F59E0B', rej: '#DC2626', todo: 'var(--border)' }

// Chi tiết 1 hồ sơ: hợp đồng + lịch sử + tài liệu của chính hồ sơ đó (3 request nhỏ)
async function loadDetail(id) {
  const c = await api.contract(id)
  const [history, docs, checklist] = await Promise.all([
    api.contractHistory(id),
    api.documents({ applicationId: c.applicationId, pageSize: 100 }),
    api.checklist(id)
  ])
  const hs = toHs(c)
  hs.checklist = checklist
  // Maker = người tạo + người gửi duyệt gần nhất (không được tự phê duyệt – ERR_SOD_01)
  hs.lastSubmitter = String([...history].find((h) => h.newStatus === 'PENDING_APPROVAL')?.changedBy ?? '')
  hs.customer = {
    id: hs.customerId, kind: c.customerKind, name: c.customerName, idNo: c.customerIdNo, rep: c.customerRepresentative,
    phone: c.customerPhone, email: c.customerEmail, address: c.customerAddress
  }
  hs.docs = docs.items.map(toDoc).sort((a, b) => a.uploadedAt.localeCompare(b.uploadedAt))
  hs.history = [
    ...history.map((h) => ({ action: historyText(h), by: String(h.changedBy), at: h.changedAt, note: NOTE_STATUSES.includes(h.newStatus) ? h.reason || '' : '' })),
    ...hs.docs.map((d) => ({ action: `Tải lên ${d.name}`, by: d.uploadedBy, at: d.uploadedAt, note: '' }))
  ].sort((a, b) => a.at.localeCompare(b.at))
  return hs
}

export default function HoSoDetail() {
  const { id } = useParams()
  const data = useData()
  const { userOf } = data
  const { user, can, canWriteBranch } = useAuth()
  const toast = useToast()
  const fileRef = useRef(null)
  const [note, setNote] = useState('')
  const [noteErr, setNoteErr] = useState('')
  const [confirm, setConfirm] = useState(null) // 'approve' | 'reject'
  const [delDoc, setDelDoc] = useState(null)
  const [busy, setBusy] = useState(false)
  const [upType, setUpType] = useState('')
  const [defer, setDefer] = useState(null) // { item, dueDate, reason, error } – lập tờ trình xin nợ
  const [decide, setDecide] = useState(null) // { d, approve, note, error } – Giám đốc CN duyệt tờ trình

  const { data: hs, error } = useApi(() => loadDetail(id), [id])
  if (!hs && error) return <div className="card"><Empty icon="file" title="Không tìm thấy hồ sơ" text={error.includes('404') || error.includes('Không tìm thấy') ? 'Hồ sơ có thể đã bị xóa.' : error} action={<Link className="btn btn-ghost" to="/ho-so">Về danh sách</Link>} /></div>
  if (!hs || hs.id !== id) return <div className="card"><Empty icon="refresh" title="Đang tải hồ sơ…" /></div>

  const c = hs.customer
  const s = statusOf(hs)
  // Thao tác ghi chỉ trong chi nhánh của mình (Admin không giới hạn) – backend kiểm tra lại
  const ownBranch = canWriteBranch(hs.branchId)
  const editable = can('hoso.create') && ownBranch && ['DRAFT', 'REJECTED'].includes(hs.status)
  // Tải bổ sung được cả khi đang chờ duyệt; xóa chỉ khi Nháp / Bị từ chối (tài liệu đang được Checker xem xét)
  const docEditable = can('document.manage') && ownBranch && ['DRAFT', 'REJECTED', 'PENDING'].includes(hs.status)
  const docDeletable = docEditable && hs.status !== 'PENDING'
  const isMaker = user && (hs.ownerId === user.id || hs.lastSubmitter === user.id)
  // Maker ≠ Checker: người tạo hồ sơ / người tải lên không tự xác minh tài liệu
  const canVerify = can('document.verify') && ownBranch && hs.ownerId !== user.id
  // N09: danh mục tài liệu + tờ trình nợ chứng từ
  const cl = hs.checklist
  const blockers = [
    ...(cl.missingMandatory.length ? [`ERR_DOC_01: Hồ sơ thiếu tài liệu bắt buộc ${cl.missingMandatory.join(', ')}. Vui lòng upload trước khi trình duyệt.`] : []),
    ...(cl.missingDeferred.length ? [`ERR_DOC_02: Thiếu ${cl.missingDeferred.join(', ')} – cần upload hoặc có Tờ trình xin nợ chứng từ được Giám đốc CN / Trưởng phòng QLRR duyệt.`] : []),
    ...(cl.overdueDeferred.length ? [`ERR_DOC_02: Tờ trình xin nợ ${cl.overdueDeferred.join(', ')} đã quá hạn – upload tài liệu hoặc lập tờ trình mới.`] : [])
  ]
  const canDecideDeferral = can('deferral.approve') && ['DRAFT', 'REJECTED'].includes(hs.status) && (user.branchId == null || user.branchId === Number(hs.branchId))
  const missingTypes = cl.items.filter((i) => !i.present).map((i) => i.documentType)
  const uploadType = upType || missingTypes[0] || 'ATTACHMENT'

  // Chạy thao tác API: khóa nút khi đang xử lý, báo lỗi bằng toast; trả về kết quả để đọc cảnh báo
  const run = async (fn, okMsg, tone) => {
    setBusy(true)
    try {
      const r = await fn()
      if (okMsg) toast(okMsg, tone)
      ;(r?.warnings || []).forEach((w) => toast(w, 'warn'))
    } catch (e) { toast(e.message, 'error') } finally { setBusy(false) }
  }
  const lastBy = (a) => [...hs.history].reverse().find((h) => h.action.startsWith(a))

  const sendDeferral = () => {
    if (!defer.dueDate) return setDefer({ ...defer, error: 'Chọn thời hạn hoàn thiện chứng từ.' })
    if (defer.reason.trim().length < 10) return setDefer({ ...defer, error: 'Lý do xin nợ chứng từ tối thiểu 10 ký tự.' })
    run(async () => { await data.requestDeferral(hs.id, { documentType: defer.item.documentType, dueDate: defer.dueDate, reason: defer.reason.trim() }); setDefer(null) },
      `Đã gửi tờ trình xin nợ ${defer.item.label}`)
  }
  const sendDecision = () => {
    if (!decide.approve && !decide.note.trim()) return setDecide({ ...decide, error: 'Vui lòng nhập lý do từ chối tờ trình.' })
    run(async () => { await data.decideDeferral(decide.d.deferralId, decide.approve, decide.note.trim() || null); setDecide(null) },
      decide.approve ? 'Đã duyệt tờ trình xin nợ chứng từ' : 'Đã từ chối tờ trình', decide.approve ? undefined : 'error')
  }

  const step3 = hs.status === 'PENDING' ? 'wait' : hs.status === 'REJECTED' ? 'rej' : ['ACTIVE', 'EXPIRED'].includes(hs.status) ? 'done' : 'todo'
  const docStep = cl.canSubmit
    ? { s: cl.warnings.length ? `Đủ (nợ ${cl.warnings.length} chứng từ)` : `Đủ ${cl.items.length}/${cl.items.length} loại yêu cầu`, st: cl.warnings.length ? 'wait' : 'done' }
    : { s: `Thiếu ${cl.missingMandatory.length + cl.missingDeferred.length} loại tài liệu`, st: ['DRAFT', 'REJECTED'].includes(hs.status) ? 'rej' : 'todo' }
  const steps = [
    { t: '1. Tạo hồ sơ', s: dateTime(hs.createdAt), st: 'done' },
    { t: '2. Đủ tài liệu', ...docStep },
    { t: '3. Phê duyệt', s: step3 === 'wait' ? 'Đang chờ quản lý duyệt' : step3 === 'rej' ? 'Bị từ chối' : step3 === 'done' ? dateTime(lastBy('Phê duyệt')?.at) : '—', st: step3 },
    { t: '4. Ký & hiệu lực', s: hs.startDate ? `${date(hs.startDate)} → ${date(hs.endDate)}` : '—', st: ['ACTIVE', 'EXPIRED'].includes(hs.status) ? 'done' : 'todo' }
  ]

  const doApprove = () => run(async () => { const r = await data.approveHoSo(hs.id, note.trim()); setConfirm(null); setNote(''); return r }, `Đã phê duyệt ${hs.code}`)
  const doReject = () => run(async () => { await data.rejectHoSo(hs.id, note.trim()); setConfirm(null); setNote('') }, `Đã từ chối ${hs.code}`, 'error')
  const askReject = () => {
    if (!note.trim()) return setNoteErr('Vui lòng nhập lý do từ chối.')
    setConfirm('reject')
  }
  const upload = (files) => {
    if (!files.length) return
    // Chặn trước khi gửi: tệp quá lớn bị máy chủ ngắt kết nối, trình duyệt chỉ thấy "lỗi mạng"
    const tooBig = [...files].filter((f) => f.size > UPLOAD_MAX_MB * 1048576)
    if (tooBig.length) toast(`Bỏ qua (quá ${UPLOAD_MAX_MB} MB): ${tooBig.map((f) => f.name).join(', ')}`, 'error')
    const list = [...files].filter((f) => f.size <= UPLOAD_MAX_MB * 1048576)
    if (!list.length) return
    run(async () => {
      const failed = await data.addDocs(hs.applicationId, list, uploadType)
      setUpType('')
      const ok = list.length - failed.length
      if (ok) toast(`Đã tải lên ${ok} tài liệu`)
      if (failed.length) toast(`Không tải được: ${failed.join('; ')}`, 'error')
    })
  }

  return (
    <>
      <PageHeader
        crumbs={<nav className="breadcrumb" aria-label="Breadcrumb"><Link to="/ho-so">Hợp đồng &amp; hồ sơ</Link><span aria-hidden="true">/</span><span className="current">{hs.code}</span></nav>}
        title={<span className="row" style={{ gap: 12 }}>{hs.code}<StatusBadge hs={hs} lg /></span>}
        subtitle={`${hs.type} · ${c?.name} · tạo ${date(hs.createdAt)} bởi ${hs.ownerName}`}
        actions={<>
          <button className="btn btn-ghost" onClick={() => window.print()}><Icon name="print" size={16} />In hồ sơ</button>
          {editable && <Link className="btn btn-ghost" to={`/ho-so/${hs.id}/sua`}><Icon name="edit" size={16} />Chỉnh sửa</Link>}
          {editable && <button className="btn btn-primary" disabled={!cl.canSubmit || busy} title={blockers.join('\n') || undefined}
            onClick={() => run(() => data.submitHoSo(hs.id), `Đã gửi ${hs.code} đi phê duyệt`)}><Icon name="send" size={16} />Gửi phê duyệt</button>}
        </>}
      />

      {editable && blockers.map((b) => <div key={b} className="notice error" role="alert"><Icon name="alert" />{b}</div>)}
      {cl.warnings.map((w) => <div key={w} className="notice warn" role="status"><Icon name="alert" />{w}</div>)}

      {s.key === 'EXPIRING' && <div className="notice" style={{ background: 'var(--exp-bg)', color: 'var(--exp)' }}><Icon name="alert" />Hợp đồng sẽ hết hạn sau {daysUntil(hs.endDate)} ngày ({date(hs.endDate)}). Liên hệ khách hàng để gia hạn.</div>}
      {hs.status === 'REJECTED' && <div className="notice error"><Icon name="alert" />Lý do từ chối: {lastBy('Từ chối')?.note || '—'}. Chỉnh sửa và gửi lại để phê duyệt.</div>}

      <section className="card card-pad" aria-label="Tiến trình hồ sơ">
        <ol className="stepper">
          {steps.map((st) => (
            <li key={st.t}><div className="bar" style={{ background: STEP_COLORS[st.st] }} /><span className="t">{st.t}</span><span className="s">{st.s}</span></li>
          ))}
        </ol>
      </section>

      <div className="split">
        <div className="main-col">
          <section className="card card-pad stack">
            <div className="row"><h2>Thông tin khách hàng</h2><Link to={`/khach-hang?id=${c?.id}`} style={{ marginLeft: 'auto', fontWeight: 600 }}>Xem khách hàng →</Link></div>
            <div className="info-grid">
              <Info k={c?.kind === 'DN' ? 'Tên doanh nghiệp' : 'Họ tên'} v={c?.name} />
              <Info k={c?.kind === 'DN' ? 'Mã số thuế' : 'Số CCCD'} v={c?.idNo} />
              {c?.kind === 'DN' && <Info k="Người đại diện" v={c?.rep} />}
              <Info k="Điện thoại" v={c?.phone} />
              <Info k="Email" v={c?.email} />
              <Info k="Địa chỉ" v={c?.address} />
            </div>
          </section>

          <section className="card card-pad stack">
            <h2>Thông tin hợp đồng</h2>
            <div className="info-grid">
              <Info k="Số tiền" v={<span style={{ fontSize: 18, fontWeight: 700, color: 'var(--navy)' }}>{money(hs.amount)}</span>} />
              <Info k="Thời hạn" v={hs.termMonths ? `${hs.termMonths} tháng` : '—'} />
              <Info k="Lãi suất" v={hs.rate != null ? `${hs.rate} %/năm` : 'Theo biểu lãi suất hiện hành'} />
              <Info k="Mục đích" v={hs.purpose} />
              <Info k="Tài sản bảo đảm" v={hs.collateral} />
              <Info k="Chi nhánh" v={hs.branch} />
              <Info k="Mã hồ sơ gốc" v={hs.applicationCode} />
              <Info k="Ngày hiệu lực" v={date(hs.startDate)} />
              <Info k="Ngày hết hạn" v={date(hs.endDate)} />
              <Info k="Chuyên viên phụ trách" v={hs.ownerName} />
            </div>
          </section>

          <section className="card card-pad stack" style={{ gap: 14 }} aria-label="Danh mục tài liệu">
            <div className="row"><h2>Danh mục tài liệu</h2><span className="muted">{cl.items.filter((i) => i.present).length}/{cl.items.length} đã có</span></div>
            <div className="checklist">
              {cl.items.map((i) => {
                const d = i.deferral
                const st = i.present ? ['ok', `Đã có (${i.uploaded})`]
                  : d?.status === 'APPROVED' ? [d.overdue ? 'rej' : 'wait', `Nợ chứng từ – hạn ${date(d.dueDate)}${d.overdue ? ' (quá hạn)' : ''}`]
                    : d?.status === 'PENDING' ? ['wait', 'Tờ trình chờ duyệt']
                      : ['rej', 'Thiếu']
                const canRequest = editable && !i.present && i.requirement === 'DEFERRED' && (!['PENDING', 'APPROVED'].includes(d?.status) || d.overdue)
                return (
                  <div className="check-row" key={i.documentType}>
                    <span className="grow">
                      <b style={{ fontWeight: 600 }}>{i.label}</b>
                      {d && !i.present && <small>Tờ trình: “{d.reason}” · {d.requestedByName} · hạn {date(d.dueDate)}
                        {d.decidedByName ? ` · ${DEFERRAL_STATUS[d.status].label.toLowerCase()} bởi ${d.decidedByName}${d.decisionNote ? `: “${d.decisionNote}”` : ''}` : ''}</small>}
                    </span>
                    <Badge tone={REQUIREMENT[i.requirement].tone}>{REQUIREMENT[i.requirement].label}</Badge>
                    <Badge tone={st[0]}>{st[1]}</Badge>
                    {canRequest && <button className="btn btn-ghost" style={{ minHeight: 34 }} disabled={busy}
                      onClick={() => setDefer({ item: i, dueDate: '', reason: '', error: '' })}>Lập tờ trình xin nợ</button>}
                    {canDecideDeferral && !i.present && d?.status === 'PENDING' && <>
                      <button className="btn btn-danger" style={{ minHeight: 34 }} disabled={busy} onClick={() => setDecide({ d, approve: false, note: '', error: '' })}>Từ chối</button>
                      <button className="btn btn-success" style={{ minHeight: 34 }} disabled={busy} onClick={() => setDecide({ d, approve: true, note: '', error: '' })}>Duyệt ngoại lệ</button>
                    </>}
                  </div>
                )
              })}
            </div>
          </section>

          <section className="card card-pad stack" style={{ gap: 14 }}>
            <div className="row">
              <h2>Tài liệu đính kèm</h2><span className="muted">{hs.docs.length} tệp</span>
              {docEditable && <>
                <input ref={fileRef} type="file" multiple hidden accept={UPLOAD_EXTS.join(',')} onChange={(e) => { upload(e.target.files); e.target.value = '' }} />
                <select className="select" style={{ marginLeft: 'auto', width: 'auto', minHeight: 36 }} aria-label="Loại tài liệu tải lên" value={uploadType} onChange={(e) => setUpType(e.target.value)}>
                  {Object.entries(DOC_TYPES).map(([k, v]) => <option key={k} value={k}>{v}{missingTypes.includes(k) ? ' (còn thiếu)' : ''}</option>)}
                </select>
                <button className="btn btn-ghost" style={{ minHeight: 36 }} disabled={busy} onClick={() => fileRef.current.click()}><Icon name="upload" size={16} />Tải lên</button>
              </>}
            </div>
            {hs.docs.length === 0 ? <Empty icon="clip" title="Chưa có tài liệu" text="Tải lên giấy tờ cần thiết trước khi gửi phê duyệt." /> : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 10 }}>
                {hs.docs.map((d) => (
                  <div className="doc" key={d.id}>
                    <DocIcon kind={d.kind} />
                    <span className="cell-2" style={{ minWidth: 0, flex: 1 }}>
                      <span className="doc-name">{d.name}</span>
                      <small>{DOC_TYPES[d.type] || d.type} · {fileSize(d.size)} · {date(d.uploadedAt)}</small>
                      <span><Badge tone={(VERIFY[d.verification] || VERIFY.PENDING)[0]}>{(VERIFY[d.verification] || VERIFY.PENDING)[1]}</Badge></span>
                    </span>
                    {canVerify && d.uploadedBy !== user.id && d.verification === 'PENDING' && <>
                      <button className="icon-btn" aria-label={`Xác minh ${d.name}`} title="Xác minh hợp lệ" disabled={busy} onClick={() => run(() => data.verifyDoc(d.id, 'VERIFIED'), 'Đã xác minh tài liệu')}><Icon name="check" /></button>
                      <button className="icon-btn" aria-label={`Đánh dấu ${d.name} không hợp lệ`} title="Không hợp lệ" disabled={busy} onClick={() => run(() => data.verifyDoc(d.id, 'REJECTED'), 'Đã đánh dấu không hợp lệ', 'error')}><Icon name="x" /></button>
                    </>}
                    <button className="icon-btn" aria-label={`Xem ${d.name}`} title="Xem" onClick={() => run(() => data.openDoc(d, true))}><Icon name="eye" /></button>
                    <button className="icon-btn" aria-label={`Tải xuống ${d.name}`} title="Tải xuống" onClick={() => run(() => data.openDoc(d))}><Icon name="download" /></button>
                    {docDeletable && <button className="icon-btn" aria-label={`Xóa ${d.name}`} onClick={() => setDelDoc(d)}><Icon name="trash" /></button>}
                  </div>
                ))}
              </div>
            )}
          </section>
        </div>

        <div className="side-col">
          {hs.status === 'PENDING' && can('hoso.approve') && ownBranch && isMaker && (
            <section className="card card-pad stack" style={{ gap: 8 }}>
              <h2>Phê duyệt</h2>
              <div className="notice error" role="alert"><Icon name="alert" />ERR_SOD_01: Người tạo hồ sơ không có quyền phê duyệt chính hồ sơ này.</div>
              <p className="muted">Không thể override (kể cả Admin). Hồ sơ phải do Checker khác của {hs.branch} (hoặc Admin khác) phê duyệt theo ma trận phân quyền.</p>
            </section>
          )}
          {hs.status === 'PENDING' && can('hoso.approve') && ownBranch && !isMaker && (
            <section className="card card-pad stack" style={{ gap: 14 }}>
              <h2>Phê duyệt</h2>
              {!cl.canApprove && <div className="notice error" role="alert"><Icon name="alert" />{blockers.join(' ')} Không cấp nào được phê duyệt bỏ qua.</div>}
              <p className="muted" style={{ color: 'var(--text-2)' }}>Hồ sơ đã gửi và chờ bạn phê duyệt (phê duyệt 1 cấp).</p>
              <label className="field">Ý kiến phê duyệt
                <textarea className="textarea" rows={4} value={note} onChange={(e) => { setNote(e.target.value); setNoteErr('') }} placeholder="Nhập ý kiến (bắt buộc khi từ chối)…" />
                {noteErr && <span className="err">{noteErr}</span>}
              </label>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0,1fr))', gap: 10 }}>
                <button className="btn btn-danger" onClick={askReject}>Từ chối</button>
                <button className="btn btn-success" disabled={!cl.canApprove} onClick={() => setConfirm('approve')}><Icon name="check" size={16} stroke={2.4} />Phê duyệt</button>
              </div>
            </section>
          )}
          {hs.status === 'PENDING' && !(can('hoso.approve') && ownBranch) && (
            <section className="card card-pad stack" style={{ gap: 8 }}>
              <h2>Đang chờ phê duyệt</h2>
              <p className="muted">{can('hoso.approve')
                ? `Hồ sơ thuộc ${hs.branch}, chỉ quản lý của chi nhánh này (hoặc Admin) được phê duyệt.`
                : 'Hồ sơ đã được gửi tới quản lý phê duyệt. Bạn sẽ nhận thông báo khi có kết quả.'}</p>
            </section>
          )}

          <section className="card card-pad stack" style={{ gap: 14 }}>
            <h2>Lịch sử xử lý</h2>
            <ol className="timeline">
              {[...hs.history].reverse().map((h, i) => {
                const u = userOf(h.by)
                const color = h.action.includes('Từ chối') ? '#DC2626' : h.action.includes('Phê duyệt') || h.action.includes('hiệu lực') ? '#14A3A0' : h.action.includes('Gửi') ? '#F59E0B' : '#94A3B8'
                return (
                  <li key={i}>
                    <i className="dot" style={{ background: color }} />
                    <span className="cell-2">
                      <span style={{ fontWeight: 500 }}>{h.action}</span>
                      {h.note && <span style={{ fontSize: 13, color: 'var(--text-2)' }}>“{h.note}”</span>}
                      <small>{u ? u.name : 'Hệ thống'} · {dateTime(h.at)}</small>
                    </span>
                  </li>
                )
              })}
            </ol>
          </section>

          <section className="card card-pad stack" style={{ gap: 12 }}>
            <h2>Người liên quan</h2>
            {[{ u: userOf(hs.ownerId), r: 'Chuyên viên phụ trách' }, ...(lastBy('Phê duyệt') || lastBy('Từ chối') ? [{ u: userOf((lastBy('Phê duyệt') || lastBy('Từ chối')).by), r: 'Người phê duyệt' }] : [])].map(({ u, r }) => u && (
              <div className="user-chip" key={r}><Avatar name={u.name} size="sm" /><span className="cell-2"><b style={{ fontWeight: 600 }}>{u.name}</b><small>{r}</small></span></div>
            ))}
          </section>
        </div>
      </div>

      {confirm && (
        <Modal title={confirm === 'approve' ? 'Xác nhận phê duyệt' : 'Xác nhận từ chối'} onClose={() => setConfirm(null)} width={460}
          footer={<>
            <button className="btn btn-ghost" onClick={() => setConfirm(null)}>Hủy</button>
            {confirm === 'approve'
              ? <button className="btn btn-success" disabled={busy} onClick={doApprove}>Phê duyệt</button>
              : <button className="btn btn-danger" disabled={busy} onClick={doReject}>Từ chối hồ sơ</button>}
          </>}>
          <p>{confirm === 'approve'
            ? <>Phê duyệt hồ sơ <b>{hs.code}</b> – {money(hs.amount)} cho {c?.name}? Hợp đồng sẽ có hiệu lực từ hôm nay{hs.termMonths ? ` trong ${hs.termMonths} tháng` : ''}.</>
            : <>Từ chối hồ sơ <b>{hs.code}</b> với lý do: “{note}”? Hồ sơ sẽ được trả về chuyên viên phụ trách.</>}</p>
        </Modal>
      )}
      {defer && (
        <Modal title="Tờ trình xin nợ chứng từ" onClose={() => setDefer(null)} width={500}
          footer={<><button className="btn btn-ghost" onClick={() => setDefer(null)}>Hủy</button><button className="btn btn-primary" disabled={busy} onClick={sendDeferral}>Gửi tờ trình</button></>}>
          <div className="stack" style={{ gap: 12 }}>
            <p>Xin trình duyệt hồ sơ <b>{hs.code}</b> khi còn nợ <b>{defer.item.label}</b>. Tờ trình cần Giám đốc chi nhánh / Trưởng phòng QLRR phê duyệt;
              hồ sơ sẽ hiển thị cảnh báo WARN_DOC_02 cho tới khi bổ sung đủ.</p>
            <label className="field">Thời hạn hoàn thiện (tối đa {MAX_DEFERRAL_DAYS} ngày)
              <input className="input" type="date" value={defer.dueDate} min={isoDay(1)} max={isoDay(MAX_DEFERRAL_DAYS)} onChange={(e) => setDefer({ ...defer, dueDate: e.target.value, error: '' })} />
            </label>
            <label className="field">Lý do xin nợ chứng từ
              <textarea className="textarea" rows={3} maxLength={500} value={defer.reason} onChange={(e) => setDefer({ ...defer, reason: e.target.value, error: '' })} placeholder="Vd: Khách hàng đang hoàn tất đăng ký giao dịch bảo đảm…" />
            </label>
            {defer.error && <span className="err">{defer.error}</span>}
          </div>
        </Modal>
      )}
      {decide && (
        <Modal title={decide.approve ? 'Duyệt tờ trình xin nợ chứng từ' : 'Từ chối tờ trình'} onClose={() => setDecide(null)} width={480}
          footer={<><button className="btn btn-ghost" onClick={() => setDecide(null)}>Hủy</button>
            <button className={`btn ${decide.approve ? 'btn-success' : 'btn-danger'}`} disabled={busy} onClick={sendDecision}>{decide.approve ? 'Duyệt ngoại lệ' : 'Từ chối'}</button></>}>
          <div className="stack" style={{ gap: 12 }}>
            <p>{decide.approve ? 'Cho phép' : 'Không cho phép'} hồ sơ <b>{hs.code}</b> nợ <b>{decide.d.documentLabel}</b> đến <b>{date(decide.d.dueDate)}</b>.
              <br /><span className="muted">Lý do của Maker: “{decide.d.reason}”</span></p>
            <label className="field">Ý kiến {decide.approve ? '(không bắt buộc)' : '(bắt buộc)'}
              <textarea className="textarea" rows={3} maxLength={400} value={decide.note} onChange={(e) => setDecide({ ...decide, note: e.target.value, error: '' })} />
            </label>
            {decide.error && <span className="err">{decide.error}</span>}
          </div>
        </Modal>
      )}
      {delDoc && (
        <Modal title="Xóa tài liệu?" onClose={() => setDelDoc(null)} width={440}
          footer={<><button className="btn btn-ghost" onClick={() => setDelDoc(null)}>Hủy</button><button className="btn btn-danger" disabled={busy} onClick={() => run(async () => { await data.removeDoc(delDoc.id); setDelDoc(null) }, 'Đã xóa tài liệu')}>Xóa</button></>}>
          <p>Xóa <b>{delDoc.name}</b> khỏi hồ sơ {hs.code}?</p>
        </Modal>
      )}
    </>
  )
}

const Info = ({ k, v }) => <div className="info"><span className="k">{k}</span><span className="v">{v || '—'}</span></div>
