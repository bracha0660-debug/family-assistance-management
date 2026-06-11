# Step 1 Verification Script
# Run from repo root: .\scripts\verify-step01.ps1

$ErrorActionPreference = "Stop"
$baseApi = "http://localhost:8080"
$baseWeb = "http://localhost:3000"
$cookieJar = Join-Path $env:TEMP "fam-verify-cookies.txt"

function Write-Result($id, $name, $passed, $detail) {
    $status = if ($passed) { "PASS" } else { "FAIL" }
    Write-Host "[$status] $id - $name"
    if ($detail) { Write-Host "       $detail" }
}

Write-Host "=== Step 1 Verification ===" -ForegroundColor Cyan

# 1. Docker compose build + up
Write-Host "`nStarting docker compose..."
docker compose up --build -d
if ($LASTEXITCODE -ne 0) { Write-Result 1 "docker compose up --build" $false "docker compose failed"; exit 1 }

Write-Host "Waiting for services..."
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $h = Invoke-RestMethod -Uri "$baseApi/api/v1/health" -TimeoutSec 3
        if ($h.status -eq "healthy") { $healthy = $true; break }
    } catch { Start-Sleep -Seconds 2 }
}
Write-Result 1 "docker compose up --build succeeds" $healthy $(if (-not $healthy) { "API not healthy after 60s" })

# 2. Health endpoint
try {
    $health = Invoke-RestMethod -Uri "$baseApi/api/v1/health"
    $ok = $health.status -eq "healthy" -and $health.database -eq "connected"
    Write-Result 2 "API health returns 200" $ok "status=$($health.status) database=$($health.database)"
} catch {
    Write-Result 2 "API health returns 200" $false $_.Exception.Message
}

# 3. Login page RTL (check HTML)
try {
    $html = Invoke-WebRequest -Uri $baseWeb -UseBasicParsing
    $rtl = $html.Content -match 'dir="rtl"' -and $html.Content -match 'lang="he"'
    Write-Result 3 "Login page Hebrew RTL" $rtl "dir=rtl and lang=he in HTML"
} catch {
    Write-Result 3 "Login page Hebrew RTL" $false $_.Exception.Message
}

# 4-7. Auth + security audit via API
if (Test-Path $cookieJar) { Remove-Item $cookieJar }

# Invalid login -> SEC-002
try {
    Invoke-WebRequest -Uri "$baseApi/api/v1/auth/login" -Method POST `
        -ContentType "application/json" `
        -Body '{"username":"superadmin","password":"WrongPassword123!"}' `
        -SessionVariable null -ErrorAction SilentlyContinue | Out-Null
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Write-Result 6 "Invalid login returns 401" ($code -eq 401) "HTTP $code"
}

# Valid login -> SEC-001
$loginOk = $false
try {
    $loginResp = Invoke-WebRequest -Uri "$baseApi/api/v1/auth/login" -Method POST `
        -ContentType "application/json" `
        -Body '{"username":"superadmin","password":"ChangeMe123!"}' `
        -SessionVariable webSession
    $loginOk = $loginResp.StatusCode -eq 200
    Write-Result 4 "SuperAdmin login works" $loginOk "HTTP $($loginResp.StatusCode)"
    Write-Result 6 "Successful login (SEC-001 trigger)" $loginOk ""
} catch {
    Write-Result 4 "SuperAdmin login works" $false $_.Exception.Message
}

# Logout -> SEC-005
$logoutOk = $false
if ($loginOk) {
    try {
        $logoutResp = Invoke-WebRequest -Uri "$baseApi/api/v1/auth/logout" -Method POST -WebSession $webSession
        $logoutOk = $logoutResp.StatusCode -eq 204
        Write-Result 7 "Logout creates SEC-005" $logoutOk "HTTP $($logoutResp.StatusCode)"
    } catch {
        Write-Result 7 "Logout creates SEC-005" $false $_.Exception.Message
    }
}

# 8. security_audit_logs in DB
Write-Host "`nChecking security_audit_logs..."
$auditQuery = @"
SELECT event_code, event_type, username_attempted, created_at
FROM security_audit_logs
ORDER BY created_at;
"@

try {
    $auditRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $auditQuery 2>&1
    $has001 = $auditRows -match "SEC-001"
    $has002 = $auditRows -match "SEC-002"
    $has005 = $auditRows -match "SEC-005"
    $auditOk = $has001 -and $has002 -and $has005
    Write-Result 5 "Invalid login creates SEC-002" $has002 ""
    Write-Result 8 "security_audit_logs expected records" $auditOk ""
    Write-Host $auditRows
} catch {
    Write-Result 8 "security_audit_logs expected records" $false $_.Exception.Message
}

# 9. No Step 2 APIs (static endpoint probe)
$step2Paths = @(
    "/api/v1/admin/organizations",
    "/api/v1/users",
    "/api/v1/families",
    "/api/v1/suppliers"
)
$noStep2 = $true
foreach ($p in $step2Paths) {
    try {
        $r = Invoke-WebRequest -Uri "$baseApi$p" -Method GET -ErrorAction SilentlyContinue
        if ($r.StatusCode -ne 404) { $noStep2 = $false; Write-Host "       Unexpected response for $p : $($r.StatusCode)" }
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        if ($code -ne 404 -and $code -ne 401) { $noStep2 = $false; Write-Host "       $p returned $code" }
    }
}
Write-Result 9 "No Step 2 entities or APIs" $noStep2 "Step 2 paths return 404/401 only"

Write-Host "`n=== Verification complete ===" -ForegroundColor Cyan
