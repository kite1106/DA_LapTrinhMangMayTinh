// Initialize SignalR connection
const initializeSignalR = () => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/alertHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("ReceiveAlert", (alert) => {
        // Update counts if on dashboard
        const alertCountElement = document.getElementById('activeAlerts');
        if (alertCountElement) {
            const currentCount = parseInt(alertCountElement.innerText);
            alertCountElement.innerText = currentCount + 1;
        }

        // Show notification
        if (alert.severity === 'Critical') {
            toastr.error(alert.message, 'Cảnh báo nghiêm trọng', {
                timeOut: 0,
                extendedTimeOut: 0,
                closeButton: true
            });
        } else if (alert.severity === 'High') {
            toastr.warning(alert.message, 'Cảnh báo mức độ cao');
        }
    });

    connection.start().catch(err => console.error('SignalR Connection Error:', err));
    return connection;
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
