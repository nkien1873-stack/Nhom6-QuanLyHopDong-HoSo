import { useState } from 'react'
import Icon from '../components/Icon'
import { Avatar, Badge, Field, Modal, PageHeader, Tabs } from '../components/UI'
import { useData } from '../context/DataContext'
import { useAuth } from '../context/AuthContext'
import { useToast } from '../context/ToastContext'
import { DEFAULT_PASSWORD, PERMISSIONS, ROLES } from '../data/constants'
import { fromNow, norm } from '../utils/format'

const BLANK = { name: '', username: '', email: '', role: 'STAFF', branch: '' }

export default function NguoiDung() {
  const { users, saveUser, toggleUser, branches } = useData()
  const { user } = useAuth()
  const toast = useToast()
  const [tab, setTab] = useState('users')
  const [q, setQ] = useState('')
  const [role, setRole] = useState('')
  const [form, setForm] = useState(null)
  const [errors, setErrors] = useState({})
  const [lock, setLock] = useState(null)
  const [busy, setBusy] = useState(false)

  const rows = users.filter((u) => (!role || u.role === role) && (!q || norm(`${u.name} ${u.username} ${u.email}`).includes(norm(q))))
  const set = (k) => (e) => { setForm({ ...form, [k]: e.target.value }); setErrors({ ...errors, [k]: '' }) }

  const save = async () => {
    const e = {}
    if (!form.name.trim()) e.name = 'Bắt buộc'
    if (!/^[a-z0-9._]{3,30}$/.test(form.username)) e.username = '3–30 ký tự: chữ thường, số, dấu chấm, gạch dưới'
    else if (users.some((u) => u.username === form.username && u.id !== form.id)) e.username = 'Tên đăng nhập đã tồn tại'
    if (!/^\S+@\S+\.\S+$/.test(form.email)) e.email = 'Email không hợp lệ'
    if (form.id === user.id && form.role !== 'ADMIN') e.role = 'Không thể tự hạ quyền quản trị của chính mình'
    if (['MANAGER', 'STAFF'].includes(form.role) && (!form.branch || form.branch === '—')) e.branch = 'Quản lý và Chuyên viên phải thuộc một chi nhánh'
    setErrors(e)
    if (Object.keys(e).length) return
    setBusy(true)
    try {
      await saveUser({ ...form, password: DEFAULT_PASSWORD })
      toast(form.id ? 'Đã cập nhật người dùng' : `Đã tạo tài khoản ${form.username} (mật khẩu ${DEFAULT_PASSWORD})`)
      setForm(null)
    } catch (err) {
      toast(err.message, 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader title="Người dùng & phân quyền" subtitle="Quản lý tài khoản và quyền truy cập theo vai trò (RBAC)"
        actions={<button className="btn btn-primary" onClick={() => { setErrors({}); setForm({ ...BLANK }) }}><Icon name="plus" size={16} stroke={2.4} />Thêm người dùng</button>} />

      <section className="card" style={{ overflow: 'hidden' }}>
        <Tabs label="Quản trị" value={tab} onChange={setTab} items={[{ key: 'users', label: 'Người dùng', count: users.length }, { key: 'roles', label: 'Vai trò & quyền', count: Object.keys(ROLES).length }]} />

        {tab === 'users' && <>
          <div className="filters">
            <div className="search"><Icon name="search" size={16} /><input type="search" aria-label="Tìm người dùng" placeholder="Tìm tên, tên đăng nhập, email…" value={q} onChange={(e) => setQ(e.target.value)} /></div>
            <select className="select" aria-label="Vai trò" value={role} onChange={(e) => setRole(e.target.value)}><option value="">Tất cả vai trò</option>{Object.values(ROLES).map((r) => <option key={r.key} value={r.key}>{r.name}</option>)}</select>
          </div>
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Người dùng</th><th>Tên đăng nhập</th><th>Vai trò</th><th>Chi nhánh</th><th>Đăng nhập gần nhất</th><th>Trạng thái</th><th className="right">Thao tác</th></tr></thead>
              <tbody>{rows.map((u) => (
                <tr key={u.id}>
                  <td><span className="user-chip"><Avatar name={u.name} size="sm" /><span className="cell-2"><b style={{ fontWeight: 600 }}>{u.name}{u.id === user.id && <span className="muted" style={{ fontWeight: 400 }}> (bạn)</span>}</b><small>{u.email}</small></span></span></td>
                  <td className="num">{u.username}</td>
                  <td><Badge tone={ROLES[u.role].tone}>{ROLES[u.role].name}</Badge></td>
                  <td>{u.branch}</td>
                  <td className="muted">{u.lastLogin ? fromNow(u.lastLogin) : 'Chưa đăng nhập'}</td>
                  <td>{u.active ? <span className="row" style={{ gap: 6 }}><i className="dot" style={{ background: '#14A3A0' }} />Hoạt động</span> : <span className="row muted" style={{ gap: 6 }}><i className="dot" style={{ background: '#94A3B8' }} />Đã khóa</span>}</td>
                  <td className="right">
                    <button className="icon-btn" aria-label={`Sửa ${u.name}`} onClick={() => { setErrors({}); setForm({ ...u }) }}><Icon name="edit" /></button>
                    {u.id !== user.id && <button className="icon-btn" aria-label={u.active ? `Khóa ${u.name}` : `Mở khóa ${u.name}`} onClick={() => setLock(u)}><Icon name={u.active ? 'lock' : 'unlock'} /></button>}
                  </td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        </>}

        {tab === 'roles' && (
          <div className="stack" style={{ padding: 22 }}>
            <div className="grid-stats">
              {Object.values(ROLES).map((r) => (
                <div key={r.key} className="card card-pad stack" style={{ gap: 8 }}>
                  <Badge tone={r.tone}>{r.name}</Badge>
                  <b style={{ fontSize: 22, color: 'var(--navy)' }}>{users.filter((u) => u.role === r.key).length} <span className="muted" style={{ fontSize: 14, fontWeight: 500 }}>người dùng</span></b>
                  <small className="muted">{PERMISSIONS.filter((p) => p.roles.includes(r.key)).length}/{PERMISSIONS.length} quyền</small>
                </div>
              ))}
            </div>
            <div className="table-wrap card">
              <table className="table matrix">
                <thead><tr><th>Quyền</th>{Object.values(ROLES).map((r) => <th key={r.key}>{r.name}</th>)}</tr></thead>
                <tbody>{PERMISSIONS.map((p) => (
                  <tr key={p.key}>
                    <td><span className="cell-2"><span style={{ fontWeight: 500 }}>{p.name}</span><small className="num">{p.key}</small></span></td>
                    {Object.keys(ROLES).map((r) => (
                      <td key={r}>{p.roles.includes(r)
                        ? <span style={{ color: 'var(--teal-d)' }} aria-label="Có quyền" role="img"><Icon name="check" stroke={2.6} /></span>
                        : <span className="muted" aria-label="Không có quyền" role="img">—</span>}</td>
                    ))}
                  </tr>
                ))}</tbody>
              </table>
            </div>
            <p className="muted" style={{ fontSize: 13 }}>Ma trận quyền được định nghĩa trong <code>src/data/constants.js</code> (PERMISSIONS) và cần khớp với policy phía API.</p>
          </div>
        )}
      </section>

      {form && (
        <Modal title={form.id ? 'Sửa người dùng' : 'Thêm người dùng'} onClose={() => setForm(null)} width={600}
          footer={<><button className="btn btn-ghost" onClick={() => setForm(null)}>Hủy</button><button className="btn btn-primary" disabled={busy} onClick={save}>{busy ? 'Đang lưu…' : 'Lưu'}</button></>}>
          <div className="form-grid">
            <Field label="Họ và tên" required error={errors.name}><input className="input" value={form.name} onChange={set('name')} /></Field>
            <Field label="Tên đăng nhập" required error={errors.username}><input className="input" value={form.username} disabled={!!form.id} onChange={(e) => { setForm({ ...form, username: e.target.value.toLowerCase() }); setErrors({ ...errors, username: '' }) }} /></Field>
            <Field label="Email" required error={errors.email}><input className="input" type="email" value={form.email} onChange={set('email')} /></Field>
            <Field label="Vai trò" required error={errors.role}><select className="select" value={form.role} onChange={set('role')}>{Object.values(ROLES).map((r) => <option key={r.key} value={r.key}>{r.name}</option>)}</select></Field>
            <Field label="Chi nhánh" required={['MANAGER', 'STAFF'].includes(form.role)} error={errors.branch}><select className="select" value={form.branch === '—' ? '' : form.branch} onChange={set('branch')}><option value="">— Không gán (Quản trị / Kiểm soát / QLRR hội sở) —</option>{branches.map((b) => <option key={b}>{b}</option>)}</select></Field>
          </div>
          {!form.id && <div className="notice"><Icon name="lock" />Mật khẩu mặc định là {DEFAULT_PASSWORD}. Người dùng nên đổi mật khẩu ở lần đăng nhập đầu tiên.</div>}
        </Modal>
      )}
      {lock && (
        <Modal title={lock.active ? 'Khóa tài khoản?' : 'Mở khóa tài khoản?'} onClose={() => setLock(null)} width={440}
          footer={<><button className="btn btn-ghost" onClick={() => setLock(null)}>Hủy</button>
            <button className={`btn ${lock.active ? 'btn-danger' : 'btn-primary'}`} disabled={busy} onClick={async () => {
              setBusy(true)
              try { await toggleUser(lock); toast(lock.active ? `Đã khóa ${lock.username}` : `Đã mở khóa ${lock.username}`); setLock(null) } catch (err) { toast(err.message, 'error') } finally { setBusy(false) }
            }}>{lock.active ? 'Khóa' : 'Mở khóa'}</button></>}>
          <p>{lock.active ? <><b>{lock.name}</b> sẽ không thể đăng nhập cho đến khi được mở khóa.</> : <><b>{lock.name}</b> sẽ có thể đăng nhập lại.</>}</p>
        </Modal>
      )}
    </>
  )
}
