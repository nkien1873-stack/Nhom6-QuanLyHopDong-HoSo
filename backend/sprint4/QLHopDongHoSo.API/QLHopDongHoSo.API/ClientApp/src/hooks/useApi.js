import { useCallback, useEffect, useRef, useState } from 'react'
import { useData } from '../context/DataContext'

/**
 * Gọi API khi `deps` đổi hoặc khi có thao tác ghi (version trong DataContext tăng).
 * Giữ dữ liệu cũ trong lúc tải trang mới để bảng không bị nháy.
 * Trả { data, loading, error, reload }.
 */
export function useApi(fn, deps) {
  const { version } = useData()
  const [state, setState] = useState({ data: null, loading: true, error: '' })
  const [tick, setTick] = useState(0)
  const fnRef = useRef(fn)
  fnRef.current = fn

  useEffect(() => {
    let alive = true
    setState((s) => ({ ...s, loading: true }))
    fnRef.current()
      .then((data) => alive && setState({ data, loading: false, error: '' }))
      .catch((e) => alive && setState((s) => ({ ...s, loading: false, error: e.message })))
    return () => { alive = false }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version, tick])

  const reload = useCallback(() => setTick((t) => t + 1), [])
  return { ...state, reload }
}

/** Từ khóa gửi lên server: cần ít nhất 2 ký tự (1 ký tự khớp gần như mọi dòng, server từ chối) */
export const searchTerm = (q) => (q.trim().length >= 2 ? q.trim() : '')

/** Giá trị trễ `ms` (ô tìm kiếm: chỉ gọi API khi người dùng ngừng gõ) */
export function useDebounced(value, ms = 300) {
  const [v, setV] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms)
    return () => clearTimeout(t)
  }, [value, ms])
  return v
}
