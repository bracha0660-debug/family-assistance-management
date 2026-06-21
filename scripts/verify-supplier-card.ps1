# Supplier Card Verification Script (~20 tests)
# Run from repo root: .\scripts\verify-supplier-card.ps1

$ErrorActionPreference = "Stop"
$baseApi = "http://localhost:8080"
$ts = Get-Date -Format "yyyyMMddHHmmss"
$results = @()

function Write-Result($id, $name, $passed, $detail) {
    $status = if ($passed) { "PASS" } else { "FAIL" }
    Write-Host "[$status] $id - $name"
    if ($detail) { Write-Host "       $detail" }
    $script:results += [pscustomobject]@{ Id = $id; Name = $name; Passed = $passed; Detail = $detail }
}

function Invoke-CurlJson {
    param([string]$Method = "GET", [string]$Uri, [string]$Body, [string]$CookieFile, [hashtable]$Headers = @{})
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
    foreach ($k in $Headers.Keys) { $args += @("-H", "$k`:$($Headers[$k])") }
    try { $raw = & curl.exe @args } finally { if ($bodyFile) { Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue } }
    $lines = $raw -split "`n"
    return @{ Content = ($lines[0..($lines.Length - 2)] -join "`n").Trim(); StatusCode = [int]$lines[-1] }
}

function Get-JsonField($jsonText, $path) {
    if ([string]::IsNullOrWhiteSpace($jsonText)) { return $null }
    $obj = $jsonText | ConvertFrom-Json
    $current = $obj
    foreach ($part in $path.Split('.')) { if ($null -eq $current) { return $null }; $current = $current.$part }
    return $current
}

function New-SupplierBody([hashtable]$Extra = @{}) {
    $body = @{
        name = "Supplier Test"
        registrationNumber = "123456782"
        bankNumber = "12"
        branchNumber = "345"
        accountNumber = "1234567"
        accountHolderName = "Supplier Holder"
    }
    foreach ($k in $Extra.Keys) { $body[$k] = $Extra[$k] }
    return ($body | ConvertTo-Json -Compress)
}

$cookieSa = Join-Path $env:TEMP "sup-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "sup-oa-$ts.txt"
$cookieMgr = Join-Path $env:TEMP "sup-mgr-$ts.txt"
$cookieFin = Join-Path $env:TEMP "sup-fin-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "sup-coord-$ts.txt"
$userPwd = "SupUser-$ts!"
$orgCode = "SUP-$ts"

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieMgr, $cookieFin, $cookieCoord)) {
        if (Test-Path $c) { Remove-Item $c -Force }
    }

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" `
        -Body (@{ username = "superadmin"; password = "ChangeMe123!" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    $org = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" `
        -Body (@{ name = "Supplier Org"; code = $orgCode } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgId = Get-JsonField $org.Content "organization.id"
    $adminUser = "sup.admin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" `
        -Body (@{ username = $adminUser; password = "SupAdmin-$ts!"; fullName = "Sup Admin" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" `
        -Body (@{ username = $adminUser; password = "SupAdmin-$ts!" } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null

    $roles = Get-JsonField (Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieOa).Content "roles"
    $mgrRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_manager" } | Select-Object -First 1
    $finRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_finance" } | Select-Object -First 1
    $coordRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_coordinator" } | Select-Object -First 1
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" `
        -Body (@{ username = "sup.mgr.$ts"; password = $userPwd; fullName = "Mgr"; organizationRoleId = $mgrRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" `
        -Body (@{ username = "sup.fin.$ts"; password = $userPwd; fullName = "Fin"; organizationRoleId = $finRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" `
        -Body (@{ username = "sup.coord.$ts"; password = $userPwd; fullName = "Coord"; organizationRoleId = $coordRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "sup.mgr.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieMgr | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "sup.fin.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieFin | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "sup.coord.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieCoord | Out-Null

    $noBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (@{ name = "No Bank Supplier"; registrationNumber = "123456782" } | ConvertTo-Json -Compress) -CookieFile $cookieOa
    Write-Result "SUP-001" "POST without bank fields -> 201" ($noBank.StatusCode -eq 201) "HTTP $($noBank.StatusCode)"

    $partialBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (@{ name = "Partial"; registrationNumber = "123456782"; bankNumber = "12" } | ConvertTo-Json -Compress) -CookieFile $cookieOa
    Write-Result "SUP-001b" "Partial bank details -> 400" ($partialBank.StatusCode -eq 400) "HTTP $($partialBank.StatusCode)"

    $badBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody @{ bankNumber = "XY" }) -CookieFile $cookieOa
    Write-Result "SUP-002" "Non-digit bank number -> 400" ($badBank.StatusCode -eq 400) "HTTP $($badBank.StatusCode)"

    $finCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody) -CookieFile $cookieFin
    Write-Result "SUP-003" "Finance without create -> 403" ($finCreate.StatusCode -eq 403) "HTTP $($finCreate.StatusCode)"

    $create = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody @{ name = "Alpha Supplies" }) -CookieFile $cookieOa
    $supId = Get-JsonField $create.Content "supplier.id"
    $supCode = Get-JsonField $create.Content "supplier.supplierCode"
    $supVer = Get-JsonField $create.Content "supplier.version"
    Write-Result "SUP-004" "OrgAdmin creates supplier with bank" ($create.StatusCode -eq 201 -and $null -ne $supCode) "code=$supCode"

    $mgrList = Invoke-CurlJson -Uri "$baseApi/api/v1/org/suppliers" -CookieFile $cookieMgr
    $mgrCount = @(Get-JsonField $mgrList.Content "suppliers").Count
    Write-Result "SUP-005" "Manager lists suppliers" ($mgrList.StatusCode -eq 200 -and $mgrCount -ge 1) "count=$mgrCount"

    $mgrGet = Invoke-CurlJson -Uri "$baseApi/api/v1/org/suppliers/$supId" -CookieFile $cookieMgr
    $mgrBank = Get-JsonField $mgrGet.Content "supplier.bankNumber"
    Write-Result "SUP-006" "Manager GET includes bank fields" ($mgrGet.StatusCode -eq 200 -and $mgrBank -eq "12") ""

    $finPatch = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId" `
        -Body (@{ bankNumber = "10"; reason = "Finance bank update" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieFin -Headers @{ "If-Match" = "$supVer" }
    $supVer = Get-JsonField $finPatch.Content "supplier.version"
    Write-Result "SUP-007" "Finance edits bank via suppliers.edit" ($finPatch.StatusCode -eq 200) "HTTP $($finPatch.StatusCode)"

    $coordPatch = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId" `
        -Body (@{ phone = "03-1111111" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieCoord -Headers @{ "If-Match" = "$supVer" }
    Write-Result "SUP-008" "Coordinator without edit -> 403" ($coordPatch.StatusCode -eq 403) "HTTP $($coordPatch.StatusCode)"

    $badVer = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId" `
        -Body (@{ phone = "03-2222222" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOa -Headers @{ "If-Match" = "999" }
    $verCode = Get-JsonField $badVer.Content "code"
    Write-Result "SUP-009" "VERSION_CONFLICT on stale If-Match" ($badVer.StatusCode -eq 409 -and $verCode -eq "VERSION_CONFLICT") "code=$verCode"

    $deact = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId/deactivate" `
        -Body (@{ reason = "Test deactivate supplier" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOa -Headers @{ "If-Match" = "$supVer" }
    $supVer = Get-JsonField $deact.Content "supplier.version"
    Write-Result "SUP-010" "OrgAdmin deactivates supplier" ($deact.StatusCode -eq 200) "HTTP $($deact.StatusCode)"

    $restore = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId/restore" `
        -Body (@{ reason = "Test restore supplier" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOa -Headers @{ "If-Match" = "$supVer" }
    Write-Result "SUP-011" "OrgAdmin restores supplier" ($restore.StatusCode -eq 200) "HTTP $($restore.StatusCode)"

    $finDeact = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/suppliers/$supId/deactivate" `
        -Body (@{ reason = "Finance deactivate attempt" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieFin -Headers @{ "If-Match" = "$(Get-JsonField $restore.Content 'supplier.version')" }
    Write-Result "SUP-012" "Finance without deactivate -> 403" ($finDeact.StatusCode -eq 403) "HTTP $($finDeact.StatusCode)"

    $emptyName = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody @{ name = "  " }) -CookieFile $cookieOa
    Write-Result "SUP-013" "Empty supplier name -> 400" ($emptyName.StatusCode -eq 400) "HTTP $($emptyName.StatusCode)"

    $noBankTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='bank_accounts');" 2>&1
    Write-Result "SUP-014" "bank_accounts table does not exist" ($noBankTable -match 'f') ""

    $supTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='suppliers');" 2>&1
    Write-Result "SUP-015" "suppliers table exists" ($supTable -match 't') ""

    $bankCols = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='suppliers' AND column_name IN ('bank_number','branch_number','account_number','account_holder_name');" 2>&1
    Write-Result "SUP-016" "suppliers has embedded bank columns" ($bankCols -match '4') "cols=$bankCols"

    $noReg = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (@{
        name = "Supplier Test"
        bankNumber = "12"
        branchNumber = "345"
        accountNumber = "1234567"
        accountHolderName = "Holder"
    } | ConvertTo-Json -Compress) -CookieFile $cookieOa
    Write-Result "SUP-017" "POST without registration number -> 400" ($noReg.StatusCode -eq 400) "HTTP $($noReg.StatusCode)"

    $shortReg = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody @{ registrationNumber = "12345678" }) -CookieFile $cookieOa
    Write-Result "SUP-018" "Registration fewer than 9 digits -> 400" ($shortReg.StatusCode -eq 400) "HTTP $($shortReg.StatusCode)"

    $badReg = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (New-SupplierBody @{ registrationNumber = "123456789" }) -CookieFile $cookieOa
    Write-Result "SUP-019" "Invalid registration checksum -> 400" ($badReg.StatusCode -eq 400) "HTTP $($badReg.StatusCode)"

} finally { Pop-Location }

$passed = ($results | Where-Object { $_.Passed }).Count
Write-Host "`n=== Supplier Card Verification: $passed / $($results.Count) PASS ===" -ForegroundColor Cyan
if ($passed -lt $results.Count) { exit 1 }
