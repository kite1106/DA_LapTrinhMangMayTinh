// Alert Notification System
class AlertNotificationManager {
    constructor() {
        this.container = null;
        this.notifications = [];
        this.maxNotifications = 3; // Giảm số lượng thông báo tối đa
        this.init();
    }

    init() {
        if (!document.getElementById('alert-notification-container')) {
            this.container = document.createElement('div');
            this.container.id = 'alert-notification-container';
            this.container.className = 'alert-notification-container';
            document.body.appendChild(this.container);
        } else {
            this.container = document.getElementById('alert-notification-container');
        }
    }

    showAlert(alert) {
        // Kiểm tra xem alert này đã hiển thị chưa để tránh duplicate
        const alertKey = `${alert.id || alert.title}-${alert.sourceIp}-${new Date().getTime()}`;
        
        // Kiểm tra duplicate trong 5 giây gần nhất
        const recentAlerts = this.notifications.filter(n => {
            const timeDiff = Date.now() - n.dataset.timestamp;
            return timeDiff < 5000; // 5 giây
        });
        
        const isDuplicate = recentAlerts.some(n => 
            n.dataset.alertTitle === alert.title && 
            n.dataset.alertIp === alert.sourceIp
        );
        
        if (isDuplicate) {
            console.log('Alert already shown, skipping duplicate:', alert.title);
            return;
        }

        const notification = this.createNotification(alert);
        notification.dataset.alertTitle = alert.title;
        notification.dataset.alertIp = alert.sourceIp;
        notification.dataset.timestamp = Date.now();
        
        this.container.appendChild(notification);
        this.notifications.push(notification);

        setTimeout(() => {
            notification.classList.add('show');
        }, 100);

        const autoHideTime = this.getAutoHideTime(alert.severityLevel);
        if (autoHideTime > 0) {
            setTimeout(() => {
                this.hideNotification(notification);
            }, autoHideTime);
        }

        if (this.notifications.length > this.maxNotifications) {
            const oldestNotification = this.notifications.shift();
            this.hideNotification(oldestNotification);
        }

        this.playAlertSound(alert.severityLevel);
    }

    createNotification(alert) {
        const notification = document.createElement('div');
        notification.className = `alert-notification ${alert.severityLevel?.toLowerCase() || 'medium'}`;
        
        const icon = this.getAlertIcon(alert.alertType);
        const severity = this.getSeverityText(alert.severityLevel);
        const time = new Date().toLocaleTimeString('vi-VN');

        notification.innerHTML = `
            <div class="alert-notification-header">
                <div class="alert-notification-title">
                    <i class="fas ${icon} alert-notification-icon"></i>
                    <span>${alert.title}</span>
                </div>
                <button class="alert-notification-close" onclick="alertNotificationManager.hideNotification(this.parentElement.parentElement)">
                    <i class="fas fa-times"></i>
                </button>
            </div>
            <div class="alert-notification-content">
                <div><strong>Loại:</strong> ${alert.alertType}</div>
                <div><strong>Mức độ:</strong> ${severity}</div>
                <div><strong>IP:</strong> ${alert.sourceIp}</div>
                ${alert.description ? `<div><strong>Mô tả:</strong> ${alert.description}</div>` : ''}
            </div>
            <div class="alert-notification-time">
                ${time}
            </div>
        `;

        return notification;
    }

    hideNotification(notification) {
        if (!notification) return;
        
        notification.classList.add('hide');
        setTimeout(() => {
            if (notification.parentElement) {
                notification.parentElement.removeChild(notification);
            }
            const index = this.notifications.indexOf(notification);
            if (index > -1) {
                this.notifications.splice(index, 1);
            }
        }, 300);
    }

    getAlertIcon(alertType) {
        const iconMap = {
            'SuspiciousIP': 'fa-exclamation-triangle',
            'DDoS': 'fa-network-wired',
            'BruteForce': 'fa-user-shield',
            'SQLInjection': 'fa-database',
            'Malware': 'fa-virus',
            'Unknown': 'fa-question-circle'
        };
        return iconMap[alertType] || 'fa-exclamation-circle';
    }

    getSeverityText(severity) {
        const severityMap = {
            'Critical': 'Nghiêm trọng',
            'High': 'Cao',
            'Medium': 'Trung bình',
            'Low': 'Thấp'
        };
        return severityMap[severity] || severity;
    }

    getAutoHideTime(severity) {
        const timeMap = {
            'Critical': 20000, // 20 giây
            'High': 15000, // 15 giây
            'Medium': 12000, // 12 giây
            'Low': 10000 // 10 giây
        };
        return timeMap[severity] || 12000;
    }

    playAlertSound(severity) {
        try {
            const audio = new Audio('/sounds/alert.mp3');
            audio.volume = severity === 'Critical' ? 0.8 : 0.5;
            audio.play().catch(e => console.log('Error playing sound:', e));
        } catch (e) {
            console.log('Error playing alert sound:', e);
        }
    }

    clearAll() {
        this.notifications.forEach(notification => {
            this.hideNotification(notification);
        });
    }
}

const alertNotificationManager = new AlertNotificationManager();
window.alertNotificationManager = alertNotificationManager;
window.showAlertNotification = (alert) => alertNotificationManager.showAlert(alert); 