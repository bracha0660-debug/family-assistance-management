# Workflow Completion Phase Verification
# Run from repo root: .\scripts\verify-workflow.ps1

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

$cookieSa = Join-Path $env:TEMP "wf-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "wf-oa-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "wf-coord-$ts.txt"
$cookieManager = Join-Path $env:TEMP "wf-mgr-$ts.txt"
$cookieFinance = Join-Path $env:TEMP "wf-fin-$ts.txt"
$userPwd = "WFUser-$ts!"
$orgCode = "WF-$ts"

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord, $cookieManager, $cookieFinance)) { if (Test-Path $c) { Remove-Item $c -Force } }

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "superadmin"; password = "ChangeMe123!" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    $org = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body (@{ name = "WF Org"; code = $orgCode } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgId = Get-JsonField $org.Content "organization.id"
    $adminUser = "wf.admin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body (@{ username = $adminUser; password = "WFAdmin-$ts!"; fullName = "WF Admin" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $adminUser; password = "WFAdmin-$ts!" } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null

    $roles = Get-JsonField (Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieOa).Content "roles"
    $coordRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_coordinator" } | Select-Object -First 1
    $mgrRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_manager" } | Select-Object -First 1
    $finRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_finance" } | Select-Object -First 1
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "wf.coord.$ts"; password = $userPwd; fullName = "Coord"; organizationRoleId = $coordRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "wf.mgr.$ts"; password = $userPwd; fullName = "Mgr"; organizationRoleId = $mgrRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "wf.fin.$ts"; password = $userPwd; fullName = "Fin"; organizationRoleId = $finRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "wf.coord.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieCoord | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "wf.mgr.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieManager | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "wf.fin.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieFinance | Out-Null

    # WF-01: Dashboard API
    $dashMgr = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieManager
    Write-Result "WF-01" "Manager workflow dashboard" ($dashMgr.StatusCode -eq 200 -and (Get-JsonField $dashMgr.Content "awaitingMyAction")) "HTTP $($dashMgr.StatusCode)"

    # WF-02: OrgAdmin oversight (view dashboard, no workflow grants)
    $dashOa = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieOa
    $oaAwaiting = Get-JsonField $dashOa.Content "awaitingMyAction.totalAwaitingMyAction"
    Write-Result "WF-02" "OrgAdmin dashboard oversight" ($dashOa.StatusCode -eq 200) "awaiting=$oaAwaiting"

    # Setup family + decision (mirror verify-step15.ps1)
    $fam = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (@{
        familyLastName = "WF Family"; bankNumber = "12"; branchNumber = "345"; accountNumber = "1234567"; accountHolderName = "Holder"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $familyId = Get-JsonField $fam.Content "family.id"
    $typeCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body (@{
        typeCode = "WF-T-$ts"; name = "Food"; frequency = "one_time"
    } | ConvertTo-Json -Compress) -CookieFile $cookieFinance
    $typeId = Get-JsonField $typeCreate.Content "assistanceType.id"
    $dec = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions" -Body (@{
        familyId = $familyId; meetingDate = "2026-07-01"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $decisionId = Get-JsonField $dec.Content "decision.id"
    $decVersion = Get-JsonField $dec.Content "decision.version"
    $item = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/items" -Body (@{
        assistanceTypeId = $typeId; amount = 100; paymentTarget = "family"; paymentMethod = "check"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decVersion" }
    $decVersion = Get-JsonField $item.Content "decisionVersion"
    if ($null -eq $decVersion) { $decVersion = Get-JsonField $item.Content "decision.version" }
    $submit = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/submit" -Body "{}" -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decVersion" }
    $decVersion = Get-JsonField $submit.Content "decision.version"

    # WF-03: Suspend from submitted
    $suspend = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/suspend" -Body (@{ reason = "WF suspend reason for additional review" } | ConvertTo-Json -Compress) -CookieFile $cookieManager -Headers @{ "If-Match" = "$decVersion" }
    $suspendedStatus = Get-JsonField $suspend.Content "decision.status"
    Write-Result "WF-03" "Suspend from submitted" ($suspend.StatusCode -eq 200 -and $suspendedStatus -eq "suspended") "status=$suspendedStatus"

    # WF-04: Resume to approved
    $decVersion = Get-JsonField $suspend.Content "decision.version"
    $resume = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/resume" -Body (@{ reason = "WF resume approved for payment" } | ConvertTo-Json -Compress) -CookieFile $cookieManager -Headers @{ "If-Match" = "$decVersion" }
    $resumedStatus = Get-JsonField $resume.Content "decision.status"
    Write-Result "WF-04" "Resume to approved" ($resume.StatusCode -eq 200 -and $resumedStatus -eq "approved") "status=$resumedStatus"

    # WF-05: Finance on_hold after suspend (approved path)
    $decVersion = Get-JsonField $resume.Content "decision.version"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/suspend" -Body (@{ reason = "WF suspend for finance hold test" } | ConvertTo-Json -Compress) -CookieFile $cookieManager -Headers @{ "If-Match" = "$decVersion" } | Out-Null
    $payments = Invoke-CurlJson -Uri "$baseApi/api/v1/org/payments?section=finance_on_hold" -CookieFile $cookieFinance
    $onHoldCount = @(Get-JsonField $payments.Content "payments").Count
    Write-Result "WF-05" "Finance on_hold section" ($payments.StatusCode -eq 200 -and $onHoldCount -ge 1) "onHold=$onHoldCount"

    # WF-06: Execute blocked when on hold
    $payList = @(Get-JsonField $payments.Content "payments")
    if ($payList.Count -ge 1) {
        $payId = $payList[0].id
        $payVer = $payList[0].version
        $execBlocked = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/payments/$payId/execute" -Body "{}" -CookieFile $cookieFinance -Headers @{ "If-Match" = "$payVer" }
        Write-Result "WF-06" "Execute blocked on hold" ($execBlocked.StatusCode -eq 403) "HTTP $($execBlocked.StatusCode)"
    } else {
        Write-Result "WF-06" "Execute blocked on hold" $false "No on-hold payments available"
    }

    # WF-07: OrgAdmin cannot approve (Option A)
    $decNow = Invoke-CurlJson -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId" -CookieFile $cookieOa
    $oaApprove = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/resume" -Body "{}" -CookieFile $cookieOa -Headers @{ "If-Match" = "$(Get-JsonField $decNow.Content 'decision.version')" }
    Write-Result "WF-07" "OrgAdmin workflow action denied" ($oaApprove.StatusCode -eq 403) "HTTP $($oaApprove.StatusCode)"

    $failed = @($results | Where-Object { -not $_.Passed })
    Write-Host ""
    Write-Host "Total: $($results.Count) | Passed: $($results.Count - $failed.Count) | Failed: $($failed.Count)"
    if ($failed.Count -gt 0) { exit 1 }
}
finally {
    Pop-Location
}
