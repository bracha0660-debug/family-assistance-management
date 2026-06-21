# Step 4.1 / §14 Family Card Verification Script
# Run from repo root: .\scripts\verify-step04_1.ps1

$ErrorActionPreference = "Stop"
$baseApi = "http://localhost:8080"
$ts = Get-Date -Format "yyyyMMddHHmmss"

$orgCode = "VERIF41-$ts"
$adminUser = "verif41.admin.$ts"
$adminPass = "VerifPass41-$ts!"
$coordUser = "verif41.coord.$ts"
$userPwd = "VerifUser41-$ts!"

$cookieSa = Join-Path $env:TEMP "fam-step041-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "fam-step041-oa-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "fam-step041-coord-$ts.txt"

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
        [System.IO.File]::WriteAllText($bodyFile, $Body, [System.Text.UTF8Encoding]::new($false))
        $args += @("-H", "Content-Type: application/json; charset=utf-8", "--data-binary", "@$bodyFile")
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
    $statusLine = $lines[-1].Trim()
    $content = ($lines[0..($lines.Length - 2)] -join "`n").Trim()
    return @{ Content = $content; StatusCode = [int]$statusLine }
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

function Login($cookieFile, $username, $password) {
    $body = (@{ username = $username; password = $password } | ConvertTo-Json -Compress)
    return Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body $body -CookieFile $cookieFile
}

function Get-OrgRoleIdByPreset($cookieFile, $presetKey) {
    $rolesResp = Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieFile
    $roles = @((Get-JsonField $rolesResp.Content "roles"))
    $match = $roles | Where-Object { $_.factoryPresetKey -eq $presetKey } | Select-Object -First 1
    return $match.id
}

function New-FamilyBody([hashtable]$Extra = @{}) {
    $body = @{
        familyLastName = "Test"
        bankNumber = "12"
        branchNumber = "345"
        accountNumber = "1234567"
        accountHolderName = "Test Holder"
    }
    foreach ($k in $Extra.Keys) { $body[$k] = $Extra[$k] }
    return ($body | ConvertTo-Json -Compress)
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord)) {
        if (Test-Path $c) { Remove-Item $c -Force -ErrorAction SilentlyContinue }
    }

    $saLogin = Login $cookieSa "superadmin" "ChangeMe123!"
    if ($saLogin.StatusCode -ne 200) { throw "SuperAdmin login failed" }

    $createOrg = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" `
        -Body (@{ name = "Verify 4.1 $ts"; code = $orgCode } | ConvertTo-Json -Compress) `
        -CookieFile $cookieSa
    $orgId = Get-JsonField $createOrg.Content "organization.id"

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" `
        -Body (@{ username = $adminUser; password = $adminPass; fullName = "Org Admin 41" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieSa | Out-Null

    Login $cookieOa $adminUser $adminPass | Out-Null
    $coordRoleId = Get-OrgRoleIdByPreset $cookieOa "preset_coordinator"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" `
        -Body (@{ username = $coordUser; password = $userPwd; fullName = "Coord 41"; organizationRoleId = $coordRoleId } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOa | Out-Null
    $coordLogin = Login $cookieCoord $coordUser $userPwd
    $coordId = Get-JsonField $coordLogin.Content "user.id"

    $f1 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Cohen" }) -CookieFile $cookieCoord
    $acc1 = Get-JsonField $f1.Content "family.accountingCode"
    Write-Result "4.1-01" "First family suggested accounting code = 1" ($f1.StatusCode -eq 201 -and $acc1 -eq 1) "acc=$acc1"

    $f2 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Levi" }) -CookieFile $cookieCoord
    $acc2 = Get-JsonField $f2.Content "family.accountingCode"
    Write-Result "4.1-02" "Second family suggested accounting code = 2" ($f2.StatusCode -eq 201 -and $acc2 -eq 2) "acc=$acc2"

    $f3 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Peretz"; accountingCode = 100 }) -CookieFile $cookieCoord
    $acc3 = Get-JsonField $f3.Content "family.accountingCode"
    Write-Result "4.1-03" "Manual accounting code 100 accepted" ($f3.StatusCode -eq 201 -and $acc3 -eq 100) "acc=$acc3"

    $f4 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Avraham" }) -CookieFile $cookieCoord
    $acc4 = Get-JsonField $f4.Content "family.accountingCode"
    Write-Result "4.1-04" "Auto after manual high = 101" ($f4.StatusCode -eq 201 -and $acc4 -eq 101) "acc=$acc4"

    $f5 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "David"; accountingCode = 50 }) -CookieFile $cookieCoord
    Write-Result "4.1-05" "Manual accounting 50 (gap) accepted" ($f5.StatusCode -eq 201) "HTTP $($f5.StatusCode)"

    $dup = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Dup Cohen"; accountingCode = 50 }) -CookieFile $cookieCoord
    $dupCode = Get-JsonField $dup.Content "code"
    Write-Result "4.1-06" "Duplicate accounting code rejected" ($dup.StatusCode -eq 409 -and $dupCode -eq "DUPLICATE_ACCOUNTING_CODE") "HTTP $($dup.StatusCode)"

    $noBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (@{ familyLastName = "No Bank" } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    Write-Result "4.1-07" "Create without bank fields allowed" ($noBank.StatusCode -eq 201) "HTTP $($noBank.StatusCode)"

    $noName = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "  " }) -CookieFile $cookieCoord
    Write-Result "4.1-08" "Whitespace-only familyLastName rejected" ($noName.StatusCode -eq 400) "HTTP $($noName.StatusCode)"

    $noFatherId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "No Father ID" }) -CookieFile $cookieCoord
    Write-Result "4.1-09" "Create without fatherIsraeliId accepted" ($noFatherId.StatusCode -eq 201) "HTTP $($noFatherId.StatusCode)"

    $badFather = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Bad Father ID"; fatherIsraeliId = "123456789" }) -CookieFile $cookieCoord
    Write-Result "4.1-10" "Invalid fatherIsraeliId rejected" ($badFather.StatusCode -eq 400) "HTTP $($badFather.StatusCode)"

    $goodFather = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Good Father ID"; fatherIsraeliId = "123456782" }) -CookieFile $cookieCoord
    Write-Result "4.1-11" "Valid fatherIsraeliId accepted" ($goodFather.StatusCode -eq 201) "HTTP $($goodFather.StatusCode)"

    $code1 = Get-JsonField $f1.Content "family.familyCode"
    Write-Result "4.1-12" "familyCode format F-000001" ($code1 -eq "F-000001") "code=$code1"

    $fam1Id = Get-JsonField $f1.Content "family.id"
    $fam1Ver = Get-JsonField $f1.Content "family.version"
    $patchAcc = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam1Id" `
        -Body (@{ accountingCode = 200; reason = "Accounting correction test" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieCoord -Headers @{ "If-Match" = "$fam1Ver" }
    $patchedAcc = Get-JsonField $patchAcc.Content "family.accountingCode"
    Write-Result "4.1-13" "PATCH accountingCode with reason" ($patchAcc.StatusCode -eq 200 -and $patchedAcc -eq 200) "acc=$patchedAcc"

    $bankQ = "SELECT column_name FROM information_schema.columns WHERE table_name='families' AND column_name='bank_number';"
    $bankRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $bankQ 2>&1
    Write-Result "4.1-14" "bank_number column exists on families" ($bankRows -match 'bank_number') ""

    $noBankTableQ = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='bank_accounts');"
    $noBankTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c $noBankTableQ 2>&1
    Write-Result "4.1-15" "bank_accounts table removed" ($noBankTable -match 'f') ""

    $acctCoord = Get-JsonField $f1.Content "family.accountingCoordinatorId"
    Write-Result "4.1-16" "accountingCoordinatorId set at create" ($acctCoord -eq $coordId) "coord=$acctCoord"

    $idxQ = "SELECT indexname FROM pg_indexes WHERE tablename='families' AND indexname='ux_families_org_acct_coord_code';"
    $idxRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $idxQ 2>&1
    Write-Result "4.1-17" "ux_families_org_acct_coord_code index exists" ($idxRows -match 'ux_families_org_acct_coord_code') ""

    $list = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieCoord
    $first = (Get-JsonField $list.Content "families") | Select-Object -First 1
    Write-Result "4.1-18" "List families returns familyLastName" ($list.StatusCode -eq 200 -and $null -ne $first.familyLastName) ""

    $badMother = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "Bad Mother ID"; motherIsraeliId = "12345678" }) -CookieFile $cookieCoord
    Write-Result "4.1-19" "Invalid motherIsraeliId rejected" ($badMother.StatusCode -eq 400) "HTTP $($badMother.StatusCode)"

    $trimName = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (New-FamilyBody @{ familyLastName = "  Golan  " }) -CookieFile $cookieCoord
    $trimmed = Get-JsonField $trimName.Content "family.familyLastName"
    Write-Result "4.1-20" "familyLastName trimmed on save" ($trimName.StatusCode -eq 201 -and $trimmed -eq "Golan") "name=$trimmed"

} finally {
    Pop-Location
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord)) {
        if (Test-Path $c) { Remove-Item $c -Force -ErrorAction SilentlyContinue }
    }
}

$passed = ($results | Where-Object { $_.Passed }).Count
$total = $results.Count
Write-Host "`n=== Step 4.1 / §14 Verification: $passed / $total PASS ===" -ForegroundColor Cyan
if ($passed -lt $total) { exit 1 }
