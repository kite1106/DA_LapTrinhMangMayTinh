// Khởi tạo kết nối SignalR với cấu hình chi tiết
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/alertHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000]) // Retry sau 0s, 2s, 5s, 10s, 20s
    .configureLogging(signalR.LogLevel.Debug)
    .build();

// Xử lý sự kiện kết nối/ngắt kết nối
connection.onreconnecting(error => {
    console.log("Đang kết nối lại SignalR...", error);
    toastr.info("Đang kết nối lại với máy chủ...");
});

connection.onreconnected(connectionId => {
    console.log("Đã kết nối lại SignalR", connectionId);
    toastr.success("Đã kết nối lại thành công!");
});

connection.onclose(error => {
    console.log("Mất kết nối SignalR", error);
    toastr.error("Mất kết nối với máy chủ. Đang thử kết nối lại...");
});

// Xử lý sự kiện nhận cảnh báo
connection.on("ReceiveLoginAlert", function (alert) {
    console.log("Nhận cảnh báo:", alert);
    showToast(alert);
    addAlertToList(alert);
    playAlertSound();
    updateAlertCount(1);
});

// Toast notification
function showToast(alert) {
    toastr.options = {
        closeButton: true,
        progressBar: true,
        positionClass: "toast-top-right",
        timeOut: 5000
    };
    
    toastr.warning(alert.description, alert.title);
}

// Thêm cảnh báo vào danh sách
function addAlertToList(alert) {
    const alertsContainer = document.getElementById('alertsContainer');
    if (alertsContainer) {
        const alertHtml = `
            <div class="alert alert-warning alert-dismissible fade show" role="alert" data-alert-id="${alert.id}">
                <h5 class="alert-heading">${alert.title}</h5>
                <p>${alert.description}</p>
                <hr>
                <p class="mb-0">
                    <small>
                        <strong>Email:</strong> ${alert.email}<br>
                        <strong>IP:</strong> ${alert.ip}<br>
                        <strong>Số lần thất bại:</strong> ${alert.failedAttempts}<br>
                        <strong>Thời gian:</strong> ${new Date(alert.timestamp).toLocaleString()}
                    </small>
                </p>
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;
        alertsContainer.insertAdjacentHTML('afterbegin', alertHtml);
    }
}

// Phát âm thanh thông báo
function playAlertSound() {
    const audio = document.getElementById('alertSound');
    if (audio) {
        audio.play().catch(function(error) {
            console.log("Không thể phát âm thanh:", error);
        });
    }
}

// Cập nhật số lượng cảnh báo
function updateAlertCount(increment) {
    const elements = document.querySelectorAll('.alert-count');
    elements.forEach(element => {
        const currentCount = parseInt(element.textContent || '0');
        element.textContent = currentCount + increment;
    });
}

// Kết nối đến SignalR hub
connection.start()
    .then(function() {
        console.log("Đã kết nối đến AlertHub");
    })
    .catch(function(err) {
        console.error("Lỗi kết nối đến AlertHub:", err.toString());
        // Thử kết nối lại sau 5 giây
        setTimeout(() => connection.start(), 5000);
    });
