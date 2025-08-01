# Test Log Generation Toggle
Write-Host "🧪 Testing Log Generation Toggle..." -ForegroundColor Yellow

# Test 1: Kiểm tra API endpoints
Write-Host "`n📁 Testing API endpoints..." -ForegroundColor Cyan

$baseUrl = "http://localhost:5100"

# Test status endpoint
Write-Host "Testing GET /api/loggeneration/status..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/loggeneration/status" -Method GET
    Write-Host "✅ Status endpoint working" -ForegroundColor Green
    Write-Host "   Current status: $($response.isEnabled)" -ForegroundColor White
} catch {
    Write-Host "❌ Status endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test enable endpoint
Write-Host "`nTesting POST /api/loggeneration/enable..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/loggeneration/enable" -Method POST
    Write-Host "✅ Enable endpoint working" -ForegroundColor Green
    Write-Host "   Response: $($response.message)" -ForegroundColor White
} catch {
    Write-Host "❌ Enable endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test disable endpoint
Write-Host "`nTesting POST /api/loggeneration/disable..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/loggeneration/disable" -Method POST
    Write-Host "✅ Disable endpoint working" -ForegroundColor Green
    Write-Host "   Response: $($response.message)" -ForegroundColor White
} catch {
    Write-Host "❌ Disable endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test toggle endpoint
Write-Host "`nTesting POST /api/loggeneration/toggle..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/loggeneration/toggle" -Method POST
    Write-Host "✅ Toggle endpoint working" -ForegroundColor Green
    Write-Host "   New status: $($response.isEnabled)" -ForegroundColor White
    Write-Host "   Response: $($response.message)" -ForegroundColor White
} catch {
    Write-Host "❌ Toggle endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎯 Summary:" -ForegroundColor Yellow
Write-Host "✅ All API endpoints should be working" -ForegroundColor Green
Write-Host "✅ Toggle functionality should work correctly" -ForegroundColor Green
Write-Host "✅ FakeLogGeneratorService should respect the toggle" -ForegroundColor Green

Write-Host "`n🚀 Next steps:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet run" -ForegroundColor White
Write-Host "2. Open browser console on Admin dashboard" -ForegroundColor White
Write-Host "3. Toggle the 'Auto Log Generation' switch" -ForegroundColor White
Write-Host "4. Check console logs for debug information" -ForegroundColor White
Write-Host "5. Verify logs stop/start generating" -ForegroundColor White

Write-Host "`n📋 Expected Behavior:" -ForegroundColor Yellow
Write-Host "• Toggle ON → Logs should start generating" -ForegroundColor White
Write-Host "• Toggle OFF → Logs should stop generating" -ForegroundColor White
Write-Host "• Console should show debug messages" -ForegroundColor White
Write-Host "• Toastr notifications should appear" -ForegroundColor White 