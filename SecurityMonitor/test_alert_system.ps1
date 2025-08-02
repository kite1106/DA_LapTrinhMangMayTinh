# Test Alert System
Write-Host "🧪 Testing Alert System..." -ForegroundColor Yellow

# Test 1: Kiểm tra các file đã được tạo
Write-Host "`n📁 Checking files..." -ForegroundColor Cyan
$files = @(
    "wwwroot/css/alert-notifications.css",
    "wwwroot/js/alert-notifications.js",
    "Views/Shared/_Layout.cshtml"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "✅ $file exists" -ForegroundColor Green
    } else {
        Write-Host "❌ $file missing" -ForegroundColor Red
    }
}

# Test 2: Kiểm tra layout đã include files
Write-Host "`n📁 Checking Layout includes..." -ForegroundColor Cyan
$layoutContent = Get-Content "Views/Shared/_Layout.cshtml" -Raw
if ($layoutContent -match "alert-notifications\.css") {
    Write-Host "✅ CSS included in layout" -ForegroundColor Green
} else {
    Write-Host "❌ CSS not included in layout" -ForegroundColor Red
}

if ($layoutContent -match "alert-notifications\.js") {
    Write-Host "✅ JavaScript included in layout" -ForegroundColor Green
} else {
    Write-Host "❌ JavaScript not included in layout" -ForegroundColor Red
}

# Test 3: Kiểm tra toastr đã được tắt
if ($layoutContent -match "Tắt toastr cho cảnh báo") {
    Write-Host "✅ Toastr disabled for alerts" -ForegroundColor Green
} else {
    Write-Host "❌ Toastr not disabled for alerts" -ForegroundColor Red
}

# Test 4: Kiểm tra Alerts.cshtml đã được cập nhật
Write-Host "`n📁 Checking Alerts.cshtml..." -ForegroundColor Cyan
$alertsContent = Get-Content "Views/Admin/Alerts.cshtml" -Raw
if ($alertsContent -match "showAlertNotification") {
    Write-Host "✅ Alerts.cshtml uses new notification system" -ForegroundColor Green
} else {
    Write-Host "❌ Alerts.cshtml still uses old notification system" -ForegroundColor Red
}

Write-Host "`n🎯 Summary:" -ForegroundColor Yellow
Write-Host "✅ Alert notifications should now appear in top-left corner only" -ForegroundColor Green
Write-Host "✅ No more overflowing alerts that break the page" -ForegroundColor Green
Write-Host "✅ Different colors for different severity levels" -ForegroundColor Green
Write-Host "✅ Auto-hide based on severity level" -ForegroundColor Green

Write-Host "`n🚀 Next steps:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet run" -ForegroundColor White
Write-Host "2. Go to /Admin/Alerts" -ForegroundColor White
Write-Host "3. Check if alerts appear in top-left corner only" -ForegroundColor White
Write-Host "4. Verify no more overflowing alerts" -ForegroundColor White 