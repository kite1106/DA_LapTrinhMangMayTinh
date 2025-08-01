// Real-time Security Monitor
// Quản lý tất cả kết nối SignalR và real-time notifications

class RealTimeMonitor {
    constructor() {
        this.alertConnection = null;
        this.accountConnection = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.recentAlerts = new Set(); // Để tránh trùng lặp
        this.alertElements = new Map(); // Lưu reference đến alert elements
        
        this.init();
    }

    init() {
        this.initializeConnections();
        this.setupEventListeners();
        this.setupToastrConfig();
    }

    initializeConnections() {
        // Alert Hub connection
        this.alertConnection = new signalR.HubConnectionBuilder()
            .withUrl("/alertHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Account Hub connection
        this.accountConnection = new signalR.HubConnectionBuilder()
            .withUrl("/accountHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupAlertHandlers();
        this.setupAccountHandlers();
        this.startConnections();
    }

    setupAlertHandlers() {
        // Handle login alerts
        this.alertConnection.on("ReceiveLoginAlert", (alert) => {
            console.log('Received login alert:', alert);
            this.handleLoginAlert(alert);
        });

        // Handle general alerts
        this.alertConnection.on("ReceiveAlert", (alert) => {
            console.log('Received general alert:', alert);
            this.handleGeneralAlert(alert);
        });

        // Connection events
        this.alertConnection.onreconnecting((error) => {
            console.log('AlertHub reconnecting:', error);
            this.showConnectionStatus('Đang kết nối lại...', 'warning');
        });

        this.alertConnection.onreconnected((connectionId) => {
            console.log('AlertHub reconnected:', connectionId);
            this.showConnectionStatus('Đã kết nối lại', 'success');
            this.reconnectAttempts = 0;
        });

        this.alertConnection.onclose((error) => {
            console.log('AlertHub connection closed:', error);
            this.showConnectionStatus('Mất kết nối', 'error');
        });
    }

    setupAccountHandlers() {
        // User status updates
        this.accountConnection.on("UserStatusUpdated", (userName, isLocked, userId) => {
            console.log('User status updated:', { userName, isLocked, userId });
            this.handleUserStatusUpdate(userName, isLocked, userId);
        });

        // User restrictions
        this.accountConnection.on("UserRestricted", (userName, reason, userId) => {
            console.log('User restricted:', { userName, reason, userId });
            this.handleUserRestriction(userName, reason, userId);
        });

        // User unrestrictions
        this.accountConnection.on("UserUnrestricted", (userName, userId) => {
            console.log('User unrestricted:', { userName, userId });
            this.handleUserUnrestriction(userName, userId);
        });

        // Connection events
        this.accountConnection.onreconnecting((error) => {
            console.log('AccountHub reconnecting:', error);
        });

        this.accountConnection.onreconnected((connectionId) => {
            console.log('AccountHub reconnected:', connectionId);
        });

        this.accountConnection.onclose((error) => {
            console.log('AccountHub connection closed:', error);
        });
    }

    async startConnections() {
        try {
            await Promise.all([
                this.alertConnection.start(),
                this.accountConnection.start()
            ]);
            
            this.isConnected = true;
            this.reconnectAttempts = 0;
            console.log('SignalR connections established successfully');
            this.showConnectionStatus('Đã kết nối', 'success');
        } catch (error) {
            console.error('Failed to start SignalR connections:', error);
            this.showConnectionStatus('Lỗi kết nối', 'error');
            this.handleConnectionError(error);
        }
    }

    // Tạo key duy nhất cho alert để tránh trùng lặp
    createAlertKey(alert) {
        const sourceIp = alert.sourceIp || '';
        const type = alert.type || '';
        const severityLevel = alert.severityLevel || '';
        
        return `${sourceIp}-${type}-${severityLevel}`;
    }

    // Kiểm tra xem alert đã tồn tại chưa và highlight nếu cần
    isDuplicateAlert(alert) {
        const key = this.createAlertKey(alert);
        if (this.recentAlerts.has(key)) {
            // Nếu đã tồn tại, highlight element cũ
            const existingElement = this.alertElements.get(key);
            if (existingElement) {
                this.highlightElement(existingElement);
            }
            return true;
        }
        
        // Thêm vào danh sách đã hiển thị và xóa sau 10 giây
        this.recentAlerts.add(key);
        setTimeout(() => {
            this.recentAlerts.delete(key);
            this.alertElements.delete(key);
        }, 10000);
        
        return false;
    }

    // Highlight effect cho element
    highlightElement(element) {
        if (!element) return;
        
        // Highlight effect
        element.style.backgroundColor = '#fff3cd';
        element.style.border = '2px solid #ffc107';
        element.style.transform = 'scale(1.02)';
        element.style.transition = 'all 0.3s ease';
        
        // Reset sau 2 giây
        setTimeout(() => {
            element.style.backgroundColor = '';
            element.style.border = '';
            element.style.transform = '';
        }, 2000);
    }

    handleLoginAlert(alert) {
        // Kiểm tra trùng lặp
        if (this.isDuplicateAlert(alert)) {
            console.log('Duplicate alert ignored:', alert.title);
            return;
        }

        // Play alert sound
        this.playAlertSound();

        // Show toast notification với timeout 5 giây
        const key = this.createAlertKey(alert);
        const toastElement = toastr.warning(alert.description, alert.title, {
            timeOut: 5000, // Tự động ẩn sau 5 giây
            extendedTimeOut: 2000,
            closeButton: true,
            tapToDismiss: true,
            newestOnTop: true,
            preventDuplicates: true
        });

        // Lưu reference đến element
        if (toastElement) {
            this.alertElements.set(key, toastElement);
        }

        // Update alerts table if on alerts page
        this.updateAlertsTable();

        // Update dashboard counters
        this.updateAlertCounters();
    }

    handleGeneralAlert(alert) {
        // Kiểm tra trùng lặp
        if (this.isDuplicateAlert(alert)) {
            console.log('Duplicate alert ignored:', alert.title);
            return;
        }

        // Play alert sound
        this.playAlertSound();

        // Show toast notification với timeout 5 giây
        const message = alert.description || alert.message || 'Cảnh báo mới';
        const title = alert.title || 'Thông báo bảo mật';
        const key = this.createAlertKey(alert);
        
        const toastElement = toastr.warning(message, title, {
            timeOut: 5000, // Tự động ẩn sau 5 giây
            extendedTimeOut: 2000,
            closeButton: true,
            tapToDismiss: true,
            newestOnTop: true,
            preventDuplicates: true
        });

        // Lưu reference đến element
        if (toastElement) {
            this.alertElements.set(key, toastElement);
        }

        // Update alerts table if on alerts page
        this.updateAlertsTable();

        // Update dashboard counters
        this.updateAlertCounters();
    }

    handleUserStatusUpdate(userName, isLocked, userId) {
        const currentUser = this.getCurrentUserName();
        if (!currentUser || userName !== currentUser) return;

        if (isLocked) {
            toastr.error('Tài khoản của bạn đã bị khóa. Bạn sẽ được đăng xuất sau 3 giây.', 'Thông báo', {
                timeOut: 0,
                extendedTimeOut: 0,
                closeButton: true
            });
            setTimeout(() => {
                this.logoutUser();
            }, 3000);
        } else {
            toastr.success('Tài khoản của bạn đã được mở khóa.', 'Thông báo', {
                timeOut: 5000,
                extendedTimeOut: 2000
            });
        }
    }

    handleUserRestriction(userName, reason, userId) {
        const currentUser = this.getCurrentUserName();
        if (!currentUser || userName !== currentUser) return;

        toastr.warning(`Tài khoản của bạn đã bị hạn chế. Lý do: ${reason}. Bạn sẽ được chuyển về trang User Dashboard.`, 'Thông báo', {
            timeOut: 5000,
            extendedTimeOut: 2000
        });
        setTimeout(() => {
            window.location.href = '/User/Index';
        }, 3000);
    }

    handleUserUnrestriction(userName, userId) {
        const currentUser = this.getCurrentUserName();
        if (!currentUser || userName !== currentUser) return;

        toastr.success('Tài khoản của bạn đã được bỏ hạn chế', 'Thông báo', {
            timeOut: 5000,
            extendedTimeOut: 2000
        });
    }

    playAlertSound() {
        try {
            const audio = new Audio('/sounds/alert.mp3');
            audio.play().catch(e => console.log('Error playing sound:', e));
        } catch (error) {
            console.log('Could not play alert sound:', error);
        }
    }

    updateAlertsTable() {
        if (window.location.pathname.includes('/alerts')) {
            // SignalR will handle real-time updates instead of manual reload
            console.log('Alerts table update triggered, SignalR will handle real-time updates');
        }
    }

    updateAlertCounters() {
        // Update alert count badges
        const badges = document.querySelectorAll('.alert-count-badge');
        badges.forEach(badge => {
            const currentCount = parseInt(badge.textContent || '0');
            badge.textContent = currentCount + 1;
        });

        // Update dashboard counters via AJAX
        $.get('/api/alerts/counts', function(data) {
            $('.critical-count').text(data.critical || 0);
            $('.high-count').text(data.high || 0);
            $('.medium-count').text(data.medium || 0);
            $('.low-count').text(data.low || 0);
            $('.inprogress-count').text(data.inProgress || 0);
            $('.resolved-count').text(data.resolved || 0);
        }).catch(error => {
            console.log('Error updating alert counts:', error);
        });
    }

    getCurrentUserName() {
        return window.currentUserName || 
               (typeof USER_NAME !== 'undefined' ? USER_NAME : null) ||
               (typeof CURRENT_USER !== 'undefined' ? CURRENT_USER : null);
    }

    async logoutUser() {
        try {
            await $.post('/Login/Logout', {});
            window.location.href = '/Login/Index';
        } catch (error) {
            console.error('Error during logout:', error);
            window.location.href = '/Login/Index';
        }
    }

    showConnectionStatus(message, type) {
        const statusElement = document.getElementById('connectionStatus');
        if (statusElement) {
            statusElement.textContent = message;
            statusElement.className = `text-${type === 'success' ? 'success' : type === 'warning' ? 'warning' : 'danger'}`;
        }

        // Show toast for connection issues
        if (type === 'error') {
            toastr.error('Mất kết nối với server real-time', 'Lỗi kết nối', {
                timeOut: 5000,
                extendedTimeOut: 2000
            });
        } else if (type === 'success' && this.reconnectAttempts > 0) {
            toastr.success('Đã kết nối lại thành công', 'Kết nối', {
                timeOut: 5000,
                extendedTimeOut: 2000
            });
        }
    }

    handleConnectionError(error) {
        this.reconnectAttempts++;
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            setTimeout(() => {
                this.startConnections();
            }, 5000 * this.reconnectAttempts);
        } else {
            toastr.error('Không thể kết nối với server. Vui lòng tải lại trang.', 'Lỗi kết nối', {
                timeOut: 0,
                extendedTimeOut: 0
            });
        }
    }

    setupEventListeners() {
        // Global AJAX error handler
        $(document).ajaxError((event, jqXHR, settings, error) => {
            console.error('AJAX Error:', error);
            toastr.error('Có lỗi xảy ra khi thực hiện yêu cầu', 'Lỗi', {
                timeOut: 5000,
                extendedTimeOut: 2000
            });
        });

        // Page visibility change
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && !this.isConnected) {
                this.startConnections();
            }
        });
    }

    setupToastrConfig() {
        toastr.options = {
            closeButton: true,
            progressBar: true,
            positionClass: "toast-top-right",
            timeOut: 5000, // Mặc định 5 giây
            extendedTimeOut: 2000,
            newestOnTop: true,
            preventDuplicates: true,
            tapToDismiss: true
        };
    }

    // Public methods for external use
    getAlertConnection() {
        return this.alertConnection;
    }

    getAccountConnection() {
        return this.accountConnection;
    }

    isConnected() {
        return this.isConnected;
    }
}

// Initialize when document is ready
$(document).ready(function() {
    // Initialize real-time monitor
    window.realTimeMonitor = new RealTimeMonitor();
    
    // Make it globally available
    window.RealTimeMonitor = RealTimeMonitor;
});

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = RealTimeMonitor;
} 