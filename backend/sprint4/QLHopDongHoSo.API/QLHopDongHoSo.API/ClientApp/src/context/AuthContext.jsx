import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { PERMISSIONS, roleFromApi } from '../data/constants'
import { api, setUnauthorizedHandler, tokenStore } from '../services/api'

const AuthContext = createContext(null)
const KEY = 'qlhd.session'

const load = () => {
  try {
    const user = JSON.parse(sessionStorage.getItem(KEY))
    return user && tokenStore.get() ? user : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(load)

  const clear = useCallback(() => {
    tokenStore.clear()
    try { sessionStorage.removeItem(KEY) } catch { /* bỏ qua */ }
    setUser(null)
  }, [])

  // Token hết hạn (API trả 401) => về trang đăng nhập
  useEffect(() => { setUnauthorizedHandler(clear) }, [clear])

  // Đăng nhập qua API: nhận JWT + thông tin người dùng (vai trò, chi nhánh)
  const login = async (username, password) => {
    try {
      const r = await api.login(username.trim(), password)
      const u = {
        id: String(r.userId),
        username: r.username,
        name: r.fullName,
        role: roleFromApi(r.role),
        branch: r.branchName || '—',
        branchId: r.branchId ?? null
      }
      tokenStore.set(r.token)
      try { sessionStorage.setItem(KEY, JSON.stringify(u)) } catch { /* bỏ qua */ }
      setUser(u)
      return { ok: true }
    } catch (e) {
      return { ok: false, message: e.status === 401 ? 'Tên đăng nhập hoặc mật khẩu không đúng.' : e.message }
    }
  }

  const logout = async () => {
    try { await api.logout() } catch { /* vẫn đăng xuất phía giao diện */ }
    clear()
  }

  const can = (perm) => !!user && !!PERMISSIONS.find((p) => p.key === perm)?.roles.includes(user.role)

  // Thao tác ghi chỉ trong chi nhánh của mình; Admin không giới hạn (khớp backend)
  const canWriteBranch = (branchId) => !!user && (user.role === 'ADMIN' || (user.branchId != null && user.branchId === Number(branchId)))

  return <AuthContext.Provider value={{ user, login, logout, can, canWriteBranch }}>{children}</AuthContext.Provider>
}

export const useAuth = () => useContext(AuthContext)
