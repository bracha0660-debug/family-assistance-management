# Step 2 Verification Script
# Run from repo root: .\scripts\verify-step02.ps1
#
# SAFETY: Creates ONLY isolated disposable test data (org code VERIF-*).
# Does NOT modify, overwrite, or bootstrap admins on existing user organizations.
# Test password is generated per run — never use on real accounts.

$ErrorActionPreference = "Stop"
$baseApi = "http://localhost:8080"
$baseWeb = "http://localhost:3000"
$ts = Get-Date -Format "yyyyMMddHHmmss"
$orgCode = "VERIF-$ts"
$orgName = "Verification Org $ts"
$adminUser = "verif.admin.$ts"
$adminPass = "VerifPass-$ts!"
$cookieSa = Join-Path $env:TEMP "fam-step02-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "fam-step02-oa-$ts.txt"
$results = @()

function Write-Result($id, $name, $passed, $detail) {
    $status = if ($passed) { "PASS" } else { "FAIL" }
    Write-Host "[$status] $id - $name"
    if ($detail) { Write-Host "       $detail" }
    $script:results += [pscustomobject]@{ Id = $id; Name = $name; Passed = $passed; Detail = $detail }
}

function Invoke-CurlJson {
    param(
        [string]$Method = "GET",
        [string]$Uri,
        [string]$Body,
        [string]$CookieFile,
        [hashtable]$Headers = @{}
    )
    $args = @("-s", "-w", "`n%{http_code}", "-X", $Method, $Uri)
    if ($CookieFile) {
        if (Test-Path $CookieFile) { $args += @("-b", $CookieFile) }
        $args += @("-c", $CookieFile)
    }
    $bodyFile = $null
    if ($Body) {
        $bodyFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($bodyFile, $Body)
        $args += @("-H", "Content-Type: application/json", "--data-binary", "@$bodyFile")
    }
    foreach ($k in $Headers.Keys) {
        $args += @("-H", "$k`:$($Headers[$k])")
    }
    try {
        $raw = & curl.exe @args
    } finally {
        if ($bodyFile -and (Test-Path $bodyFile)) { Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue }
    }
    $lines = $raw -split "`n"
    $httpCode = [int]$lines[-1]
    $content = ($lines[0..($lines.Length - 2)] -join "`n").Trim()
    return @{ StatusCode = $httpCode; Content = $content }
}

function Get-JsonField($jsonText, $path) {
    if ([string]::IsNullOrWhiteSpace($jsonText)) { return $null }
    $obj = $jsonText | ConvertFrom-Json
    $current = $obj
    foreach ($part in $path.Split('.')) {
        if ($null -eq $current) { return $null }
        $current = $current.$part
    }
    return $current
}

Write-Host "=== Step 2 Verification ===" -ForegroundColor Cyan
Write-Host "NOTE: Creates isolated test org VERIF-* only. Does not touch existing organizations." -ForegroundColor Yellow

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (Test-Path $cookieSa) { Remove-Item $cookieSa -Force }
    if (Test-Path $cookieOa) { Remove-Item $cookieOa -Force }

    Write-Host "`nStarting docker compose..."
    docker compose up --build -d | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Result 1 "docker compose up --build" $false "docker compose failed"; exit 1 }

    Write-Host "Waiting for API..."
    $healthy = $false
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $h = Invoke-RestMethod -Uri "$baseApi/api/v1/health" -TimeoutSec 3
            if ($h.status -eq "healthy") { $healthy = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    Write-Result 1 "docker compose up --build succeeds" $healthy $(if (-not $healthy) { "API not healthy after 60s" })

    $health = Invoke-RestMethod -Uri "$baseApi/api/v1/health" -TimeoutSec 5
    $ok = $health.status -eq "healthy" -and $health.database -eq "connected"
    Write-Result 2 "API health returns 200 (regression)" $ok "status=$($health.status) database=$($health.database)"

    $anon = Invoke-CurlJson -Uri "$baseApi/api/v1/admin/organizations"
    Write-Result 3 "Anonymous admin access returns 401" ($anon.StatusCode -eq 401) "HTTP $($anon.StatusCode)"

    $login = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" `
        -Body '{"username":"superadmin","password":"ChangeMe123!"}' `
        -CookieFile $cookieSa
    $loginOk = $login.StatusCode -eq 200
    Write-Result 4 "SuperAdmin login works (regression)" $loginOk "HTTP $($login.StatusCode)"

    if (-not $loginOk) {
        Write-Host "`nCannot continue without SuperAdmin session." -ForegroundColor Red
        exit 1
    }

    $list = Invoke-CurlJson -Uri "$baseApi/api/v1/admin/organizations" -CookieFile $cookieSa
    $listOk = $list.StatusCode -eq 200 -and
        $null -ne (Get-JsonField $list.Content "summary.total") -and
        $null -ne (Get-JsonField $list.Content "summary.active") -and
        $null -ne (Get-JsonField $list.Content "summary.suspended")
    Write-Result 5 "GET organizations with summary" $listOk "HTTP $($list.StatusCode)"

    $badCode = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" `
        -Body '{"name":"bad","code":"lowercase"}' -CookieFile $cookieSa
    $badCodeErr = Get-JsonField $badCode.Content "code"
    Write-Result 6 "Invalid org code returns 400" ($badCode.StatusCode -eq 400 -and $badCodeErr -eq "VALIDATION_ERROR") "HTTP $($badCode.StatusCode) code=$badCodeErr"

    $createBody = (@{ name = $orgName; code = $orgCode } | ConvertTo-Json -Compress)
    $create = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body $createBody -CookieFile $cookieSa
    $orgId = Get-JsonField $create.Content "organization.id"
    $orgVersion = Get-JsonField $create.Content "organization.version"
    $createOk = $create.StatusCode -eq 201 -and (Get-JsonField $create.Content "organization.status") -eq "active"
    Write-Result 7 "Create organization returns 201" $createOk "id=$orgId code=$orgCode"

    if ($orgId) {
        $audQuery = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-001' AND entity_id = '$orgId';"
        $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQuery 2>&1
        Write-Result 8 'AUD-001 written on create' ($audRows -match 'AUD-001') ''
    } else {
        Write-Result 8 "AUD-001 written on create" $false "No org created"
    }

    if ($orgId) {
        $dup = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body $createBody -CookieFile $cookieSa
        $dupErr = Get-JsonField $dup.Content "code"
        Write-Result 9 "Duplicate org code returns 409" ($dup.StatusCode -eq 409 -and $dupErr -eq "DUPLICATE_ORG_CODE") "HTTP $($dup.StatusCode)"
    }

    if ($orgId) {
        $bootBody = (@{ username = $adminUser; password = $adminPass; fullName = "Test Admin" } | ConvertTo-Json -Compress)
        $boot = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body $bootBody -CookieFile $cookieSa
        $bootRole = Get-JsonField $boot.Content "user.role"
        Write-Result 10 "Bootstrap first org admin returns 201" ($boot.StatusCode -eq 201 -and $bootRole -eq "OrganizationAdministrator") "HTTP $($boot.StatusCode)"

        $audQuery = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-003' ORDER BY created_at DESC LIMIT 1;"
        $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQuery 2>&1
        Write-Result 11 'AUD-003 written on bootstrap' ($audRows -match 'AUD-003') ''

        $boot2Body = (@{ username = "other.$ts"; password = $adminPass; fullName = "Second Admin" } | ConvertTo-Json -Compress)
        $boot2 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body $boot2Body -CookieFile $cookieSa
        $boot2Err = Get-JsonField $boot2.Content "code"
        Write-Result 12 "Second bootstrap returns 409 ORG_ADMIN_EXISTS" ($boot2.StatusCode -eq 409 -and $boot2Err -eq "ORG_ADMIN_EXISTS") "HTTP $($boot2.StatusCode)"
    }

    $oaLoginBody = (@{ username = $adminUser; password = $adminPass } | ConvertTo-Json -Compress)
    $oaLogin = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body $oaLoginBody -CookieFile $cookieOa
    $oaLoginOk = $oaLogin.StatusCode -eq 200
    Write-Result 13 "Org admin login works" $oaLoginOk "HTTP $($oaLogin.StatusCode)"

    if ($oaLoginOk) {
        $oaAdmin = Invoke-CurlJson -Uri "$baseApi/api/v1/admin/organizations" -CookieFile $cookieOa
        $oaErr = Get-JsonField $oaAdmin.Content "code"
        Write-Result 14 "Non-SuperAdmin admin access returns 403" ($oaAdmin.StatusCode -eq 403 -and $oaErr -eq "FORBIDDEN") "HTTP $($oaAdmin.StatusCode)"
    } else {
        Write-Result 14 "Non-SuperAdmin admin access returns 403" $false "Org admin login failed"
    }

    if ($orgId) {
        $badSuspend = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/admin/organizations/$orgId/suspend" `
            -Body '{"reason":"ab"}' -CookieFile $cookieSa -Headers @{ "If-Match" = "$orgVersion" }
        $badSuspendErr = Get-JsonField $badSuspend.Content "code"
        Write-Result 15 "Suspend without valid reason returns 400" ($badSuspend.StatusCode -eq 400 -and $badSuspendErr -eq "VALIDATION_ERROR") "HTTP $($badSuspend.StatusCode)"

        $suspend = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/admin/organizations/$orgId/suspend" `
            -Body '{"reason":"Automated suspension test reason"}' -CookieFile $cookieSa -Headers @{ "If-Match" = "$orgVersion" }
        $suspendStatus = Get-JsonField $suspend.Content "organization.status"
        Write-Result 16 "Suspend organization returns 200" ($suspend.StatusCode -eq 200 -and $suspendStatus -eq "suspended") "HTTP $($suspend.StatusCode)"

        $audQuery = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-002' AND entity_id = '$orgId';"
        $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQuery 2>&1
        Write-Result 17 'AUD-002 written on suspend' ($audRows -match 'AUD-002') ''

        $suspend2 = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/admin/organizations/$orgId/suspend" `
            -Body '{"reason":"Second attempt"}' -CookieFile $cookieSa -Headers @{ "If-Match" = "99" }
        $suspend2Err = Get-JsonField $suspend2.Content "code"
        Write-Result 18 "Already suspended returns 409" ($suspend2.StatusCode -eq 409 -and $suspend2Err -eq "ALREADY_SUSPENDED") "HTTP $($suspend2.StatusCode)"
    }

    if ($oaLoginOk) {
        $oaMe = Invoke-CurlJson -Uri "$baseApi/api/v1/auth/me" -CookieFile $cookieOa
        Write-Result 19 "Suspended org user /me returns 401" ($oaMe.StatusCode -eq 401) "HTTP $($oaMe.StatusCode)"
    } else {
        Write-Result 19 "Suspended org user /me returns 401" $false "Org admin login failed"
    }

    $saMe = Invoke-CurlJson -Uri "$baseApi/api/v1/auth/me" -CookieFile $cookieSa
    $saRole = Get-JsonField $saMe.Content "user.role"
    Write-Result 20 "SuperAdmin /me unaffected (regression)" ($saMe.StatusCode -eq 200 -and $saRole -eq "SuperAdmin") "HTTP $($saMe.StatusCode)"

    $html = Invoke-WebRequest -Uri $baseWeb -UseBasicParsing -TimeoutSec 10
    $rtl = $html.Content -match 'dir="rtl"' -and $html.Content -match 'lang="he"'
    Write-Result 21 "Frontend Hebrew RTL (regression)" $rtl ""

    $list2 = Invoke-CurlJson -Uri "$baseApi/api/v1/admin/organizations" -CookieFile $cookieSa
    $suspendedCount = Get-JsonField $list2.Content "summary.suspended"
    $orgs = (Get-JsonField $list2.Content "organizations")
    $found = $orgs | Where-Object { $_.code -eq $orgCode -and $_.status -eq "suspended" }
    Write-Result 22 "Summary counts reflect suspended org" ($suspendedCount -ge 1 -and $null -ne $found) "suspended=$suspendedCount"

    $noStep3 = $true
    foreach ($p in @("/api/v1/users", "/api/v1/families", "/api/v1/suppliers")) {
        $r = Invoke-CurlJson -Uri "$baseApi$p" -CookieFile $cookieSa
        if ($r.StatusCode -ne 404) { $noStep3 = $false }
    }
    Write-Result 23 "No Step 3+ APIs exposed" $noStep3 ""

} finally {
    Pop-Location
    if (Test-Path $cookieSa) { Remove-Item $cookieSa -Force -ErrorAction SilentlyContinue }
    if (Test-Path $cookieOa) { Remove-Item $cookieOa -Force -ErrorAction SilentlyContinue }
}

$passed = ($results | Where-Object { $_.Passed }).Count
$total = $results.Count
Write-Host "`n=== Step 2 Verification: $passed / $total PASS ===" -ForegroundColor Cyan
if ($passed -lt $total) { exit 1 }
