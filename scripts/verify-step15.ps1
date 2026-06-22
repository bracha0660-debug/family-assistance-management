# §15 Committee Decisions / Suppliers / Payments Verification
# Run from repo root: .\scripts\verify-step15.ps1

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
    if ($CookieFile) { if (Test-Path $CookieFile) { $args += @("-b", $CookieFile) }; $args += @("-c", $CookieFile) }
    $bodyFile = $null
    if ($Body) { $bodyFile = [System.IO.Path]::GetTempFileName(); [System.IO.File]::WriteAllText($bodyFile, $Body); $args += @("-H", "Content-Type: application/json", "--data-binary", "@$bodyFile") }
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
    $body = @{ familyLastName = "CD Family"; bankNumber = "12"; branchNumber = "345"; accountNumber = "1234567"; accountHolderName = "Holder" }
    foreach ($k in $Extra.Keys) { $body[$k] = $Extra[$k] }
    return ($body | ConvertTo-Json -Compress)
}

$cookieSa = Join-Path $env:TEMP "s15-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "s15-oa-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "s15-coord-$ts.txt"
$cookieManager = Join-Path $env:TEMP "s15-mgr-$ts.txt"
$cookieFinance = Join-Path $env:TEMP "s15-fin-$ts.txt"
$userPwd = "S15User-$ts!"
$orgCode = "S15-$ts"

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord, $cookieManager, $cookieFinance)) { if (Test-Path $c) { Remove-Item $c -Force } }

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "superadmin"; password = "ChangeMe123!" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    $org = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body (@{ name = "S15 Org"; code = $orgCode } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgId = Get-JsonField $org.Content "organization.id"
    $adminUser = "s15.admin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body (@{ username = $adminUser; password = "S15Admin-$ts!"; fullName = "S15 Admin" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $adminUser; password = "S15Admin-$ts!" } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null

    $roles = Get-JsonField (Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieOa).Content "roles"
    $coordRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_coordinator" } | Select-Object -First 1
    $mgrRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_manager" } | Select-Object -First 1
    $finRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_finance" } | Select-Object -First 1
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "s15.coord.$ts"; password = $userPwd; fullName = "Coord"; organizationRoleId = $coordRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "s15.mgr.$ts"; password = $userPwd; fullName = "Mgr"; organizationRoleId = $mgrRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "s15.fin.$ts"; password = $userPwd; fullName = "Fin"; organizationRoleId = $finRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "s15.coord.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieCoord | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "s15.mgr.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieManager | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "s15.fin.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieFinance | Out-Null

    # Supplier (OrgAdmin creates — finance preset has view+edit only, not create)
    $supCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/suppliers" -Body (@{
        name = "Supplier XYZ"; registrationNumber = "123456782"; bankNumber = "12"; branchNumber = "345"; accountNumber = "9876543"; accountHolderName = "Supplier XYZ"
    } | ConvertTo-Json -Compress) -CookieFile $cookieOa
    $supplierId = Get-JsonField $supCreate.Content "supplier.id"
    Write-Result "S15-01" "OrgAdmin creates supplier with bank" ($supCreate.StatusCode -eq 201) "HTTP $($supCreate.StatusCode)"

    $supList = Invoke-CurlJson -Uri "$baseApi/api/v1/org/suppliers" -CookieFile $cookieManager
    Write-Result "S15-02" "Manager lists suppliers" ($supList.StatusCode -eq 200) "HTTP $($supList.StatusCode)"

    # Family + assistance type
    $fam = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (New-FamilyBody) -CookieFile $cookieCoord
    $familyId = Get-JsonField $fam.Content "family.id"
    $type = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body (@{ typeCode = "S15-T-$ts"; name = "Food"; frequency = "one_time" } | ConvertTo-Json -Compress) -CookieFile $cookieFinance
    $typeId = Get-JsonField $type.Content "assistanceType.id"

    # Committee decision
    $decision = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions" -Body (@{
        familyId = $familyId; meetingDate = "2026-07-01"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $decisionId = Get-JsonField $decision.Content "decision.id"
    $decisionVer = Get-JsonField $decision.Content "decision.version"
    Write-Result "S15-03" "Coordinator creates committee decision draft" ($decision.StatusCode -eq 201) "HTTP $($decision.StatusCode)"

    $item = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/items" -Body (@{
        assistanceTypeId = $typeId; amount = 1000; paymentTarget = "family"; paymentMethod = "bank_transfer"; isUrgent = $true
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decisionVer" }
    $itemUrgent = Get-JsonField $item.Content "item.isUrgent"
    $decisionVer = Get-JsonField $item.Content "decisionVersion"
    if ($null -eq $decisionVer) { $decisionVer = Get-JsonField $item.Content "decision.version" }
    Write-Result "S15-04" "Add assistance item (family + bank_transfer)" ($item.StatusCode -eq 201 -and $itemUrgent -eq $true) "HTTP $($item.StatusCode) isUrgent=$itemUrgent"

    $badItem = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/items" -Body (@{
        assistanceTypeId = $typeId; amount = 500; paymentTarget = "supplier"; paymentMethod = "bank_transfer"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decisionVer" }
    Write-Result "S15-05" "Supplier target without supplier rejected" ($badItem.StatusCode -eq 400) "HTTP $($badItem.StatusCode)"

    $submit = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/submit" -Body "{}" -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decisionVer" }
    $decisionVer = Get-JsonField $submit.Content "decision.version"
    Write-Result "S15-06" "Submit decision for approval" ($submit.StatusCode -eq 200) "HTTP $($submit.StatusCode)"

    $approve = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/approve" -Body (@{ reason = "Approved for payment test" } | ConvertTo-Json -Compress) -CookieFile $cookieManager -Headers @{ "If-Match" = "$decisionVer" }
    Write-Result "S15-07" "Manager approves decision" ($approve.StatusCode -eq 200) "HTTP $($approve.StatusCode)"

    $payments = Invoke-CurlJson -Uri "$baseApi/api/v1/org/payments" -CookieFile $cookieFinance
    $payItems = @(Get-JsonField $payments.Content "payments")
    Write-Result "S15-08" "Finance views payment queue" ($payments.StatusCode -eq 200 -and $payItems.Count -ge 1) "count=$($payItems.Count)"

    if ($payItems.Count -ge 1) {
        $payId = $payItems[0].id
        $payVer = $payItems[0].version
        $exec = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/payments/$payId/execute" -Body (@{ reason = "Execute test payment" } | ConvertTo-Json -Compress) -CookieFile $cookieFinance -Headers @{ "If-Match" = "$payVer" }
        Write-Result "S15-09" "Finance executes payment with complete bank" ($exec.StatusCode -eq 200) "HTTP $($exec.StatusCode)"
    } else {
        Write-Result "S15-09" "Finance executes payment with complete bank" $false "no payment items"
    }

    # Family without bank — add bank_transfer item blocked at item save
    $noBankFam = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (@{ familyLastName = "No Bank Fam" } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $noBankFamId = Get-JsonField $noBankFam.Content "family.id"
    $nbDecision = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions" -Body (@{
        familyId = $noBankFamId; meetingDate = "2026-07-02"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $nbDecisionId = Get-JsonField $nbDecision.Content "decision.id"
    $nbVer = Get-JsonField $nbDecision.Content "decision.version"
    $nbItem = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$nbDecisionId/items" -Body (@{
        assistanceTypeId = $typeId; amount = 200; paymentTarget = "family"; paymentMethod = "bank_transfer"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "$nbVer" }
    $nbCode = Get-JsonField $nbItem.Content "code"
    Write-Result "S15-13" "Add bank_transfer item without family bank -> 400" ($nbItem.StatusCode -eq 400 -and $nbCode -eq "INCOMPLETE_BANK_DETAILS") "HTTP $($nbItem.StatusCode) code=$nbCode"

    $coordPay = Invoke-CurlJson -Uri "$baseApi/api/v1/org/payments" -CookieFile $cookieCoord
    Write-Result "S15-10" "Coordinator without payments.view -> 403" ($coordPay.StatusCode -eq 403) "HTTP $($coordPay.StatusCode)"

    $supTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='suppliers');" 2>&1
    Write-Result "S15-11" "suppliers table exists" ($supTable -match 't') ""

    $cdTable = docker compose exec -T postgres psql -U fam -d family_assistance -t -c "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='committee_decisions');" 2>&1
    Write-Result "S15-12" "committee_decisions table exists" ($cdTable -match 't') ""

} finally { Pop-Location }

$passed = ($results | Where-Object { $_.Passed }).Count
Write-Host "`n=== §15 Verification: $passed / $($results.Count) PASS ===" -ForegroundColor Cyan
if ($passed -lt $results.Count) { exit 1 }
