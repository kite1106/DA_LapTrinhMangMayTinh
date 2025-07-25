$sourceDir = "node_modules"
$targetDir = "wwwroot\lib"

# Create target directories
$libs = @(
    "jquery",
    "bootstrap",
    "datatables",
    "toastr",
    "chartjs",
    "jquery-validation",
    "jquery-validation-unobtrusive",
    "microsoft/signalr"
)

foreach ($lib in $libs) {
    $path = Join-Path $targetDir $lib
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force
    }
}

# Copy jQuery
Copy-Item "$sourceDir\jquery\dist\*" "$targetDir\jquery\dist" -Recurse -Force

# Copy Bootstrap
Copy-Item "$sourceDir\bootstrap\dist\*" "$targetDir\bootstrap\dist" -Recurse -Force

# Copy DataTables
Copy-Item "$sourceDir\datatables.net\js\*" "$targetDir\datatables\js" -Recurse -Force
Copy-Item "$sourceDir\datatables.net-bs5\js\*" "$targetDir\datatables\js" -Recurse -Force
Copy-Item "$sourceDir\datatables.net-bs5\css\*" "$targetDir\datatables\css" -Recurse -Force

# Copy Toastr
Copy-Item "$sourceDir\toastr\build\*" "$targetDir\toastr" -Recurse -Force

# Copy Chart.js
Copy-Item "$sourceDir\chart.js\dist\*" "$targetDir\chartjs" -Recurse -Force

# Copy jQuery Validation
Copy-Item "$sourceDir\jquery-validation\dist\*" "$targetDir\jquery-validation\dist" -Recurse -Force

# Copy jQuery Validation Unobtrusive
Copy-Item "$sourceDir\jquery-validation-unobtrusive\dist\*" "$targetDir\jquery-validation-unobtrusive" -Recurse -Force

# Copy SignalR
Copy-Item "$sourceDir\@microsoft\signalr\dist\browser\*" "$targetDir\microsoft\signalr\dist\browser" -Recurse -Force

# Copy Font Awesome
$faPath = Join-Path $targetDir "fontawesome"
if (-not (Test-Path $faPath)) {
    New-Item -ItemType Directory -Path $faPath -Force
}
Copy-Item "$sourceDir\@fortawesome\fontawesome-free\css\*" "$targetDir\fontawesome\css" -Recurse -Force
Copy-Item "$sourceDir\@fortawesome\fontawesome-free\webfonts\*" "$targetDir\fontawesome\webfonts" -Recurse -Force
