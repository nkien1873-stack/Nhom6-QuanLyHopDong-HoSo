const vnd = new Intl.NumberFormat('vi-VN')

export const money = (n) => (n == null ? '—' : `${vnd.format(n)} ₫`)
export const moneyNum = (n) => (n == null ? '—' : vnd.format(n))

/** 486300000000 → "486,3 tỷ" */
export const moneyShort = (n) => {
  if (n == null) return '—'
  if (n >= 1e9) return `${(n / 1e9).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tỷ`
  if (n >= 1e6) return `${(n / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 0 })} triệu`
  return vnd.format(n)
}

export const date = (iso) => (iso ? new Date(iso).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '—')
export const dateTime = (iso) =>
  iso ? new Date(iso).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—'

export const fromNow = (iso) => {
  if (!iso) return '—'
  const diff = (Date.now() - new Date(iso).getTime()) / 1000
  if (diff < 60) return 'vừa xong'
  if (diff < 3600) return `${Math.floor(diff / 60)} phút trước`
  if (diff < 86400) return `${Math.floor(diff / 3600)} giờ trước`
  if (diff < 172800) return 'hôm qua'
  return date(iso)
}

export const daysUntil = (iso) => (iso ? Math.ceil((new Date(iso).getTime() - Date.now()) / 86400000) : null)

export const fileSize = (b) => {
  if (b < 1024) return `${b} B`
  if (b < 1048576) return `${Math.round(b / 1024)} KB`
  return `${(b / 1048576).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`
}

export const initials = (name = '') => {
  const p = name.trim().split(/\s+/)
  return ((p[0]?.[0] || '') + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase()
}

/** Bỏ dấu tiếng Việt để tìm kiếm không phân biệt dấu */
export const norm = (s = '') => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase()

export const uid = (p = 'id') => `${p}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`

const AVATAR_TONES = [['#DCEFEE', '#0B5F55'], ['#E6F0FA', '#1D4F86'], ['#FEF0C7', '#8A4B06'], ['#EDE7FB', '#5B21B6'], ['#FDE7EF', '#9D174D']]
export const avatarTone = (seed = '') => AVATAR_TONES[[...seed].reduce((a, c) => a + c.charCodeAt(0), 0) % AVATAR_TONES.length]

/** Xuất mảng object ra CSV (mở được bằng Excel, có BOM UTF-8) */
export const downloadCsv = (filename, rows) => {
  if (!rows.length) return
  const cols = Object.keys(rows[0])
  const esc = (v) => `"${String(v ?? '').replace(/"/g, '""')}"`
  const csv = '﻿' + [cols.map(esc).join(','), ...rows.map((r) => cols.map((c) => esc(r[c])).join(','))].join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}
