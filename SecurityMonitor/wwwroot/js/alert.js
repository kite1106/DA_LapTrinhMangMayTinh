const connection = new signalR.HubConnectionBuilder()
    .withUrl("/alertHub")
    .withAutomaticReconnect()
    .build();

// Xử lý cảnh báo thông thường
connection.on("ReceiveAlert", function (alert) {
    handleAlert(alert, false);
});

// Xử lý cảnh báo ưu tiên cao
connection.on("ReceiveHighPriorityAlert", function (alert) {
    handleAlert(alert, true);
});

// Xử lý cập nhật trạng thái
connection.on("AlertStatusUpdated", function (alertId, newStatus) {
    updateAlertStatus(alertId, newStatus);
});

// Xử lý khi được assign cảnh báo
connection.on("AlertAssigned", function (alert) {
    showAssignmentNotification(alert);
});

// Xử lý lỗi hệ thống
connection.on("ReceiveError", function (title, message) {
    showErrorNotification(title, message);
});

function handleAlert(alert, isHighPriority) {
    // Tạo thông báo mới với style phù hợp
    const notification = createAlertNotification(alert, isHighPriority);
    
    // Thêm vào bảng alerts nếu đang ở trang alerts
    const alertsTable = document.getElementById("alertsTable");
    if (alertsTable) {
        const newRow = createAlertTableRow(alert);
        if (isHighPriority) {
            newRow.classList.add('high-priority');
        }
        alertsTable.querySelector("tbody").prepend(newRow);
    }

    // Cập nhật dashboard counters
    updateDashboardCounters(alert);

    // Phát âm thanh cho cảnh báo nghiêm trọng
    if (isHighPriority) {
        playHighPrioritySound();
    } else if (alert.severityLevelId >= 3) {
        playAlertSound();
    }
}

function createAlertNotification(alert) {
    const severityClass = getSeverityClass(alert.severityLevelId);
    const notification = document.createElement("div");
    notification.className = `alert ${severityClass} alert-dismissible fade show`;
    notification.innerHTML = `
        <strong>${alert.title}</strong>
        <p>${alert.description}</p>
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    document.getElementById("notificationArea").prepend(notification);
    return notification;
}

function createAlertTableRow(alert) {
    const tr = document.createElement("tr");
    tr.innerHTML = `
        <td>${formatDate(alert.timestamp)}</td>
        <td>${alert.title}</td>
        <td>${getSeverityBadge(alert.severityLevelId)}</td>
        <td>${alert.sourceIp}</td>
        <td>${alert.targetIp}</td>
        <td>${getStatusBadge(alert.statusId)}</td>
        <td>
            <button class="btn btn-sm btn-primary" onclick="viewAlertDetails(${alert.id})">
                Chi tiết
            </button>
        </td>
    `;
    return tr;
}

function getSeverityClass(severityId) {
    switch (severityId) {
        case 4: return "alert-danger";
        case 3: return "alert-warning";
        case 2: return "alert-info";
        default: return "alert-secondary";
    }
}

function getSeverityBadge(severityId) {
    const severityMap = {
        4: { text: "Khẩn cấp", class: "danger" },
        3: { text: "Cao", class: "warning" },
        2: { text: "Trung bình", class: "info" },
        1: { text: "Thấp", class: "secondary" }
    };
    const severity = severityMap[severityId] || severityMap[1];
    return `<span class="badge bg-${severity.class}">${severity.text}</span>`;
}

function getStatusBadge(statusId) {
    const statusMap = {
        1: { text: "Mới", class: "info" },
        2: { text: "Đang xử lý", class: "warning" },
        3: { text: "Đã xử lý", class: "success" },
        4: { text: "Dương tính giả", class: "secondary" },
        5: { text: "Bỏ qua", class: "danger" }
    };
    const status = statusMap[statusId] || statusMap[1];
    return `<span class="badge bg-${status.class}">${status.text}</span>`;
}

function updateDashboardCounters(alert) {
    // Cập nhật các bộ đếm trên dashboard
    const totalCounter = document.getElementById("totalAlerts");
    if (totalCounter) {
        totalCounter.textContent = parseInt(totalCounter.textContent) + 1;
    }

    const severityCounter = document.getElementById(`severity${alert.severityLevelId}Count`);
    if (severityCounter) {
        severityCounter.textContent = parseInt(severityCounter.textContent) + 1;
    }
}

function playAlertSound() {
    const audio = new Audio('/media/alert.mp3');
    audio.play();
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('vi-VN');
}

// Kết nối SignalR khi trang load xong
document.addEventListener('DOMContentLoaded', function () {
    connection.start()
        .then(function () {
            console.log("SignalR Connected!");
            // Tham gia nhóm theo role của user
            const userRole = document.body.dataset.userRole;
            if (userRole) {
                connection.invoke("JoinGroup", userRole);
            }
        })
        .catch(function (err) {
            console.error(err.toString());
        });
});
