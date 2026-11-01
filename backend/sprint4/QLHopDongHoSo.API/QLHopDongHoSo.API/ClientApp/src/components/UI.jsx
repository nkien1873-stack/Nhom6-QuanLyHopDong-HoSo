import { useEffect, useRef } from 'react'
import Icon from './Icon'
import { avatarTone, initials } from '../utils/format'
import { statusOf } from '../context/DataContext'

export function Logo({ size = 36, light = true, sub = 'QUẢN LÝ HỢP ĐỒNG' }) {
  return (
    <div className="brand">
      {/* Icon chính thức của nhóm: public/brand/CodeAndChill_icon.svg */}
      <img src="/brand/CodeAndChill_icon.svg" width={size} height={size} alt="" aria-hidden="true" style={{ borderRadius: size * 0.23 }} />
      <div style={{ display: 'flex', flexDirection: 'column' }}>
        <span className="brand-name" style={light ? undefined : { color: 'var(--navy)' }}>Code<span>&amp;</span>Chill</span>
        <span className="brand-sub">{sub}</span>
      </div>
    </div>
  )
}

export const Badge = ({ tone = 'draft', children, lg }) => <span className={`badge tone-${tone} ${lg ? 'lg' : ''}`}>{children}</span>

export const StatusBadge = ({ hs, lg }) => {
  const s = statusOf(hs)
  return <Badge tone={s.tone} lg={lg}>{s.label}</Badge>
}

export function Avatar({ name, size }) {
  const [bg, fg] = avatarTone(name)
  return <span className={`avatar ${size === 'sm' ? 'sm' : ''}`} style={{ background: bg, color: fg }} aria-hidden="true">{initials(name)}</span>
}

export function PageHeader({ title, subtitle, actions, crumbs }) {
  return (
    <div className="page-head">
      <div className="titles">
        {crumbs}
        <h1>{title}</h1>
        {subtitle && <p className="muted">{subtitle}</p>}
      </div>
      {actions && <div className="actions">{actions}</div>}
    </div>
  )
}

export function StatCard({ label, value, unit, note, noteTone, icon, tone }) {
  return (
    <div className="card stat">
      <div className="stat-top">
        <span className="stat-label">{label}</span>
        <span className={`stat-icon tone-${tone}`}><Icon name={icon} /></span>
      </div>
      <span className="stat-value">{value}{unit && <small> {unit}</small>}</span>
      {note && <span className={`stat-note ${noteTone ? `txt-${noteTone}` : ''}`}>{note}</span>}
    </div>
  )
}

export function Modal({ title, onClose, children, footer, width }) {
  const ref = useRef(null)
  useEffect(() => {
    const onKey = (e) => e.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKey)
    ref.current?.querySelector('input, select, textarea, button')?.focus()
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])
  return (
    <div className="modal-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={title} ref={ref} style={width ? { maxWidth: width } : undefined}>
        <div className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Đóng"><Icon name="x" /></button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-foot">{footer}</div>}
      </div>
    </div>
  )
}

// Trang 1, trang cuối và 2 trang quanh trang hiện tại; chỗ bị lược hiện "…"
const pageWindow = (page, pages) => {
  const set = new Set([1, pages, page - 2, page - 1, page, page + 1, page + 2])
  const list = [...set].filter((p) => p >= 1 && p <= pages).sort((a, b) => a - b)
  return list.flatMap((p, i) => (i > 0 && p - list[i - 1] > 1 ? [`gap-${p}`, p] : [p]))
}

export function Pagination({ page, total, pageSize, onChange, label = 'bản ghi' }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(total, page * pageSize)
  return (
    <div className="table-foot">
      <span className="muted" style={{ fontSize: 13 }}>Hiển thị {from.toLocaleString('vi-VN')}–{to.toLocaleString('vi-VN')} trên {total.toLocaleString('vi-VN')} {label}</span>
      <div className="pager">
        <button className="pg" disabled={page <= 1} onClick={() => onChange(page - 1)} aria-label="Trang trước"><Icon name="chevronL" size={16} /></button>
        {pageWindow(page, pages).map((p) => typeof p === 'string'
          ? <span key={p} className="muted" aria-hidden="true" style={{ padding: '0 4px' }}>…</span>
          : <button key={p} className={`pg ${p === page ? 'active' : ''}`} aria-current={p === page ? 'page' : undefined} onClick={() => onChange(p)}>{p}</button>
        )}
        <button className="pg" disabled={page >= pages} onClick={() => onChange(page + 1)} aria-label="Trang sau"><Icon name="chevronR" size={16} /></button>
      </div>
    </div>
  )
}

export function Empty({ icon = 'search', title, text, action }) {
  return (
    <div className="empty">
      <Icon name={icon} size={32} />
      <b style={{ color: 'var(--text)' }}>{title}</b>
      {text && <span>{text}</span>}
      {action}
    </div>
  )
}

export function Field({ label, required, hint, error, children, full }) {
  return (
    <label className={`field ${full ? 'full' : ''}`}>
      <span>{label}{required && <span className="req"> *</span>}</span>
      {children}
      {error ? <span className="err">{error}</span> : hint ? <span className="hint">{hint}</span> : null}
    </label>
  )
}

export function Tabs({ items, value, onChange, label }) {
  return (
    <div className="tabs" role="tablist" aria-label={label}>
      {items.map((t) => (
        <button key={t.key} role="tab" aria-selected={value === t.key} className={`tab ${value === t.key ? 'active' : ''}`} onClick={() => onChange(t.key)}>
          {t.label}{t.count != null && <span className="count">{t.count}</span>}
        </button>
      ))}
    </div>
  )
}

const DOC_TONE = { PDF: 'rej', XLS: 'ok', DOC: 'info', IMG: 'wait', FILE: 'draft' }
export const DocIcon = ({ kind }) => <span className={`doc-ic tone-${DOC_TONE[kind] || 'draft'}`}>{kind}</span>
