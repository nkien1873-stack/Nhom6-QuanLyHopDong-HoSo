import { useEffect, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import Icon from '../components/Icon'
import { Logo } from '../components/UI'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'

// Nhãn vai trò của các tài khoản demo. Danh sách + mật khẩu lấy từ API và CHỈ có ở môi trường Development
// (bản Production không hiện tài khoản/mật khẩu chung trên trang đăng nhập).
const DEMO_ROLES = { admin: 'Quản trị', manager_hn: 'Quản lý duyệt', officer_hn01: 'Chuyên viên', director_hn: 'Giám đốc CN', auditor: 'Kiểm soát' }

export default function Login() {
  const { user, login } = useAuth()
  const nav = useNavigate()
  const loc = useLocation()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [show, setShow] = useState(false)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [demo, setDemo] = useState({ password: null, accounts: [] })

  useEffect(() => {
    api.demoAccounts().then(setDemo).catch(() => { /* không có danh sách demo */ })
  }, [])

  if (user) return <Navigate to="/" replace />

  const submit = async (e) => {
    e.preventDefault()
    if (!username || !password) return setError('Vui lòng nhập tên đăng nhập và mật khẩu.')
    setBusy(true)
    const r = await login(username, password)
    setBusy(false)
    if (!r.ok) return setError(r.message)
    nav(loc.state?.from || '/', { replace: true })
  }

  return (
    <div className="login">
      <section className="login-hero">
        <Logo size={44} sub="FINTECH SOFTWARE SOLUTIONS" />
        <div className="stack" style={{ maxWidth: 480, marginTop: 'auto' }}>
          <h1>Quản lý hợp đồng &amp; hồ sơ tập trung, minh bạch.</h1>
          <p>Theo dõi hồ sơ khách hàng, phê duyệt và cảnh báo hết hạn trên một nền tảng duy nhất dành cho ngân hàng và tổ chức tài chính.</p>
        </div>
        <ul>
          <li><span className="tick"><Icon name="check" size={16} stroke={2.4} /></span>Phân quyền theo vai trò, nhật ký thao tác đầy đủ</li>
          <li><span className="tick"><Icon name="check" size={16} stroke={2.4} /></span>Lưu trữ và tra cứu tài liệu nhanh</li>
          <li><span className="tick"><Icon name="check" size={16} stroke={2.4} /></span>Dashboard số liệu theo thời gian thực</li>
        </ul>
        <span style={{ fontSize: 12, color: '#7E93AA' }}>© 2026 Code&amp;Chill · Nhóm 6 – Dự án môn Quản lý dự án CNTT</span>
      </section>

      <section className="login-form-wrap">
        <form className="login-form" onSubmit={submit} noValidate>
          <div className="stack" style={{ gap: 6 }}>
            <h2 style={{ fontSize: 28, fontWeight: 700 }}>Đăng nhập</h2>
            <p className="muted" style={{ fontSize: 15 }}>Sử dụng tài khoản được cấp bởi quản trị hệ thống.</p>
          </div>
          {error && <div className="notice error" role="alert"><Icon name="alert" />{error}</div>}
          <label className="field" style={{ fontSize: 14 }}>Tên đăng nhập
            <input className="input input-lg" autoComplete="username" value={username} onChange={(e) => { setUsername(e.target.value); setError('') }} placeholder="vd: officer_hn01" />
          </label>
          <label className="field" style={{ fontSize: 14 }}>
            <span style={{ display: 'flex' }}>Mật khẩu<a href="/dang-nhap" style={{ marginLeft: 'auto', fontSize: 13, fontWeight: 600 }} onClick={(e) => { e.preventDefault(); setError('Liên hệ quản trị viên (tài khoản admin) để đặt lại mật khẩu.') }}>Quên mật khẩu?</a></span>
            <span style={{ position: 'relative', display: 'block' }}>
              <input className="input input-lg" type={show ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={(e) => { setPassword(e.target.value); setError('') }} placeholder="••••••••" style={{ paddingRight: 48 }} />
              <button type="button" className="icon-btn" onClick={() => setShow(!show)} aria-label={show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} style={{ position: 'absolute', right: 5, top: 5 }}><Icon name={show ? 'lock' : 'eye'} /></button>
            </span>
          </label>
          <button className="btn btn-primary btn-lg btn-block" type="submit" disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>

          {demo.accounts.length > 0 && (
            <div className="stack" style={{ gap: 8 }}>
              <span className="muted" style={{ fontSize: 12, fontWeight: 600 }}>TÀI KHOẢN DEMO (mật khẩu {demo.password})</span>
              <div className="demo-acc">
                {demo.accounts.map((u) => (
                  <button type="button" key={u} onClick={() => { setUsername(u); setPassword(demo.password); setError('') }}>
                    <b>{u}</b>{DEMO_ROLES[u] || ''}
                  </button>
                ))}
              </div>
            </div>
          )}
          <div className="notice"><Icon name="shield" />Phiên đăng nhập được bảo vệ bằng JWT. Mọi lần đăng nhập đều được ghi vào nhật ký hệ thống.</div>
        </form>
      </section>
    </div>
  )
}
