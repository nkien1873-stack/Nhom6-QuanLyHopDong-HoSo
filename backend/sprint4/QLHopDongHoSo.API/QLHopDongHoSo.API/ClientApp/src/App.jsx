import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import Layout from './components/Layout'
import { Empty } from './components/UI'
import { useAuth } from './context/AuthContext'
import { useData } from './context/DataContext'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import HoSoList from './pages/HoSoList'
import HoSoDetail from './pages/HoSoDetail'
import HoSoForm from './pages/HoSoForm'
import KhachHang from './pages/KhachHang'
import PheDuyet from './pages/PheDuyet'
import NgoaiLe from './pages/NgoaiLe'
import TaiLieu from './pages/TaiLieu'
import BaoCao from './pages/BaoCao'
import NguoiDung from './pages/NguoiDung'
import NhatKy from './pages/NhatKy'
import NotFound from './pages/NotFound'

// Chặn trang khi chưa đăng nhập / không đủ quyền (auth guard)
function Guard({ perm, children }) {
  const { user, can } = useAuth()
  const loc = useLocation()
  if (!user) return <Navigate to="/dang-nhap" replace state={{ from: loc.pathname }} />
  if (perm && !can(perm)) return <NotFound forbidden />
  return children
}

// Chờ tải dữ liệu lần đầu sau khi đăng nhập
function Loaded({ children }) {
  const { loaded, error, reload } = useData()
  if (!loaded) return <div className="card"><Empty icon="refresh" title="Đang tải dữ liệu…" text="Đang lấy hợp đồng, khách hàng và tài liệu từ máy chủ." /></div>
  if (error) return <div className="card"><Empty icon="alert" title="Không tải được dữ liệu" text={error} action={<button className="btn btn-ghost" onClick={reload}>Thử lại</button>} /></div>
  return children
}

const page = (el, perm) => <Guard perm={perm}><Loaded>{el}</Loaded></Guard>

export default function App() {
  return (
    <Routes>
      <Route path="/dang-nhap" element={<Login />} />
      <Route element={<Guard><Layout /></Guard>}>
        <Route index element={page(<Dashboard />)} />
        <Route path="ho-so" element={page(<HoSoList />)} />
        <Route path="ho-so/tao-moi" element={page(<HoSoForm />, 'hoso.create')} />
        <Route path="ho-so/:id" element={page(<HoSoDetail />)} />
        <Route path="ho-so/:id/sua" element={page(<HoSoForm />, 'hoso.create')} />
        <Route path="khach-hang" element={page(<KhachHang />)} />
        <Route path="phe-duyet" element={page(<PheDuyet />, 'hoso.approve')} />
          <Route path="ngoai-le" element={page(<NgoaiLe />, 'deferral.view')} />
        <Route path="tai-lieu" element={page(<TaiLieu />)} />
        <Route path="bao-cao" element={page(<BaoCao />, 'report.view')} />
        <Route path="nguoi-dung" element={page(<NguoiDung />, 'user.manage')} />
        <Route path="nhat-ky" element={page(<NhatKy />, 'audit.view')} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}
