// Initialize SignalR connections
const initializeSignalR = () => {
    // Account Hub connection
    const accountConnection = new signalR.HubConnectionBuilder()
        .withUrl("/accountHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Alert Hub connection
    const alertConnection = new signalR.HubConnectionBuilder()
        .withUrl("/alertHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Handle login alerts
    alertConnection.on("ReceiveLoginAlert", (alert) => {
        // Play alert sound
        const audio = new Audio('/sounds/alert.mp3');
        audio.play().catch(e => console.log('Error playing sound:', e));

        // Show alert notification
        toastr.warning(alert.description, alert.title, {
            timeOut: 0,  // Không tự động ẩn
            extendedTimeOut: 0,
            closeButton: true,
            tapToDismiss: false
        });

        // If on alerts page, SignalR will handle real-time updates
        if (window.location.pathname.includes('/alerts')) {
            console.log('Alert received, SignalR will handle real-time updates');
        }
    });

    // Handle general alerts
    alertConnection.on("ReceiveAlert", (alert) => {
        // Play alert sound
        const audio = new Audio('/sounds/alert.mp3');
        audio.play().catch(e => console.log('Error playing sound:', e));

        // Show alert notification
        toastr.warning(alert.description || alert.message, alert.title, {
            timeOut: 0,
            extendedTimeOut: 0,
            closeButton: true,
            tapToDismiss: false
        });

        // Update dashboard if on alerts page - SignalR will handle real-time updates
        if (window.location.pathname.includes('/alerts')) {
            console.log('General alert received, SignalR will handle real-time updates');
        }
    });

    // Lắng nghe sự kiện cập nhật trạng thái user
    accountConnection.on("UserStatusUpdated", (userName, isLocked, userId) => {
        const currentUser = window.currentUserName || (typeof USER_NAME !== 'undefined' ? USER_NAME : null);
        if (!currentUser || userName !== currentUser) return;
        
        if (isLocked) {
            toastr.error('Tài khoản của bạn đã bị khóa. Bạn sẽ được đăng xuất sau 3 giây.', 'Thông báo');
            setTimeout(() => {
                $.post('/Login/Logout', {}, function() {
                    window.location.href = '/Login/Index';
                });
            }, 3000);
        }
    });

    // Lắng nghe sự kiện tài khoản bị hạn chế
    accountConnection.on("UserRestricted", (userName, reason, userId) => {
        const currentUser = window.currentUserName || (typeof USER_NAME !== 'undefined' ? USER_NAME : null);
        if (!currentUser || userName !== currentUser) return;
        
        toastr.warning(`Tài khoản của bạn đã bị hạn chế. Lý do: ${reason}`, 'Thông báo');
        setTimeout(() => {
            window.location.href = '/Alerts/Index';
        }, 3000);
    });

    // Lắng nghe sự kiện tài khoản được bỏ hạn chế
    accountConnection.on("UserUnrestricted", (userName, userId) => {
        const currentUser = window.currentUserName || (typeof USER_NAME !== 'undefined' ? USER_NAME : null);
        if (!currentUser || userName !== currentUser) return;
        
        toastr.success('Tài khoản của bạn đã được bỏ hạn chế', 'Thông báo');
    });

    // Start both connections
    Promise.all([
        accountConnection.start(),
        alertConnection.start()
    ]).then(() => {
        console.log('SignalR connections established successfully');
    }).catch(err => {
        console.error('SignalR Connection Error:', err);
        toastr.error('Không thể kết nối với server real-time', 'Lỗi kết nối');
    });

    return { accountConnection, alertConnection };
};

// Global AJAX error handler
$(document).ajaxError(function(event, jqXHR, settings, error) {
    console.error('AJAX Error:', error);
    toastr.error('Có lỗi xảy ra khi thực hiện yêu cầu', 'Lỗi');
});

// Theme and UI utilities
const ThemeUtils = {
    getSeverityClass: (severity) => {
        switch (severity.toLowerCase()) {
            case 'critical': return 'danger';
            case 'high': return 'warning';
            case 'medium': return 'info';
            case 'low': return 'success';
            default: return 'secondary';
        }
    },

    getStatusClass: (status) => {
        switch (status.toLowerCase()) {
            case 'new': return 'info';
            case 'in progress': return 'primary';
            case 'resolved': return 'success';
            case 'false positive': return 'secondary';
            case 'ignored': return 'secondary';
            default: return 'secondary';
        }
    },

    formatDateTime: (date) => {
        return new Date(date).toLocaleString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }
};

// DataTables language configuration
const DataTableConfig = {
    language: {
        url: '//cdn.datatables.net/plug-ins/1.10.24/i18n/Vietnamese.json'
    },
    pageLength: 25,
    responsive: true
};

// Initialize tooltips and popovers
const initializeBootstrapComponents = () => {
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl));

    const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(popoverTriggerEl => new bootstrap.Popover(popoverTriggerEl));
};

// Document ready handler
$(document).ready(function() {
    initializeBootstrapComponents();
    
    // Initialize SignalR if the connection object exists
    if (typeof signalR !== 'undefined') {
        const connection = initializeSignalR();
    }

    // Initialize all DataTables with default config
    $('.datatable').DataTable(DataTableConfig);
});
