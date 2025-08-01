# 🚨 Hướng Dẫn Test Hệ Thống Thông Báo Cảnh Báo

## ✅ Đã Hoàn Thành

### 1. Hệ Thống Thông Báo Cảnh Báo
- ✅ **CSS**: `wwwroot/css/alert-notifications.css`
- ✅ **JavaScript**: `wwwroot/js/alert-notifications.js`
- ✅ **Layout**: Đã include files vào `_Layout.cshtml`
- ✅ **Middleware**: `RestrictedUserMiddleware.cs` - Tự động logout khi bị block
- ✅ **SignalR**: `AccountHub.cs` - Gửi thông báo real-time

### 2. Tính Năng Đã Cài Đặt
- ✅ **Thông báo ở góc trái**: Không ảnh hưởng đến toastr
- ✅ **Tự động logout**: Khi user bị block
- ✅ **Thông báo real-time**: Via SignalR
- ✅ **Màu sắc khác nhau**: Theo mức độ cảnh báo
- ✅ **Âm thanh**: Phát âm thanh khi có cảnh báo

## 🧪 Cách Test

### Bước 1: Chạy Ứng Dụng
```bash
dotnet run
```

### Bước 2: Test Block User
1. **Đăng nhập Admin**:
   - Email: `admin@example.com`
   - Password: `Admin123!`

2. **Vào trang User Management**:
   - URL: `/UserAdmin/Index`

3. **Block một user**:
   - Click nút "Lock" bên cạnh user
   - Chọn số ngày block (mặc định 7 ngày)

### Bước 3: Test Restrict User
1. **Restrict một user**:
   - Click nút "Restrict" bên cạnh user
   - User sẽ bị hạn chế quyền truy cập

### Bước 4: Test Thông Báo
1. **Đăng nhập bằng user bị block/restrict**:
   - User sẽ thấy thông báo ở góc trái
   - Nếu bị block: Tự động logout sau 3 giây
   - Nếu bị restrict: Chuyển về User Dashboard với thông báo

## 🎨 Giao Diện Thông Báo

### Vị Trí
- **Góc trái trên**: Không ảnh hưởng đến toastr
- **Animation**: Slide từ trái vào
- **Auto-hide**: Tùy theo mức độ cảnh báo

### Màu Sắc
- **Critical**: Đỏ đậm (#ff4757)
- **High**: Đỏ nhạt (#ff6b6b)
- **Medium**: Cam (#ffa502)
- **Low**: Xanh (#2ed573)

### Âm Thanh
- **Critical**: Volume 80%
- **High**: Volume 50%
- **Medium**: Volume 50%
- **Low**: Volume 50%

## 🔧 Cấu Hình

### CSS Classes
```css
.alert-notification-container  /* Container chính */
.alert-notification           /* Thông báo đơn lẻ */
.alert-notification.critical  /* Mức độ nghiêm trọng */
.alert-notification.high      /* Mức độ cao */
.alert-notification.medium    /* Mức độ trung bình */
.alert-notification.low       /* Mức độ thấp */
```

### JavaScript API
```javascript
// Hiển thị thông báo
window.showAlertNotification(alert);

// Xóa tất cả thông báo
window.alertNotificationManager.clearAll();
```

## 📱 Responsive
- **Desktop**: Góc trái, max-width 400px
- **Mobile**: Toàn bộ chiều rộng, padding 10px

## 🎯 Kết Quả Mong Đợi

### Khi Admin Block User:
1. ✅ User nhận thông báo real-time
2. ✅ Thông báo hiển thị ở góc trái
3. ✅ Tự động logout sau 3 giây
4. ✅ Chuyển về trang login với thông báo

### Khi Admin Restrict User:
1. ✅ User nhận thông báo real-time
2. ✅ Thông báo hiển thị ở góc trái
3. ✅ Chuyển về User Dashboard với thông báo
4. ✅ Bị hạn chế quyền truy cập

### Khi Có Cảnh Báo Mới:
1. ✅ Thông báo hiển thị ở góc trái
2. ✅ Âm thanh phát ra
3. ✅ Màu sắc theo mức độ
4. ✅ Tự động ẩn sau thời gian

## 🐛 Troubleshooting

### Nếu thông báo không hiển thị:
1. Kiểm tra Console (F12) có lỗi JavaScript không
2. Kiểm tra SignalR connection
3. Kiểm tra file CSS/JS đã load chưa

### Nếu không tự động logout:
1. Kiểm tra middleware đã register chưa
2. Kiểm tra SignInManager injection
3. Kiểm tra database connection

### Nếu không có âm thanh:
1. Kiểm tra file `/sounds/alert.mp3` có tồn tại không
2. Kiểm tra browser có cho phép autoplay không 