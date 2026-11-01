import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import Icon from '../components/Icon'
import { Avatar, Badge, Empty, Field, Modal, PageHeader, Pagination, StatusBadge, Tabs } from '../components/UI'
import { toCustomer, toHs, useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { searchTerm, useApi, useDebounced } from '../hooks/useApi'
import { api, fetchAll } from '../services/api'
import { date, downloadCsv, money, moneyShort } from '../utils/format'

const PAGE = 8
const BLANK = { kind: 'DN', name: '', idNo: '', rep: '', phone: '', email: '', address: '' }

export default function KhachHang() {
  const { saveCustomer, branchList } = useData()
  const { can } = useAuth()
  const toast = useToast()
  const [params, setParams] = useSearchParams()
  const [q, setQ] = useState('')
  const [kind, setKind] = useState('ALL')
  const [branchId, setBranchId] = useState('')
  const [page, setPage] = useState(1)
  const [form, setForm] = useState(null)
  const [errors, setErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const viewId = params.get('id')

  // Lọc + đếm theo loại + phân trang ở server; số hồ sơ và giá trị hiệu lực cũng tính sẵn
  const dq = useDebounced(searchTerm(q))
  const { data, loading } = useApi(() => api.customers({ q: dq, kind, branchId, page, pageSize: PAGE }), [dq, kind, branchId, page])
  const rows = (data?.items || []).map(toCustomer)
  const counts = data?.counts || {}

  const save = async () => {
    const e = {}
    if (!form.name.trim()) e.name = 'Bắt buộc'
    if (!/^\d{10}(\d{2})?$/.test(form.idNo.replace(/\s/g, ''))) e.idNo = form.kind === 'DN' ? 'MST gồm 10 hoặc 12 chữ số' : 'CCCD gồm 12 chữ số'
    if (!/^0\d{9,10}$/.test(form.phone.replace(/\s/g, ''))) e.phone = 'Số điện thoại không hợp lệ'
    if (form.email && !/^\S+@\S+\.\S+$/.test(form.email)) e.email = 'Email không hợp lệ'
    setErrors(e)
    if (Object.keys(e).length) return
    setSaving(true)
    try {
      await saveCustomer({ ...form, idNo: form.idNo.replace(/\s/g, '') })
      toast(form.id ? 'Đã cập nhật khách hàng' : 'Đã thêm khách hàng')
      setForm(null)
    } catch (err) {
      // Trùng CCCD/MST (server kiểm tra) => báo ngay ở ô nhập
      if (/CCCD|MST/.test(err.message)) setErrors({ idNo: err.message })
      toast(err.message, 'error')
    } finally {
      setSaving(false)
    }
  }
  const set = (k) => (e) => { setForm({ ...form, [k]: e.target.value }); setErrors({ ...errors, [k]: '' }) }

  const exportCsv = async () => {
    try {
      const list = (await fetchAll(api.customers, { q: dq, kind, branchId })).map(toCustomer)
      downloadCsv('khach-hang.csv', list.map((c) => ({ 'Tên': c.name, 'Loại': c.kind === 'DN' ? 'Doanh nghiệp' : 'Cá nhân', 'CCCD/MST': c.idNo, 'Điện thoại': c.phone, 'Email': c.email, 'Địa chỉ': c.address, 'Chi nhánh': c.branch })))
    } catch (err) { toast(err.message, 'error') }
  }

  return (
    <>
      <PageHeader title="Khách hàng" subtitle="Danh sách khách hàng cá nhân và doanh nghiệp"
        actions={<>
          <button className="btn btn-ghost" onClick={exportCsv}><Icon name="download" size={16} />Xuất Excel</button>
          {can('customer.manage') && <button className="btn btn-primary" onClick={() => { setErrors({}); setForm({ ...BLANK }) }}><Icon name="plus" size={16} stroke={2.4} />Thêm khách hàng</button>}
        </>} />

      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Loại khách hàng" value={kind} onChange={(k) => { setKind(k); setPage(1) }} items={[
          { key: 'ALL', label: 'Tất cả', count: counts.ALL },
          { key: 'DN', label: 'Doanh nghiệp', count: counts.DN },
          { key: 'CN', label: 'Cá nhân', count: counts.CN }
        ]} />
        <div className="filters">
          <div className="search"><Icon name="search" size={16} /><input type="search" aria-label="Tìm khách hàng" placeholder="Tìm tên, CCCD/MST, điện thoại…" value={q} maxLength={100} onChange={(e) => { setQ(e.target.value); setPage(1) }} /></div>
          <select className="select" aria-label="Chi nhánh" value={branchId} onChange={(e) => { setBranchId(e.target.value); setPage(1) }}><option value="">Tất cả chi nhánh</option>{branchList.map((b) => <option key={b.branchId} value={b.branchId}>{b.branchName}</option>)}</select>
        </div>
        {!data ? <Empty icon="refresh" title="Đang tải…" /> : rows.length === 0 ? <Empty title="Không tìm thấy khách hàng" /> : <>
          <div className="table-wrap" style={loading ? { opacity: 0.6 } : undefined} aria-busy={loading}>
            <table className="table">
              <thead><tr><th>Khách hàng</th><th>Loại</th><th>CCCD / MST</th><th>Liên hệ</th><th>Chi nhánh</th><th className="right">Số hồ sơ</th><th className="right">Dư nợ/giá trị hiệu lực</th><th className="right">Thao tác</th></tr></thead>
              <tbody>
                {rows.map((c) => (
                  <tr key={c.id}>
                    <td><button className="user-chip" style={{ border: 0, background: 'none', cursor: 'pointer', padding: 0, textAlign: 'left' }} onClick={() => setParams({ id: c.id })}>
                      <Avatar name={c.name} size="sm" /><span className="cell-2"><b style={{ fontWeight: 600, color: 'var(--teal-d)' }}>{c.name}</b>{c.rep && <small>ĐD: {c.rep}</small>}</span></button></td>
                    <td><Badge tone={c.kind === 'DN' ? 'info' : 'draft'}>{c.kind === 'DN' ? 'Doanh nghiệp' : 'Cá nhân'}</Badge></td>
                    <td className="num">{c.idNo}</td>
                    <td><span className="cell-2"><span>{c.phone}</span><small>{c.email}</small></span></td>
                    <td>{c.branch}</td>
                    <td className="right num">{c.contractCount}</td>
                    <td className="right num">{c.activeValue ? moneyShort(c.activeValue) : '—'}</td>
                    <td className="right">
                      <button className="icon-btn" aria-label={`Xem ${c.name}`} onClick={() => setParams({ id: c.id })}><Icon name="eye" /></button>
                      {can('customer.manage') && <button className="icon-btn" aria-label={`Sửa ${c.name}`} onClick={() => { setErrors({}); setForm({ ...c }) }}><Icon name="edit" /></button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={page} total={data.totalItems} pageSize={PAGE} onChange={setPage} label="khách hàng" />
        </>}
      </section>

      {viewId && <CustomerModal id={viewId} onClose={() => setParams({})} onEdit={(c) => { setErrors({}); setForm({ ...c }); setParams({}) }} />}

      {form && (
        <Modal title={form.id ? 'Sửa khách hàng' : 'Thêm khách hàng'} onClose={() => setForm(null)} width={640}
          footer={<><button className="btn btn-ghost" onClick={() => setForm(null)}>Hủy</button><button className="btn btn-primary" disabled={saving} onClick={save}>{saving ? 'Đang lưu…' : 'Lưu'}</button></>}>
          <div className="form-grid">
            <Field label="Loại khách hàng" required><select className="select" value={form.kind} onChange={set('kind')}><option value="DN">Doanh nghiệp</option><option value="CN">Cá nhân</option></select></Field>
            <Field label={form.kind === 'DN' ? 'Tên doanh nghiệp' : 'Họ và tên'} required error={errors.name}><input className="input" value={form.name} onChange={set('name')} /></Field>
            <Field label={form.kind === 'DN' ? 'Mã số thuế' : 'Số CCCD'} required error={errors.idNo}><input className="input" inputMode="numeric" value={form.idNo} onChange={set('idNo')} /></Field>
            {form.kind === 'DN' && <Field label="Người đại diện"><input className="input" value={form.rep} onChange={set('rep')} /></Field>}
            <Field label="Điện thoại" required error={errors.phone}><input className="input" inputMode="tel" value={form.phone} onChange={set('phone')} /></Field>
            <Field label="Email" error={errors.email}><input className="input" type="email" value={form.email} onChange={set('email')} /></Field>
            <Field label="Địa chỉ" full><input className="input" value={form.address} onChange={set('address')} /></Field>
          </div>
        </Modal>
      )}
    </>
  )
}

// Thông tin 1 khách hàng + các hồ sơ của họ (lấy riêng theo customerId)
function CustomerModal({ id, onClose, onEdit }) {
  const { can } = useAuth()
  const { data } = useApi(async () => {
    const [c, list] = await Promise.all([api.customer(id), api.contracts({ customerId: id, pageSize: 50 })])
    return { c: toCustomer(c), list: list.items.map(toHs), total: list.totalItems }
  }, [id])

  if (!data) return <Modal title="Thông tin khách hàng" onClose={onClose} width={720}><p className="muted">Đang tải…</p></Modal>
  const { c: viewing, list, total } = data

  return (
    <Modal title="Thông tin khách hàng" onClose={onClose} width={720}
      footer={<>{can('customer.manage') && <button className="btn btn-ghost" onClick={() => onEdit(viewing)}><Icon name="edit" size={16} />Sửa</button>}
        {can('hoso.create') && <Link className="btn btn-primary" to="/ho-so/tao-moi"><Icon name="plus" size={16} />Tạo hồ sơ</Link>}</>}>
      <div className="user-chip"><Avatar name={viewing.name} /><span className="cell-2"><b style={{ fontSize: 18, color: 'var(--navy)' }}>{viewing.name}</b><small>{viewing.kind === 'DN' ? 'Doanh nghiệp' : 'Cá nhân'} · khách hàng từ {date(viewing.createdAt)}</small></span></div>
      <div className="info-grid">
        <div className="info"><span className="k">{viewing.kind === 'DN' ? 'Mã số thuế' : 'Số CCCD'}</span><span className="v">{viewing.idNo}</span></div>
        {viewing.rep && <div className="info"><span className="k">Người đại diện</span><span className="v">{viewing.rep}</span></div>}
        <div className="info"><span className="k">Điện thoại</span><span className="v">{viewing.phone}</span></div>
        <div className="info"><span className="k">Email</span><span className="v">{viewing.email || '—'}</span></div>
        <div className="info"><span className="k">Chi nhánh (hồ sơ gần nhất)</span><span className="v">{viewing.branch}</span></div>
        <div className="info" style={{ gridColumn: '1 / -1' }}><span className="k">Địa chỉ</span><span className="v">{viewing.address || '—'}</span></div>
      </div>
      <h3>Hồ sơ của khách hàng ({total})</h3>
      <div className="table-wrap card">
        <table className="table">
          <thead><tr><th>Mã</th><th>Loại</th><th className="right">Giá trị</th><th>Trạng thái</th></tr></thead>
          <tbody>{list.map((x) => (
            <tr key={x.id}><td><Link to={`/ho-so/${x.id}`} style={{ fontWeight: 600 }}>{x.code}</Link></td><td>{x.type}</td><td className="right num">{money(x.amount)}</td><td><StatusBadge hs={x} /></td></tr>
          ))}</tbody>
        </table>
        {total === 0 && <p className="muted" style={{ padding: 16 }}>Chưa có hồ sơ.</p>}
        {total > list.length && <p className="muted" style={{ padding: 16 }}>Hiển thị {list.length}/{total} hồ sơ mới nhất. <Link to="/ho-so">Xem tất cả</Link></p>}
      </div>
    </Modal>
  )
}
