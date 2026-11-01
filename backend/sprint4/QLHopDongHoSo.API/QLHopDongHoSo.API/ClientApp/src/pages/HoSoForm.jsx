import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import Icon from '../components/Icon'
import { Badge, DocIcon, Empty, Field, PageHeader } from '../components/UI'
import { kindOf, toCustomer, toHs, useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { searchTerm, useApi, useDebounced } from '../hooks/useApi'
import { api } from '../services/api'
import { DOC_TYPES, REQUIREMENT, UPLOAD_EXTS, UPLOAD_MAX_MB, docRequirements } from '../data/constants'
import { fileSize, money } from '../utils/format'

const NEW_CUS = { kind: 'DN', name: '', idNo: '', phone: '', email: '', address: '', rep: '' }

// Sửa hồ sơ: tải đúng hồ sơ đó (kèm rowVersion để phát hiện người khác sửa cùng lúc)
export default function HoSoForm() {
  const { id } = useParams()
  const { data: editing, error } = useApi(() => (id ? api.contract(id).then(toHs) : Promise.resolve(null)), [id])
  if (id && error && !editing) return <div className="card"><p className="muted" style={{ padding: 22 }}>Không tìm thấy hồ sơ.</p></div>
  if (id && (!editing || editing.id !== id)) return <div className="card"><Empty icon="refresh" title="Đang tải hồ sơ…" /></div>
  return <HoSoFormInner key={id || 'new'} editing={id ? editing : null} />
}

function HoSoFormInner({ editing }) {
  const data = useData()
  const { user } = useAuth()
  const toast = useToast()
  const nav = useNavigate()
  const fileRef = useRef(null)
  const isAdmin = user.role === 'ADMIN'

  // Chọn khách hàng: tìm ở server (không tải toàn bộ danh sách khách hàng)
  const [cusQ, setCusQ] = useState('')
  const dCusQ = useDebounced(searchTerm(cusQ))
  const { data: cusPage } = useApi(() => api.customers({ q: dCusQ, pageSize: 20 }), [dCusQ])
  const [picked, setPicked] = useState(editing ? { id: editing.customerId, name: editing.customerName, kind: editing.customerKind, idNo: editing.customerIdNo } : null)
  const cusOptions = (cusPage?.items || []).map(toCustomer)
  if (picked && !cusOptions.some((c) => c.id === picked.id)) cusOptions.unshift(picked)

  const [f, setF] = useState(() => editing
    ? {
      customerId: editing.customerId, typeId: editing.typeId, amount: editing.amount != null ? String(editing.amount) : '', termMonths: editing.termMonths ?? 12,
      rate: editing.rate ?? '', purpose: editing.purpose || '', collateral: editing.collateral || '', branchId: editing.branchId
    }
    : {
      customerId: '', typeId: data.typeList[0]?.contractTypeId ?? '', amount: '', termMonths: 12, rate: '', purpose: '', collateral: '',
      // Chuyên viên tạo hồ sơ cho chi nhánh của mình; Admin chọn chi nhánh
      branchId: user.branchId ?? data.branchList[0]?.branchId ?? ''
    })
  const [newCus, setNewCus] = useState(null)
  const [files, setFiles] = useState([])
  const [drag, setDrag] = useState(false)
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)

  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setErrors({ ...errors, [k]: '' }) }
  const setC = (k) => (e) => { setNewCus({ ...newCus, [k]: e.target.value }); setErrors({ ...errors, [`c_${k}`]: '' }) }

  const validate = () => {
    const e = {}
    if (newCus) {
      if (!newCus.name.trim()) e.c_name = 'Bắt buộc'
      // Trùng CCCD/MST do server kiểm tra (báo lỗi khi lưu)
      if (!/^\d{10}(\d{2})?$/.test(newCus.idNo.replace(/\s/g, ''))) e.c_idNo = newCus.kind === 'DN' ? 'MST gồm 10 hoặc 12 chữ số' : 'CCCD gồm 12 chữ số'
      if (!/^0\d{9,10}$/.test(newCus.phone.replace(/\s/g, ''))) e.c_phone = 'Số điện thoại không hợp lệ'
    } else if (!f.customerId) e.customerId = 'Chọn khách hàng hoặc thêm mới'
    if (!f.typeId) e.typeId = 'Chọn loại hợp đồng'
    const amt = Number(String(f.amount).replace(/\D/g, ''))
    if (!amt || amt < 1000000) e.amount = 'Tối thiểu 1.000.000 ₫'
    if (!f.termMonths || f.termMonths < 1 || f.termMonths > 360) e.termMonths = 'Từ 1 đến 360 tháng'
    if (!f.purpose.trim()) e.purpose = 'Bắt buộc'
    if (f.rate !== '' && (Number(f.rate) < 0 || Number(f.rate) > 30)) e.rate = 'Từ 0 đến 30'
    if (!f.branchId) e.branchId = 'Chọn chi nhánh'
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const save = async (submit) => {
    if (!validate()) { toast('Vui lòng kiểm tra các trường đang báo lỗi', 'error'); return }
    setSaving(true)
    try {
      let customerId = f.customerId
      if (newCus) customerId = (await data.saveCustomer({ ...newCus, idNo: newCus.idNo.replace(/\s/g, '') })).id
      const payload = {
        customerId,
        typeId: Number(f.typeId),
        amount: Number(String(f.amount).replace(/\D/g, '')),
        termMonths: Number(f.termMonths),
        rate: f.rate === '' ? null : Number(f.rate),
        purpose: f.purpose.trim(),
        collateral: f.collateral.trim() || 'Tín chấp',
        branchId: Number(f.branchId)
      }
      if (editing) {
        const r = await data.updateHoSo(editing, payload, files, submit)
        report(r, submit ? `Đã lưu và gửi ${editing.code} đi phê duyệt` : `Đã lưu ${editing.code}`, `Đã lưu ${editing.code} nhưng chưa gửi được`)
        nav(`/ho-so/${editing.id}`)
      } else {
        const r = await data.createHoSo(payload, files, submit)
        report(r, submit ? `Đã tạo ${r.code} và gửi phê duyệt` : `Đã lưu nháp ${r.code}`, `Đã lưu nháp ${r.code} nhưng chưa gửi được`)
        nav(`/ho-so/${r.id}`)
      }
    } catch (e) {
      toast(e.message, 'error')
    } finally {
      setSaving(false)
    }
  }

  // Kết quả lưu: tệp lỗi, gửi duyệt bị chặn (ERR_DOC_01/02 – hồ sơ vẫn đã lưu nháp), cảnh báo nợ chứng từ
  const report = (r, okMsg, savedMsg) => {
    if (r.failed.length) toast(`Không tải được: ${r.failed.join('; ')}`, 'error')
    if (r.submitError) toast(`${savedMsg}: ${r.submitError}`, 'error')
    else toast(okMsg)
    r.warnings.forEach((w) => toast(w, 'warn'))
  }

  // Danh mục tài liệu theo loại khách hàng / loại hợp đồng / TSBĐ (backend kiểm tra lại khi gửi duyệt)
  const cusKind = newCus ? newCus.kind : picked?.kind
  const required = cusKind ? docRequirements(cusKind, data.typeList.find((t) => t.contractTypeId === Number(f.typeId))?.typeName, f.collateral.trim() || 'Tín chấp') : []
  const nextType = (chosen) => required.find((r) => !chosen.includes(r.type))?.type || 'ATTACHMENT'

  // Chỉ nhận đúng loại và dung lượng backend cho phép
  const addFiles = (list) => {
    const ok = []
    const bad = []
    for (const x of list) {
      const ext = `.${x.name.split('.').pop().toLowerCase()}`
      if (!UPLOAD_EXTS.includes(ext)) bad.push(`${x.name} (không hỗ trợ)`)
      else if (x.size > UPLOAD_MAX_MB * 1048576) bad.push(`${x.name} (quá ${UPLOAD_MAX_MB} MB)`)
      else ok.push(x)
    }
    if (bad.length) toast(`Bỏ qua: ${bad.join(', ')}`, 'error')
    // Gợi ý loại tài liệu: lần lượt các loại yêu cầu còn thiếu, đổi được ở từng tệp
    setFiles((s) => {
      const next = [...s]
      for (const file of ok) next.push({ file, type: nextType(next.map((x) => x.type)) })
      return next
    })
  }
  const amountNum = Number(String(f.amount).replace(/\D/g, ''))
  const typeName = data.typeList.find((t) => t.contractTypeId === Number(f.typeId))?.typeName

  return (
    <>
      <PageHeader
        crumbs={<nav className="breadcrumb" aria-label="Breadcrumb"><Link to="/ho-so">Hợp đồng &amp; hồ sơ</Link><span aria-hidden="true">/</span><span className="current">{editing ? `Sửa ${editing.code}` : 'Tạo hồ sơ mới'}</span></nav>}
        title={editing ? `Chỉnh sửa ${editing.code}` : 'Tạo hồ sơ mới'}
        subtitle="Các trường có dấu * là bắt buộc. Có thể lưu nháp và bổ sung sau. Mã hồ sơ do hệ thống tự cấp."
      />

      <form className="split" onSubmit={(e) => { e.preventDefault(); save(true) }} noValidate>
        <div className="main-col">
          <section className="card card-pad stack">
            <div className="row">
              <h2>1. Khách hàng</h2>
              {!editing && <button type="button" className="btn btn-ghost" style={{ marginLeft: 'auto', minHeight: 36 }} onClick={() => setNewCus(newCus ? null : { ...NEW_CUS })}>
                <Icon name={newCus ? 'x' : 'plus'} size={16} />{newCus ? 'Chọn khách hàng có sẵn' : 'Khách hàng mới'}
              </button>}
            </div>
            {!newCus ? (
              <div className="form-grid">
                {!editing && (
                  <Field label="Tìm khách hàng" hint="Gõ tên, CCCD/MST hoặc số điện thoại">
                    <input className="input" type="search" value={cusQ} maxLength={100} onChange={(e) => setCusQ(e.target.value)} placeholder="Tìm khách hàng…" />
                  </Field>
                )}
                <Field label="Khách hàng" required error={errors.customerId}>
                  <select className="select" value={f.customerId} disabled={!!editing}
                    onChange={(e) => { set('customerId')(e); setPicked(cusOptions.find((c) => c.id === e.target.value) || null) }}>
                    <option value="">— Chọn khách hàng —</option>
                    {cusOptions.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.kind === 'DN' ? 'MST' : 'CCCD'} {c.idNo}</option>)}
                  </select>
                </Field>
              </div>
            ) : (
              <div className="form-grid">
                <Field label="Loại khách hàng" required>
                  <select className="select" value={newCus.kind} onChange={setC('kind')}><option value="DN">Doanh nghiệp</option><option value="CN">Cá nhân</option></select>
                </Field>
                <Field label={newCus.kind === 'DN' ? 'Tên doanh nghiệp' : 'Họ và tên'} required error={errors.c_name}><input className="input" value={newCus.name} onChange={setC('name')} /></Field>
                <Field label={newCus.kind === 'DN' ? 'Mã số thuế' : 'Số CCCD'} required error={errors.c_idNo}><input className="input" inputMode="numeric" value={newCus.idNo} onChange={setC('idNo')} /></Field>
                {newCus.kind === 'DN' && <Field label="Người đại diện"><input className="input" value={newCus.rep} onChange={setC('rep')} /></Field>}
                <Field label="Điện thoại" required error={errors.c_phone}><input className="input" inputMode="tel" value={newCus.phone} onChange={setC('phone')} /></Field>
                <Field label="Email"><input className="input" type="email" value={newCus.email} onChange={setC('email')} /></Field>
                <Field label="Địa chỉ" full><input className="input" value={newCus.address} onChange={setC('address')} /></Field>
              </div>
            )}
          </section>

          <section className="card card-pad stack">
            <h2>2. Thông tin hợp đồng</h2>
            <div className="form-grid">
              <Field label="Loại hợp đồng" required error={errors.typeId}>
                <select className="select" value={f.typeId} onChange={set('typeId')}>{data.typeList.map((t) => <option key={t.contractTypeId} value={t.contractTypeId}>{t.typeName}</option>)}</select>
              </Field>
              <Field label="Số tiền (₫)" required error={errors.amount} hint={amountNum ? money(amountNum) : 'Nhập số, vd 500000000'}>
                <input className={`input num ${errors.amount ? 'invalid' : ''}`} inputMode="numeric" value={f.amount ? Number(String(f.amount).replace(/\D/g, '')).toLocaleString('vi-VN') : ''}
                  onChange={(e) => { setF({ ...f, amount: e.target.value.replace(/\D/g, '') }); setErrors({ ...errors, amount: '' }) }} />
              </Field>
              <Field label="Thời hạn (tháng)" required error={errors.termMonths}><input className="input" type="number" min="1" max="360" value={f.termMonths} onChange={set('termMonths')} /></Field>
              <Field label="Lãi suất (%/năm)" error={errors.rate} hint="Để trống nếu theo biểu lãi suất hiện hành"><input className="input" type="number" step="0.1" value={f.rate} onChange={set('rate')} /></Field>
              <Field label="Chi nhánh" required error={errors.branchId} hint={!isAdmin ? 'Theo chi nhánh của bạn' : editing ? 'Không đổi được sau khi tạo' : undefined}>
                <select className="select" value={f.branchId} onChange={set('branchId')} disabled={!isAdmin || !!editing}>
                  {data.branchList.map((b) => <option key={b.branchId} value={b.branchId}>{b.branchName}</option>)}
                </select>
              </Field>
              <Field label="Tài sản bảo đảm" hint="Để trống nếu tín chấp"><input className="input" value={f.collateral} onChange={set('collateral')} /></Field>
              <Field label="Mục đích" required error={errors.purpose} full><textarea className="textarea" rows={3} value={f.purpose} onChange={set('purpose')} /></Field>
            </div>
          </section>

          <section className="card card-pad stack">
            <h2>3. Tài liệu đính kèm</h2>
            <div className={`dropzone ${drag ? 'drag' : ''}`} role="button" tabIndex={0}
              onClick={() => fileRef.current.click()} onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && fileRef.current.click()}
              onDragOver={(e) => { e.preventDefault(); setDrag(true) }} onDragLeave={() => setDrag(false)}
              onDrop={(e) => { e.preventDefault(); setDrag(false); addFiles(e.dataTransfer.files) }}>
              <Icon name="upload" size={28} />
              <b style={{ color: 'var(--text)' }}>Kéo thả tệp vào đây hoặc bấm để chọn</b>
              <span style={{ fontSize: 13 }}>PDF, Word, Excel, ảnh JPG/PNG · tối đa {UPLOAD_MAX_MB} MB mỗi tệp</span>
              <input ref={fileRef} type="file" multiple hidden accept={UPLOAD_EXTS.join(',')} onChange={(e) => { addFiles(e.target.files); e.target.value = '' }} />
            </div>
            {required.length > 0 && (
              <div className="notice" role="note">
                <Icon name="clip" />
                <span>Tài liệu cần có: {required.map((r, i) => <span key={r.type}>{i > 0 && ', '}<b>{DOC_TYPES[r.type]}</b> ({REQUIREMENT[r.requirement].label.toLowerCase()})</span>)}.
                  Thiếu tài liệu bắt buộc thì không gửi phê duyệt được; tài liệu bổ sung sau có thể lập Tờ trình xin nợ chứng từ ở trang chi tiết.</span>
              </div>
            )}
            {editing?.docCount > 0 && <p className="muted" style={{ fontSize: 13 }}>Hồ sơ đang có {editing.docCount} tài liệu. Tệp mới sẽ được thêm vào.</p>}
            {files.map((x, i) => {
              const req = required.find((r) => r.type === x.type)
              return (
                <div className="doc" key={i}>
                  <DocIcon kind={kindOf(x.file.name)} />
                  <span className="cell-2" style={{ flex: 1, minWidth: 0 }}><span className="doc-name">{x.file.name}</span><small>{fileSize(x.file.size)}</small></span>
                  <select className="select" style={{ width: 'auto', minHeight: 36 }} aria-label={`Loại tài liệu của ${x.file.name}`} value={x.type}
                    onChange={(e) => setFiles(files.map((y, j) => (j === i ? { ...y, type: e.target.value } : y)))}>
                    {Object.entries(DOC_TYPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </select>
                  {req && <Badge tone={REQUIREMENT[req.requirement].tone}>{REQUIREMENT[req.requirement].label}</Badge>}
                  <button type="button" className="icon-btn" aria-label={`Bỏ ${x.file.name}`} onClick={() => setFiles(files.filter((_, j) => j !== i))}><Icon name="x" /></button>
                </div>
              )
            })}
          </section>
        </div>

        <div className="side-col" style={{ position: 'sticky', top: 90 }}>
          <section className="card card-pad stack" style={{ gap: 14 }}>
            <h2>Tóm tắt</h2>
            <div className="info"><span className="k">Khách hàng</span><span className="v">{newCus ? (newCus.name || '—') + ' (mới)' : picked?.name || '—'}</span></div>
            <div className="info"><span className="k">Loại hợp đồng</span><span className="v">{typeName || '—'}</span></div>
            <div className="info"><span className="k">Số tiền</span><span className="v" style={{ fontSize: 18, fontWeight: 700, color: 'var(--navy)' }}>{amountNum ? money(amountNum) : '—'}</span></div>
            <div className="info"><span className="k">Thời hạn</span><span className="v">{f.termMonths || '—'} tháng</span></div>
            <div className="info"><span className="k">Tài liệu</span><span className="v">{(editing?.docCount || 0) + files.length} tệp</span></div>
            <button type="submit" className="btn btn-primary btn-lg" disabled={saving}><Icon name="send" size={16} />{saving ? 'Đang lưu…' : 'Lưu & gửi phê duyệt'}</button>
            <button type="button" className="btn btn-ghost" disabled={saving} onClick={() => save(false)}>Lưu nháp</button>
            <Link className="btn btn-ghost" to={editing ? `/ho-so/${editing.id}` : '/ho-so'} style={{ border: 0 }}>Hủy</Link>
          </section>
        </div>
      </form>
    </>
  )
}
