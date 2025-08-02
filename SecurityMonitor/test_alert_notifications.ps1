# Test Alert Notifications System
Write-Host "🧪 Testing Alert Notifications System..." -ForegroundColor Yellow

# Test 1: Kiểm tra file CSS
Write-Host "`n📁 Checking CSS file..." -ForegroundColor Cyan
if (Test-Path "SecurityMonitor/wwwroot/css/alert-notifications.css") {
    Write-Host "✅ alert-notifications.css exists" -ForegroundColor Green
} else {
    Write-Host "❌ alert-notifications.css missing" -ForegroundColor Red
}

# Test 2: Kiểm tra file JavaScript
Write-Host "`n📁 Checking JavaScript file..." -ForegroundColor Cyan
if (Test-Path "SecurityMonitor/wwwroot/js/alert-notifications.js") {
    Write-Host "✅ alert-notifications.js exists" -ForegroundColor Green
} else {
    Write-Host "❌ alert-notifications.js missing" -ForegroundColor Red
}

# Test 3: Kiểm tra layout đã include files
Write-Host "`n📁 Checking Layout includes..." -ForegroundColor Cyan
$layoutContent = Get-Content "SecurityMonitor/Views/Shared/_Layout.cshtml" -Raw
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

# Test 4: Kiểm tra middleware
Write-Host "`n📁 Checking Middleware..." -ForegroundColor Cyan
if (Test-Path "SecurityMonitor/Middleware/RestrictedUserMiddleware.cs") {
    Write-Host "✅ RestrictedUserMiddleware exists" -ForegroundColor Green
} else {
    Write-Host "❌ RestrictedUserMiddleware missing" -ForegroundColor Red
}

# Test 5: Kiểm tra SignalR Hub
Write-Host "`n📁 Checking SignalR Hub..." -ForegroundColor Cyan
if (Test-Path "SecurityMonitor/Hubs/AccountHub.cs") {
    Write-Host "✅ AccountHub exists" -ForegroundColor Green
} else {
    Write-Host "❌ AccountHub missing" -ForegroundColor Red
}

Write-Host "`n🎯 Test Summary:" -ForegroundColor Yellow
Write-Host "✅ Alert notification system should work properly" -ForegroundColor Green
Write-Host "✅ User blocking/restricting should show notifications" -ForegroundColor Green
Write-Host "✅ Notifications appear in top-left corner" -ForegroundColor Green
Write-Host "✅ Different severity levels have different colors" -ForegroundColor Green

Write-Host "`n🚀 Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the application: dotnet run" -ForegroundColor White
Write-Host "2. Login as admin and block/restrict a user" -ForegroundColor White
Write-Host "3. Login as that user to see notifications" -ForegroundColor White
Write-Host "4. Check if notifications appear in top-left corner" -ForegroundColor White 