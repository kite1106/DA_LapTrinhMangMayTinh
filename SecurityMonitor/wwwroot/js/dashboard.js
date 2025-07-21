"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/alertHub")
    .withAutomaticReconnect()
    .build();

connection.on("ReceiveAlert", function (alert) {
    // Add the new alert to the dashboard
    addAlertToTable(alert);
    
    // Update alert counter
    updateAlertCounter();
    
    // Show notification
    showNotification(alert);
});

function addAlertToTable(alert) {
    const table = document.getElementById("alertsTable");
    if (!table) return;

    const row = table.insertRow(1); // Insert after header
    
    // Add cells
    const cells = [
        formatDate(alert.timestamp),
        alert.title,
        alert.sourceIp,
        getSeverityBadge(alert.severityLevelId),
        getAlertTypeName(alert.alertTypeId),
        `<button class="btn btn-sm btn-primary" onclick="viewAlertDetails(${alert.id})">Chi tiết</button>`
    ];

    cells.forEach(cellContent => {
        const cell = row.insertCell();
        cell.innerHTML = cellContent;
    });
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('vi-VN');
}

function getSeverityBadge(severityId) {
    const severityClasses = {
        1: 'badge bg-info',
        2: 'badge bg-warning',
        3: 'badge bg-danger',
        4: 'badge bg-dark'
    };
    const severityNames = {
        1: 'Thấp',
        2: 'Trung bình',
        3: 'Cao',
        4: 'Nghiêm trọng'
    };
    return `<span class="${severityClasses[severityId] || 'badge bg-secondary'}">${severityNames[severityId] || 'Không xác định'}</span>`;
}

function getAlertTypeName(typeId) {
    const typeNames = {
        1: 'Đăng nhập thất bại',
        2: 'Truy cập trái phép',
        3: 'Tấn công brute force',
        4: 'Hoạt động bất thường'
        // Add more alert types as needed
    };
    return typeNames[typeId] || 'Khác';
}

function updateAlertCounter() {
    const counter = document.getElementById("alertCounter");
    if (counter) {
        const currentCount = parseInt(counter.innerText) || 0;
        counter.innerText = currentCount + 1;
    }
}

function showNotification(alert) {
    // Check if browser supports notifications
    if (!("Notification" in window)) return;

    // Check if permission is granted
    if (Notification.permission === "granted") {
        new Notification("Cảnh báo mới!", {
            body: alert.title,
            icon: "/images/alert-icon.png"
        });
    }
    // Otherwise, request permission
    else if (Notification.permission !== "denied") {
        Notification.requestPermission().then(permission => {
            if (permission === "granted") {
                new Notification("Cảnh báo mới!", {
                    body: alert.title,
                    icon: "/images/alert-icon.png"
                });
            }
        });
    }
}

// Start the connection
connection.start()
    .then(() => console.log("Đã kết nối với AlertHub"))
    .catch(err => console.error("Lỗi kết nối: " + err));
