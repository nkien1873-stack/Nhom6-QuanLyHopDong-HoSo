import { Link } from 'react-router-dom'
import { Empty } from '../components/UI'

export default function NotFound({ forbidden }) {
  return (
    <div className="card">
      {forbidden
        ? <Empty icon="lock" title="Bạn không có quyền truy cập trang này" text="Liên hệ quản trị viên nếu bạn cần được cấp quyền." action={<Link className="btn btn-ghost" to="/">Về trang tổng quan</Link>} />
        : <Empty icon="search" title="Không tìm thấy trang (404)" text="Đường dẫn không tồn tại hoặc đã bị thay đổi." action={<Link className="btn btn-ghost" to="/">Về trang tổng quan</Link>} />}
    </div>
  )
}
