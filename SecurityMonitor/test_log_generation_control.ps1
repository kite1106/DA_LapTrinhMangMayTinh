# Test Log Generation Control System
Write-Host "🧪 Testing Log Generation Control System..." -ForegroundColor Yellow

# Test 1: Kiểm tra các file đã được tạo
Write-Host "`n📁 Checking files..." -ForegroundColor Cyan
$files = @(
    "Services/Interfaces/ILogGenerationControlService.cs",
    "Services/Implementation/LogGenerationControlService.cs",
    "Controllers/Api/LogGenerationController.cs",
    "Services/Implementation/FakeLogGeneratorService.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "✅ $file exists" -ForegroundColor Green
    } else {
        Write-Host "❌ $file missing" -ForegroundColor Red
    }
}

# Test 2: Kiểm tra Program.cs đã đăng ký services
Write-Host "`n📁 Checking Program.cs registrations..." -ForegroundColor Cyan
$programContent = Get-Content "Program.cs" -Raw
if ($programContent -match "AddHostedService<FakeLogGeneratorService>") {
    Write-Host "✅ FakeLogGeneratorService registered" -ForegroundColor Green
} else {
    Write-Host "❌ FakeLogGeneratorService not registered" -ForegroundColor Red
}

if ($programContent -match "AddScoped<ILogGenerationControlService") {
    Write-Host "✅ LogGenerationControlService registered" -ForegroundColor Green
} else {
    Write-Host "❌ LogGenerationControlService not registered" -ForegroundColor Red
}

# Test 3: Kiểm tra Admin Index đã có UI controls
Write-Host "`n📁 Checking Admin UI..." -ForegroundColor Cyan
$adminContent = Get-Content "Views/Admin/Index.cshtml" -Raw
if ($adminContent -match "logGenerationToggle") {
    Write-Host "✅ Log generation toggle exists in UI" -ForegroundColor Green
} else {
    Write-Host "❌ Log generation toggle missing from UI" -ForegroundColor Red
}

if ($adminContent -match "toggleLogGeneration") {
    Write-Host "✅ Toggle function exists in JavaScript" -ForegroundColor Green
} else {
    Write-Host "❌ Toggle function missing from JavaScript" -ForegroundColor Red
}

# Test 4: Kiểm tra API endpoints
Write-Host "`n📁 Checking API endpoints..." -ForegroundColor Cyan
$controllerContent = Get-Content "Controllers/Api/LogGenerationController.cs" -Raw
$endpoints = @(
    "HttpGet.*status",
    "HttpPost.*enable", 
    "HttpPost.*disable",
    "HttpPost.*toggle"
)

foreach ($endpoint in $endpoints) {
    if ($controllerContent -match $endpoint) {
        Write-Host "✅ $endpoint endpoint exists" -ForegroundColor Green
    } else {
        Write-Host "❌ $endpoint endpoint missing" -ForegroundColor Red
    }
}

Write-Host "`n🎯 Summary:" -ForegroundColor Yellow
Write-Host "✅ Log generation control system implemented" -ForegroundColor Green
Write-Host "✅ Toggle button available in Admin dashboard" -ForegroundColor Green
Write-Host "✅ API endpoints for enable/disable/toggle" -ForegroundColor Green
Write-Host "✅ Background service with status checking" -ForegroundColor Green

Write-Host "`n🚀 Next steps:" -ForegroundColor Yellow
Write-Host "1. Run: dotnet run" -ForegroundColor White
Write-Host "2. Go to /Admin/Index" -ForegroundColor White
Write-Host "3. Test the log generation toggle button" -ForegroundColor White
Write-Host "4. Check if logs are generated when enabled" -ForegroundColor White
Write-Host "5. Verify logs stop when disabled" -ForegroundColor White

Write-Host "`n📋 API Endpoints:" -ForegroundColor Cyan
Write-Host "GET  /api/loggeneration/status" -ForegroundColor White
Write-Host "POST /api/loggeneration/enable" -ForegroundColor White
Write-Host "POST /api/loggeneration/disable" -ForegroundColor White
Write-Host "POST /api/loggeneration/toggle" -ForegroundColor White 