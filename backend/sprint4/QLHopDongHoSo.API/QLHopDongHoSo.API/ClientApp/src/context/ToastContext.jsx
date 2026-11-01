import { createContext, useCallback, useContext, useState } from 'react'
import Icon from '../components/Icon'

const ToastContext = createContext(() => {})

export function ToastProvider({ children }) {
  const [items, setItems] = useState([])
  const toast = useCallback((message, type = 'success') => {
    const id = Math.random().toString(36).slice(2)
    setItems((s) => [...s, { id, message, type }])
    // Lỗi/cảnh báo nghiệp vụ (ERR_.../WARN_...) dài hơn => hiện lâu hơn để kịp đọc
    setTimeout(() => setItems((s) => s.filter((t) => t.id !== id)), type === 'success' ? 3200 : 7000)
  }, [])
  return (
    <ToastContext.Provider value={toast}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {items.map((t) => (
          <div key={t.id} className={`toast ${t.type === 'error' || t.type === 'warn' ? t.type : ''}`}>
            <Icon name={t.type === 'success' ? 'check' : 'alert'} size={18} />
            {t.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export const useToast = () => useContext(ToastContext)
