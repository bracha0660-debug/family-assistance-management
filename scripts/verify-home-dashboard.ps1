# Home Dashboard Verification (Phase 2 KPI + Phase 3 financial + Phase 4 trend + Phase 5 bottlenecks)
# Run from repo root: .\scripts\verify-home-dashboard.ps1

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

$cookieSa = Join-Path $env:TEMP "hd-sa-$ts.txt"
$cookieOa = Join-Path $env:TEMP "hd-oa-$ts.txt"
$cookieCoord = Join-Path $env:TEMP "hd-coord-$ts.txt"
$cookieManager = Join-Path $env:TEMP "hd-mgr-$ts.txt"
$cookieFinance = Join-Path $env:TEMP "hd-fin-$ts.txt"
$userPwd = "HDUser-$ts!"
$orgCode = "HD-$ts"

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in @($cookieSa, $cookieOa, $cookieCoord, $cookieManager, $cookieFinance)) { if (Test-Path $c) { Remove-Item $c -Force } }

    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "superadmin"; password = "ChangeMe123!" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    $org = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" -Body (@{ name = "HD Org"; code = $orgCode } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgId = Get-JsonField $org.Content "organization.id"
    $adminUser = "hd.admin.$ts"
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgId/admin" -Body (@{ username = $adminUser; password = "HDAdmin-$ts!"; fullName = "HD Admin" } | ConvertTo-Json -Compress) -CookieFile $cookieSa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = $adminUser; password = "HDAdmin-$ts!" } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null

    $roles = Get-JsonField (Invoke-CurlJson -Uri "$baseApi/api/v1/org/roles" -CookieFile $cookieOa).Content "roles"
    $coordRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_coordinator" } | Select-Object -First 1
    $mgrRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_manager" } | Select-Object -First 1
    $finRole = $roles | Where-Object { $_.factoryPresetKey -eq "preset_finance" } | Select-Object -First 1
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "hd.coord.$ts"; password = $userPwd; fullName = "Coord"; organizationRoleId = $coordRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "hd.mgr.$ts"; password = $userPwd; fullName = "Mgr"; organizationRoleId = $mgrRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body (@{ username = "hd.fin.$ts"; password = $userPwd; fullName = "Fin"; organizationRoleId = $finRole.id } | ConvertTo-Json -Compress) -CookieFile $cookieOa | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "hd.coord.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieCoord | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "hd.mgr.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieManager | Out-Null
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body (@{ username = "hd.fin.$ts"; password = $userPwd } | ConvertTo-Json -Compress) -CookieFile $cookieFinance | Out-Null

    # HD-01: home.widgets present with backward compat fields
    $dashMgr = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieManager
    $hasHome = $null -ne (Get-JsonField $dashMgr.Content "home")
    $hasWidgets = $null -ne (Get-JsonField $dashMgr.Content "home.widgets")
    $hasAwaiting = $null -ne (Get-JsonField $dashMgr.Content "awaitingMyAction")
    $hasSections = $null -ne (Get-JsonField $dashMgr.Content "sections")
    Write-Result "HD-01" "Home contract + backward compat" ($dashMgr.StatusCode -eq 200 -and $hasHome -and $hasWidgets -and $hasAwaiting -and $hasSections) "HTTP $($dashMgr.StatusCode)"

    # HD-02: Manager has kpi_cards widget with awaiting_approval card
    $widgets = @(Get-JsonField $dashMgr.Content "home.widgets")
    $kpiWidget = $widgets | Where-Object { $_.type -eq "kpi_cards" } | Select-Object -First 1
    $mgrCards = @($kpiWidget.data.cards)
    $awaitingCard = $mgrCards | Where-Object { $_.kpiKey -eq "awaiting_approval" } | Select-Object -First 1
    Write-Result "HD-02" "Manager kpi_cards with awaiting_approval" ($null -ne $kpiWidget -and $null -ne $awaitingCard) "cards=$($mgrCards.Count)"

    # HD-03: Coordinator has drafts KPI, no awaiting_execution
    $dashCoord = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieCoord
    $coordWidgets = @(Get-JsonField $dashCoord.Content "home.widgets")
    $coordKpi = $coordWidgets | Where-Object { $_.type -eq "kpi_cards" } | Select-Object -First 1
    $coordCards = @($coordKpi.data.cards)
    $draftsCard = $coordCards | Where-Object { $_.kpiKey -eq "drafts" } | Select-Object -First 1
    $execCard = $coordCards | Where-Object { $_.kpiKey -eq "awaiting_execution" } | Select-Object -First 1
    Write-Result "HD-03" "Coordinator drafts KPI, no awaiting_execution" ($null -ne $draftsCard -and $null -eq $execCard) "cards=$($coordCards.Count)"

    # HD-04: Finance has awaiting_execution KPI
    $dashFin = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieFinance
    $finWidgets = @(Get-JsonField $dashFin.Content "home.widgets")
    $finKpi = $finWidgets | Where-Object { $_.type -eq "kpi_cards" } | Select-Object -First 1
    $finCards = @($finKpi.data.cards)
    $finExec = $finCards | Where-Object { $_.kpiKey -eq "awaiting_execution" } | Select-Object -First 1
    Write-Result "HD-04" "Finance awaiting_execution KPI" ($null -ne $finExec) "cards=$($finCards.Count)"

    # Setup: family + draft decision + submit for count match
    $fam = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body (@{
        familyLastName = "HD Family"; bankNumber = "12"; branchNumber = "345"; accountNumber = "1234567"; accountHolderName = "Holder"
    } | ConvertTo-Json -Compress) -CookieFile $cookieCoord
    $familyId = Get-JsonField $fam.Content "family.id"
    $typeCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body (@{
        typeCode = "HD-T-$ts"; name = "Food"; frequency = "one_time"
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
    Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/committee-decisions/$decisionId/submit" -Body "{}" -CookieFile $cookieCoord -Headers @{ "If-Match" = "$decVersion" } | Out-Null

    # HD-05: KPI count matches filtered list (manager submitted)
    $dashMgr2 = Invoke-CurlJson -Uri "$baseApi/api/v1/org/workflow/dashboard" -CookieFile $cookieManager
    $widgets2 = @(Get-JsonField $dashMgr2.Content "home.widgets")
    $kpi2 = $widgets2 | Where-Object { $_.type -eq "kpi_cards" } | Select-Object -First 1
    $awaiting2 = ($kpi2.data.cards | Where-Object { $_.kpiKey -eq "awaiting_approval" } | Select-Object -First 1)
    $navSection = $awaiting2.navigationTarget.section
    $filtered = Invoke-CurlJson -Uri "$baseApi/api/v1/org/committee-decisions?section=$navSection" -CookieFile $cookieManager
    $filteredCount = @(Get-JsonField $filtered.Content "decisions").Count
    Write-Result "HD-05" "KPI count matches filtered list" ($awaiting2.count -eq $filteredCount -and $filteredCount -ge 1) "kpi=$($awaiting2.count) list=$filteredCount section=$navSection"

    # HD-06: Navigation target shape
    $hasTargetTab = $awaiting2.navigationTarget.targetTab -eq "decisions"
    Write-Result "HD-06" "Navigation target has targetTab" $hasTargetTab "targetTab=$($awaiting2.navigationTarget.targetTab)"

    # HD-07: statusSemantic on KPI cards (no presentation-oriented accent field)
    $draftsSemantic = ($coordCards | Where-Object { $_.kpiKey -eq "drafts" } | Select-Object -First 1).statusSemantic
    $execSemantic = $finExec.statusSemantic
    $noAccent = ($null -eq $draftsCard.accent) -and ($null -eq $finExec.accent)
    Write-Result "HD-07" "KPI uses statusSemantic not accent" ($draftsSemantic -eq "draft" -and $execSemantic -eq "pending_execution" -and $noAccent) "draft=$draftsSemantic exec=$execSemantic"

    # HD-08: financial_summary widget present for manager with 4 metrics
    $finWidget = $widgets | Where-Object { $_.type -eq "financial_summary" } | Select-Object -First 1
    $finMetrics = @($finWidget.data.metrics)
    Write-Result "HD-08" "Manager financial_summary with 4 metrics" ($null -ne $finWidget -and $finMetrics.Count -eq 4) "metrics=$($finMetrics.Count)"

    # HD-09: financial metric contract shape
    $approvedMetric = $finMetrics | Where-Object { $_.metricKey -eq "approved_this_month" } | Select-Object -First 1
    $hasAmount = $null -ne $approvedMetric.amount -and $null -ne $approvedMetric.statusSemantic
    $hasNav = $approvedMetric.navigationTarget.targetTab -eq "decisions"
    Write-Result "HD-09" "Financial metric contract shape" ($hasAmount -and $hasNav) "key=$($approvedMetric.metricKey) semantic=$($approvedMetric.statusSemantic)"

    # HD-10: Coordinator has financial_summary (mine-scoped visibility gate)
    $coordFin = $coordWidgets | Where-Object { $_.type -eq "financial_summary" } | Select-Object -First 1
    $coordFinMetrics = @($coordFin.data.metrics)
    Write-Result "HD-10" "Coordinator financial_summary present" ($null -ne $coordFin -and $coordFinMetrics.Count -eq 4) "metrics=$($coordFinMetrics.Count)"

    # HD-11: home.generatedAt present for footer
    $genAt = Get-JsonField $dashMgr2.Content "home.generatedAt"
    Write-Result "HD-11" "home.generatedAt timestamp present" ($null -ne $genAt -and $genAt.Length -gt 10) "generatedAt=$genAt"

    # HD-12: monthly_trend widget with exactly 6 points
    $trendWidget = $widgets | Where-Object { $_.type -eq "monthly_trend" } | Select-Object -First 1
    $trendPoints = @($trendWidget.data.points)
    Write-Result "HD-12" "Manager monthly_trend with 6 points" ($null -ne $trendWidget -and $trendPoints.Count -eq 6) "points=$($trendPoints.Count)"

    # HD-13: monthly trend point contract shape
    $firstPoint = $trendPoints | Select-Object -First 1
    $hasMonthKey = $null -ne $firstPoint.monthKey -and $firstPoint.monthKey -match '^\d{4}-\d{2}$'
    $hasLabel = $null -ne $firstPoint.labelHe -and $firstPoint.labelHe.Length -gt 0
    $hasSubtitle = $trendWidget.data.subtitle.Length -gt 0
    Write-Result "HD-13" "Monthly trend contract shape" ($hasMonthKey -and $hasLabel -and $hasSubtitle) "monthKey=$($firstPoint.monthKey)"

    # HD-14: Coordinator has monthly_trend (financial visibility gate)
    $coordTrend = $coordWidgets | Where-Object { $_.type -eq "monthly_trend" } | Select-Object -First 1
    $coordTrendPoints = @($coordTrend.data.points)
    Write-Result "HD-14" "Coordinator monthly_trend present" ($null -ne $coordTrend -and $coordTrendPoints.Count -eq 6) "points=$($coordTrendPoints.Count)"

    # HD-15: Manager has bottlenecks widget with stale_submitted alert
    $bnWidget = $widgets | Where-Object { $_.type -eq "bottlenecks" } | Select-Object -First 1
    $bnAlerts = @($bnWidget.data.alerts)
    $staleSubmitted = $bnAlerts | Where-Object { $_.alertKey -eq "stale_submitted" } | Select-Object -First 1
    Write-Result "HD-15" "Manager bottlenecks with stale_submitted" ($null -ne $bnWidget -and $null -ne $staleSubmitted) "alerts=$($bnAlerts.Count)"

    # HD-16: Bottleneck alert contract shape (alertKey, count, navigationTarget.minAgeDays)
    $hasBnNav = $staleSubmitted.navigationTarget.targetTab -eq "decisions" -and $staleSubmitted.navigationTarget.minAgeDays -eq 7
    $hasSemantic = $staleSubmitted.statusSemantic -eq "pending_approval"
    Write-Result "HD-16" "Bottleneck alert contract shape" ($hasBnNav -and $hasSemantic -and $null -ne $staleSubmitted.count) "minAgeDays=$($staleSubmitted.navigationTarget.minAgeDays)"

    # HD-17: Finance has stale_awaiting_payment alert; fresh submitted excluded by minAgeDays=7
    $finBn = $finWidgets | Where-Object { $_.type -eq "bottlenecks" } | Select-Object -First 1
    $finBnAlerts = @($finBn.data.alerts)
    $stalePay = $finBnAlerts | Where-Object { $_.alertKey -eq "stale_awaiting_payment" } | Select-Object -First 1
    $coordBn = $coordWidgets | Where-Object { $_.type -eq "bottlenecks" } | Select-Object -First 1
    $coordBnAlerts = @($coordBn.data.alerts)
    $coordNoPay = ($null -eq ($coordBnAlerts | Where-Object { $_.alertKey -eq "stale_awaiting_payment" } | Select-Object -First 1))
    $navSection = $staleSubmitted.navigationTarget.section
    $agedList = Invoke-CurlJson -Uri "$baseApi/api/v1/org/committee-decisions?section=$navSection&minAgeDays=7" -CookieFile $cookieManager
    $agedCount = @(Get-JsonField $agedList.Content "decisions").Count
    $freshList = Invoke-CurlJson -Uri "$baseApi/api/v1/org/committee-decisions?section=$navSection" -CookieFile $cookieManager
    $freshCount = @(Get-JsonField $freshList.Content "decisions").Count
    Write-Result "HD-17" "Bottleneck visibility + minAgeDays filter" (
        $null -ne $stalePay -and $stalePay.navigationTarget.minAgeDays -eq 14 -and $coordNoPay -and $agedCount -eq 0 -and $freshCount -ge 1
    ) "financePay=$($stalePay.alertKey) aged=$agedCount fresh=$freshCount"

    $failed = @($results | Where-Object { -not $_.Passed })
    Write-Host ""
    Write-Host "Total: $($results.Count) | Passed: $($results.Count - $failed.Count) | Failed: $($failed.Count)"
    if ($failed.Count -gt 0) { exit 1 }
}
finally {
    Pop-Location
}
