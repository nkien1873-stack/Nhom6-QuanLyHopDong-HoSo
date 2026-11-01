import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import Icon from './Icon'
import { Avatar, Logo } from './UI'
import { useAuth } from '../context/AuthContext'
import { toHs, useData } from '../context/DataContext'
import { useToast } from '../context/ToastContext'
import { useApi } from '../hooks/useApi'
import { api } from '../services/api'
import { ROLES } from '../data/constants'
import { fromNow } from '../utils/format'

const MAIN_NAV = [
  { to: '/', label: 'Tổng quan', icon: 'dashboard', perm: 'dashboard.view', end: true },
  { to: '/ho-so', label: 'Hợp đồng & hồ sơ', icon: 'file', perm: 'hoso.view' },
  { to: '/khach-hang', label: 'Khách hàng', icon: 'users', perm: 'hoso.view' },
  { to: '/phe-duyet', label: 'Phê duyệt', icon: 'check', perm: 'hoso.approve', badge: true },
  { to: '/ngoai-le', label: 'Duyệt ngoại lệ', icon: 'shield', perm: 'deferral.view', badge: 'deferral' },
  { to: '/tai-lieu', label: 'Tài liệu', icon: 'clip', perm: 'hoso.view' },
  { to: '/bao-cao', label: 'Báo cáo', icon: 'chart', perm: 'report.view' }
]
const ADMIN_NAV = [
  { to: '/nguoi-dung', label: 'Người dùng & quyền', icon: 'shield', perm: 'user.manage' },
  { to: '/nhat-ky', label: 'Nhật ký hệ thống', icon: 'clock', perm: 'audit.view' }
]

export default function Layout() {
  const { user, logout, can } = useAuth()
  const { reload } = useData()
  const toast = useToast()
  const nav = useNavigate()
  const loc = useLocation()
  const [open, setOpen] = useState(false)
  const [menu, setMenu] = useState(null) // 'notif' | 'user' | null
  const [q, setQ] = useState('')
  const menuRef = useRef(null)

  // Số hồ sơ chờ duyệt + 5 hồ sơ mới nhất (server lọc theo chi nhánh của người duyệt); làm mới khi chuyển trang
  const { data: notif } = useApi(() => api.notifications(), [loc.pathname])
  const pendingCount = notif?.count ?? 0
  // Giám đốc CN / QLRR: số tờ trình xin nợ chứng từ đang chờ mình duyệt
  const isDirector = user.role === 'DIRECTOR'
  const { data: deferrals } = useApi(() => (isDirector
    ? api.deferrals({ status: 'PENDING', branchId: user.branchId ?? '', pageSize: 1 })
    : Promise.resolve(null)), [loc.pathname, isDirector])
  const deferralCount = deferrals?.totalItems ?? 0
  const pending = (notif?.items || []).map(toHs)

  useEffect(() => { setOpen(false); setMenu(null) }, [loc.pathname])
  useEffect(() => {
    const close = (e) => menuRef.current && !menuRef.current.contains(e.target) && setMenu(null)
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [])

  const search = (e) => {
    e.preventDefault()
    nav(`/ho-so?q=${encodeURIComponent(q)}`)
  }

  const renderLink = (it) => can(it.perm) && (
    <NavLink key={it.to} to={it.to} end={it.end} className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
      <Icon name={it.icon} />{it.label}
      {it.badge === true && pendingCount > 0 && <span className="nav-count">{pendingCount}</span>}
      {it.badge === 'deferral' && deferralCount > 0 && <span className="nav-count">{deferralCount}</span>}
    </NavLink>
  )
  const adminLinks = ADMIN_NAV.filter((i) => can(i.perm))

  return (
    <div className="shell">
      <aside className={`sidebar ${open ? 'open' : ''}`} aria-label="Thanh điều hướng">
        <Logo />
        <nav className="nav-group" aria-label="Điều hướng chính">{MAIN_NAV.map(renderLink)}</nav>
        {adminLinks.length > 0 && (
          <div className="nav-group">
            <span className="nav-title">QUẢN TRỊ</span>
            <nav className="nav-group" aria-label="Quản trị">{ADMIN_NAV.map(renderLink)}</nav>
          </div>
        )}
        <div className="sidebar-foot">© 2026 Code&amp;Chill · Nhóm 6</div>
      </aside>
      <div className={`backdrop ${open ? 'open' : ''}`} onClick={() => setOpen(false)} />

      <div className="main">
        <header className="topbar">
          <button className="icon-btn bordered menu-btn" onClick={() => setOpen(true)} aria-label="Mở menu"><Icon name="menu" /></button>
          <form className="search" onSubmit={search} role="search" style={{ maxWidth: 520 }}>
            <Icon name="search" />
            <label className="sr-only" htmlFor="global-q">Tìm kiếm</label>
            <input id="global-q" type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm số hợp đồng, khách hàng, CCCD/MST…" />
          </form>

          <div ref={menuRef} style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 12, position: 'relative' }}>
            <button className="icon-btn bordered" aria-label={`Thông báo (${pendingCount})`} aria-expanded={menu === 'notif'} onClick={() => setMenu(menu === 'notif' ? null : 'notif')}>
              <Icon name="bell" />{pendingCount > 0 && <span className="notif-dot" />}
            </button>
            <button className="user-chip" onClick={() => setMenu(menu === 'user' ? null : 'user')} aria-expanded={menu === 'user'}
              style={{ border: 0, background: 'none', cursor: 'pointer', textAlign: 'left', padding: 4, borderRadius: 10 }}>
              <Avatar name={user.name} />
              <span className="cell-2" style={{ lineHeight: 1.3 }}>
                <b style={{ fontSize: 14 }}>{user.name}</b>
                <small>{ROLES[user.role].name} · {user.branch}</small>
              </span>
            </button>

            {menu === 'notif' && (
              <div className="card" style={{ position: 'absolute', right: 0, top: 52, width: 340, boxShadow: 'var(--shadow)', zIndex: 30 }}>
                <div className="card-head" style={{ paddingBottom: 8 }}><h2>Thông báo</h2></div>
                {pending.length === 0 ? <p className="muted" style={{ padding: '0 22px 18px' }}>Không có thông báo mới.</p> : (
                  <div style={{ display: 'flex', flexDirection: 'column', padding: '0 10px 10px' }}>
                    {pending.map((x) => (
                      <button key={x.id} onClick={() => nav(`/ho-so/${x.id}`)} className="task" style={{ border: 0, background: 'none', cursor: 'pointer', textAlign: 'left' }}>
                        <i className="dot" style={{ background: '#F59E0B', marginTop: 6 }} />
                        <span className="cell-2"><span className="title">{x.code} chờ phê duyệt</span><small>{x.customerName} · {fromNow(x.updatedAt)}</small></span>
                      </button>
                    ))}
                    {pendingCount > pending.length && can('hoso.approve') && (
                      <button onClick={() => nav('/phe-duyet')} className="nav-link" style={{ border: 0, background: 'none', cursor: 'pointer' }}>Xem tất cả {pendingCount} hồ sơ chờ duyệt →</button>
                    )}
                  </div>
                )}
              </div>
            )}
            {menu === 'user' && (
              <div className="card" style={{ position: 'absolute', right: 0, top: 52, width: 240, boxShadow: 'var(--shadow)', zIndex: 30, padding: 8, display: 'flex', flexDirection: 'column' }}>
                <button className="nav-link" style={{ color: 'var(--text)', border: 0, background: 'none', cursor: 'pointer' }}
                  onClick={async () => { setMenu(null); await reload(); toast('Đã tải lại dữ liệu mới nhất') }}>
                  <Icon name="refresh" />Tải lại dữ liệu
                </button>
                <button className="nav-link" style={{ color: '#B91C1C', border: 0, background: 'none', cursor: 'pointer' }}
                  onClick={async () => { await logout(); nav('/dang-nhap') }}>
                  <Icon name="logout" />Đăng xuất
                </button>
              </div>
            )}
          </div>
        </header>
        <main className="content" id="noi-dung">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
