# §14 Family Card Verification Script
# Run from repo root: .\scripts\verify-family-card.ps1

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

function New-FamilyBody([hashtable]$Extra = @{}) {
    $body = @{ familyLastName = "FC Test"; bankNumber = "12"; branchNumber = "345"; accountNumber = "1234567"; accountHolderName = "Holder" }
    foreach ($k in $Extra.Keys) { $body[$k] = $Extra[$k] }
    return ($body | ConvertTo-Json -Compress)
}

$cookieSa = Join-Path $env:TEMP "fc-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "fc-oa-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "fc-coord-$ts.txt"
$cookieFinance = Join-Path $env:TEMP "fc-fin-$ts.txt"
$userPwd = "FcUser-$ts!"
$orgCode = "FC-$ts"

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord, $cookieFinance)) { if (Test-Path $c) { Remove-Item $c -Force } }

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "superadmin"; password = "ChangeMe123!" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    $org = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body (@{ name = "FC Org"; code = $orgCode } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgId = Get-JsonField $org.Content "organization.id"
    $adminUser = "fc.admin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body (@{ username = $adminUser; password = "FcAdmin-$ts!"; fullName = "FC Admin" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $adminUser; password = "FcAdmin-$ts!" } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null

    $roles = Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieOa
    $coordRole = (Get-JsonField $roles.Content "roles") | Where-Object { $_.factoryPresetKey -eq "preset_coordinator" } | Select-Object -First 1
    $finRole = (Get-JsonField $roles.Content "roles") | Where-Object { $_.factoryPresetKey -eq "preset_finance" } | Select-Object -First 1
    $coordUser = "fc.coord.$ts"
    $finUser = "fc.fin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = $coordUser; password = $userPwd; fullName = "FC Coord"; organizationRoleId = $coordRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = $finUser; password = $userPwd; fullName = "FC Fin"; organizationRoleId = $finRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    $coordMe = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $coordUser; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $coordId = Get-JsonField $coordMe.Content "user.id"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $finUser; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieFinance | Out-Null

    $suggest = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families/suggested-accounting-code?coordinatorId=$coordId" -CookieFile $cookieCoord
    $suggested = Get-JsonField $suggest.Content "suggestedAccountingCode"
    Write-Result "FC-001" "Suggested accounting code for coordinator" ($suggest.StatusCode -eq 200 -and $suggested -eq 1) "suggested=$suggested"

    $noBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (@{ familyLastName = "X" } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    Write-Result "FC-002" "POST without bank fields -> 400" ($noBank.StatusCode -eq 400) "HTTP $($noBank.StatusCode)"

    $badBank = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (New-FamilyBody @{ bankNumber = "ABC" }) -CookieFile $cookieCoord
    Write-Result "FC-003" "Non-digit bank number -> 400" ($badBank.StatusCode -eq 400) "HTTP $($badBank.StatusCode)"

    $create = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (New-FamilyBody @{ familyLastName = "Cohen FC"; fatherIsraeliId = "000000018" }) -CookieFile $cookieCoord
    $famId = Get-JsonField $create.Content "family.id"
    $acctCoord = Get-JsonField $create.Content "family.accountingCoordinatorId"
    Write-Result "FC-004" "Create with bank + suggestion" ($create.StatusCode -eq 201 -and $acctCoord -eq $coordId) ""

    $dupId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (New-FamilyBody @{ familyLastName = "Dup"; fatherIsraeliId = "000000018" }) -CookieFile $cookieCoord
    Write-Result "FC-007" "Duplicate Israeli ID -> 409" ($dupId.StatusCode -eq 409) "HTTP $($dupId.StatusCode)"

    $ver = Get-JsonField $create.Content "family.version"
    $patch = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$famId" -Body (@{ bankNumber = "10"; reason = "Bank update test" } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "$ver" }
    Write-Result "FC-012" "PATCH bank with reason" ($patch.StatusCode -eq 200) "HTTP $($patch.StatusCode)"

    $badVer = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$famId" -Body (@{ phone = "1" } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "999" }
    $verCode = Get-JsonField $badVer.Content "code"
    Write-Result "FC-013" "VERSION_CONFLICT on stale If-Match" ($badVer.StatusCode -eq 409 -and $verCode -eq "VERSION_CONFLICT") "code=$verCode HTTP $($badVer.StatusCode)"

    $finPatch = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$famId" -Body (@{ bankNumber = "11"; reason = "x" } | ConvertTo-Json -Compress) -CookieFile $cookieFinance -Headers @{ "If-Match" = "$(Get-JsonField $patch.Content 'family.version')" }
    Write-Result "FC-018" "Finance without edit cannot PATCH bank" ($finPatch.StatusCode -eq 403) "HTTP $($finPatch.StatusCode)"

    $finGet = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families/$famId" -CookieFile $cookieFinance
    $finBank = Get-JsonField $finGet.Content "family.bankNumber"
    Write-Result "FC-017" "Finance can read bank fields" ($finGet.StatusCode -eq 200 -and $null -ne $finBank) ""

    $noBankTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='bank_accounts');" 2>&1
    Write-Result "FC-019" "bank_accounts table does not exist" ($noBankTable -match 'f') ""

    $noChildrenCol = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='families' AND column_name='number_of_children');" 2>&1
    Write-Result "FC-020" "number_of_children column removed" ($noChildrenCol -match 'f') ""

} finally {
    Pop-Location
}

$passed = ($results | Where-Object { $_.Passed }).Count
Write-Host "`n=== Family Card Verification: $passed / $($results.Count) PASS ===" -ForegroundColor Cyan
if ($passed -lt $results.Count) { exit 1 }
