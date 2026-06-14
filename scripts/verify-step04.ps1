# Step 4 Verification Script
# Run from repo root: .\scripts\verify-step04.ps1
#
# SAFETY: Creates ONLY isolated disposable test data (org code VERIF4-*).
# Does NOT modify, overwrite, or bootstrap admins on existing user organizations.
# Test passwords are generated per run - never used on real accounts.

$ErrorActionPreference = "Stop"
$baseApi = "http://localhost:8080"
$baseWeb = "http://localhost:3000"
$ts = Get-Date -Format "yyyyMMddHHmmss"

$orgCodeA = "VERIF4-A-$ts"
$orgNameA = "Verification Step4 Org A $ts"
$adminUserA = "verif4.admin.a.$ts"
$adminPassA = "VerifPass4A-$ts!"

$orgCodeB = "VERIF4-B-$ts"
$orgNameB = "Verification Step4 Org B $ts"
$adminUserB = "verif4.admin.b.$ts"
$adminPassB = "VerifPass4B-$ts!"

$coordA1 = "verif4.coord.a1.$ts"
$coordA2 = "verif4.coord.a2.$ts"
$financeA = "verif4.finance.a.$ts"
$managerA = "verif4.manager.a.$ts"
$coordB = "verif4.coord.b.$ts"
$userPwd = "VerifUser4-$ts!"

$cookieSa = Join-Path $env:TEMP "fam-step04-sa-$ts.txt"
$cookieOaA = Join-Path $env:TEMP "fam-step04-oa-a-$ts.txt"
$cookieOaB = Join-Path $env:TEMP "fam-step04-oa-b-$ts.txt"
$cookieCoordA1 = Join-Path $env:TEMP "fam-step04-coord-a1-$ts.txt"
$cookieCoordA2 = Join-Path $env:TEMP "fam-step04-coord-a2-$ts.txt"
$cookieFinanceA = Join-Path $env:TEMP "fam-step04-finance-a-$ts.txt"
$cookieManagerA = Join-Path $env:TEMP "fam-step04-manager-a-$ts.txt"
$cookieCoordB = Join-Path $env:TEMP "fam-step04-coord-b-$ts.txt"

$cookies = @(
    $cookieSa, $cookieOaA, $cookieOaB,
    $cookieCoordA1, $cookieCoordA2,
    $cookieFinanceA, $cookieManagerA, $cookieCoordB
)

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

function Get-JsonArray($jsonText, $path) {
    $v = Get-JsonField $jsonText $path
    if ($null -eq $v) { return @() }
    return @($v)
}

function Login($cookieFile, $username, $password) {
    $body = (@{ username = $username; password = $password } | ConvertTo-Json -Compress)
    return Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/auth/login" -Body $body -CookieFile $cookieFile
}

function Create-User($cookieFile, $username, $fullName, $role) {
    $body = (@{ username = $username; password = $userPwd; fullName = $fullName; role = $role } | ConvertTo-Json -Compress)
    return Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/users" -Body $body -CookieFile $cookieFile
}

Write-Host "=== Step 4 Verification ===" -ForegroundColor Cyan
Write-Host "NOTE: Creates isolated test orgs VERIF4-A-* and VERIF4-B-* only. Does not touch existing organizations." -ForegroundColor Yellow

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    foreach ($c in $cookies) {
        if (Test-Path $c) { Remove-Item $c -Force -ErrorAction SilentlyContinue }
    }

    # 1: Health
    $health = Invoke-RestMethod -Uri "$baseApi/api/v1/health" -TimeoutSec 5
    Write-Result 1 "Health endpoint OK (regression)" ($health.status -eq "healthy") ""

    # 2: SuperAdmin login
    $saLogin = Login $cookieSa "superadmin" "ChangeMe123!"
    Write-Result 2 "SuperAdmin login (regression)" ($saLogin.StatusCode -eq 200) "HTTP $($saLogin.StatusCode)"
    if ($saLogin.StatusCode -ne 200) { exit 1 }

    # 3-4: Create two orgs
    $createA = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" `
        -Body (@{ name = $orgNameA; code = $orgCodeA } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgIdA = Get-JsonField $createA.Content "organization.id"
    Write-Result 3 "Create test org A (regression)" ($createA.StatusCode -eq 201 -and $orgIdA) "id=$orgIdA"

    $createB = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations" `
        -Body (@{ name = $orgNameB; code = $orgCodeB } | ConvertTo-Json -Compress) -CookieFile $cookieSa
    $orgIdB = Get-JsonField $createB.Content "organization.id"
    Write-Result 4 "Create test org B (regression)" ($createB.StatusCode -eq 201 -and $orgIdB) "id=$orgIdB"

    # 5-6: Bootstrap OrgAdmin in each org
    $bootA = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgIdA/admin" `
        -Body (@{ username = $adminUserA; password = $adminPassA; fullName = "Verif Admin A" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieSa
    Write-Result 5 "Bootstrap OrgAdmin A (regression)" ($bootA.StatusCode -eq 201) "HTTP $($bootA.StatusCode)"

    $bootB = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/admin/organizations/$orgIdB/admin" `
        -Body (@{ username = $adminUserB; password = $adminPassB; fullName = "Verif Admin B" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieSa
    Write-Result 6 "Bootstrap OrgAdmin B (regression)" ($bootB.StatusCode -eq 201) ""

    # 7-8: OrgAdmin logins
    $oaA = Login $cookieOaA $adminUserA $adminPassA
    Write-Result 7 "OrgAdmin A login" ($oaA.StatusCode -eq 200) ""

    $oaB = Login $cookieOaB $adminUserB $adminPassB
    Write-Result 8 "OrgAdmin B login" ($oaB.StatusCode -eq 200) ""

    # 9-12: OrgAdmin A creates Step 4 fixture users (2 coords + finance + manager)
    $c1 = Create-User $cookieOaA $coordA1 "Coord A1" "Coordinator"
    $coordA1Id = Get-JsonField $c1.Content "user.id"
    Write-Result 9 "Create Coordinator A1" ($c1.StatusCode -eq 201) ""

    $c2 = Create-User $cookieOaA $coordA2 "Coord A2" "Coordinator"
    $coordA2Id = Get-JsonField $c2.Content "user.id"
    Write-Result 10 "Create Coordinator A2" ($c2.StatusCode -eq 201) ""

    $fa = Create-User $cookieOaA $financeA "Finance A" "Finance"
    $financeAId = Get-JsonField $fa.Content "user.id"
    Write-Result 11 "Create Finance A" ($fa.StatusCode -eq 201) ""

    $ma = Create-User $cookieOaA $managerA "Manager A" "Manager"
    $managerAId = Get-JsonField $ma.Content "user.id"
    Write-Result 12 "Create Manager A" ($ma.StatusCode -eq 201) ""

    $cb = Create-User $cookieOaB $coordB "Coord B" "Coordinator"
    $coordBId = Get-JsonField $cb.Content "user.id"
    Write-Result 13 "Create Coordinator B" ($cb.StatusCode -eq 201) ""

    # Login all role users
    Write-Result 14 "Coordinator A1 login" ((Login $cookieCoordA1 $coordA1 $userPwd).StatusCode -eq 200) ""
    Write-Result 15 "Coordinator A2 login" ((Login $cookieCoordA2 $coordA2 $userPwd).StatusCode -eq 200) ""
    Write-Result 16 "Finance A login" ((Login $cookieFinanceA $financeA $userPwd).StatusCode -eq 200) ""
    Write-Result 17 "Manager A login" ((Login $cookieManagerA $managerA $userPwd).StatusCode -eq 200) ""
    Write-Result 18 "Coordinator B login" ((Login $cookieCoordB $coordB $userPwd).StatusCode -eq 200) ""

    # === RBAC: Families ===

    # 19: Anonymous /org/families -> 401
    $anonF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families"
    Write-Result 19 "Anonymous GET /org/families returns 401" ($anonF.StatusCode -eq 401) "HTTP $($anonF.StatusCode)"

    # 20: SuperAdmin GET /org/families -> 403
    $saF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieSa
    Write-Result 20 "SuperAdmin GET /org/families returns 403" ($saF.StatusCode -eq 403) "HTTP $($saF.StatusCode)"

    # 21: Finance GET /org/families -> 403 (not family viewer)
    $finF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieFinanceA
    Write-Result 21 "Finance GET /org/families returns 403" ($finF.StatusCode -eq 403) "HTTP $($finF.StatusCode)"

    # 22: Manager creating family -> 403
    $mgrCreateBody = (@{ headOfHouseholdName = "Manager Attempt" } | ConvertTo-Json -Compress)
    $mgrCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $mgrCreateBody -CookieFile $cookieManagerA
    Write-Result 22 "Manager POST /org/families returns 403" ($mgrCreate.StatusCode -eq 403) "HTTP $($mgrCreate.StatusCode)"

    # 23: OrgAdmin creating family -> 403
    $oaCreate = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $mgrCreateBody -CookieFile $cookieOaA
    Write-Result 23 "OrgAdmin POST /org/families returns 403" ($oaCreate.StatusCode -eq 403) "HTTP $($oaCreate.StatusCode)"

    # === Families happy path (Coordinator A1) ===

    # 24: Coordinator A1 creates family -> 201 with F-000001
    $famBody = (@{
        headOfHouseholdName = "Cohen Test Family"
        headIdNumber = "000000018"
        phone = "050-1234567"
        address = "Herzl St 1, Tel Aviv"
        householdSize = 4
        notes = "Verification test family"
    } | ConvertTo-Json -Compress)
    $fam1 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $famBody -CookieFile $cookieCoordA1
    $fam1Id = Get-JsonField $fam1.Content "family.id"
    $fam1Code = Get-JsonField $fam1.Content "family.familyCode"
    $fam1Version = Get-JsonField $fam1.Content "family.version"
    $fam1Coord = Get-JsonField $fam1.Content "family.assignedCoordinatorId"
    Write-Result 24 "Coordinator creates family (201)" ($fam1.StatusCode -eq 201 -and $fam1Code -eq "F-000001") "code=$fam1Code"

    # 25: family_code auto-assigned + coordinator pinned to actor
    Write-Result 25 "Family auto-pinned to creator coordinator" ($fam1Coord -eq $coordA1Id) "coord=$fam1Coord"

    # 26: Second family auto-increments code
    $famBody2 = (@{ headOfHouseholdName = "Levi Test"; householdSize = 2 } | ConvertTo-Json -Compress)
    $fam2 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $famBody2 -CookieFile $cookieCoordA1
    $fam2Code = Get-JsonField $fam2.Content "family.familyCode"
    $fam2Id = Get-JsonField $fam2.Content "family.id"
    $fam2Version = Get-JsonField $fam2.Content "family.version"
    Write-Result 26 "Second family auto-increments to F-000002" ($fam2.StatusCode -eq 201 -and $fam2Code -eq "F-000002") "code=$fam2Code"

    # 27: AUD-007 written
    $audQ = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-007' AND entity_id = '$fam1Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 27 "AUD-007 written on family create" ($audRows -match 'AUD-007') ""

    # 28: Coordinator A2 creates own family - counter shared org-wide
    $famBody3 = (@{ headOfHouseholdName = "Peretz Test"; householdSize = 3 } | ConvertTo-Json -Compress)
    $fam3 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $famBody3 -CookieFile $cookieCoordA2
    $fam3Code = Get-JsonField $fam3.Content "family.familyCode"
    Write-Result 28 "Org-wide counter: A2 family is F-000003" ($fam3.StatusCode -eq 201 -and $fam3Code -eq "F-000003") "code=$fam3Code"

    # 29: Org B counter is independent (starts at 1)
    $famBBody = (@{ headOfHouseholdName = "Test B"; householdSize = 1 } | ConvertTo-Json -Compress)
    $famB = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $famBBody -CookieFile $cookieCoordB
    $famBCode = Get-JsonField $famB.Content "family.familyCode"
    Write-Result 29 "Org B counter independent (F-000001)" ($famB.StatusCode -eq 201 -and $famBCode -eq "F-000001") "code=$famBCode"

    # === Israeli ID validation ===

    # 30: Invalid Israeli ID (bad checksum) - rejected
    $badIdBody = (@{ headOfHouseholdName = "Bad ID"; headIdNumber = "123456789" } | ConvertTo-Json -Compress)
    $badId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $badIdBody -CookieFile $cookieCoordA1
    $badIdErr = Get-JsonField $badId.Content "error"
    Write-Result 30 "Invalid Israeli ID checksum rejected" ($badId.StatusCode -eq 400 -and $badIdErr -ne $null) "HTTP $($badId.StatusCode) err=$badIdErr"

    # 31: Wrong length ID (8 digits)
    $shortIdBody = (@{ headOfHouseholdName = "Short ID"; headIdNumber = "12345678" } | ConvertTo-Json -Compress)
    $shortId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $shortIdBody -CookieFile $cookieCoordA1
    Write-Result 31 "Israeli ID wrong length rejected" ($shortId.StatusCode -eq 400) "HTTP $($shortId.StatusCode)"

    # 32: Null/missing ID accepted
    $noIdBody = (@{ headOfHouseholdName = "No ID Person" } | ConvertTo-Json -Compress)
    $noId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $noIdBody -CookieFile $cookieCoordA1
    Write-Result 32 "Missing Israeli ID accepted" ($noId.StatusCode -eq 201) "HTTP $($noId.StatusCode)"

    # 33: Empty string ID accepted (normalized to null)
    $emptyIdBody = (@{ headOfHouseholdName = "Empty ID"; headIdNumber = "" } | ConvertTo-Json -Compress)
    $emptyId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $emptyIdBody -CookieFile $cookieCoordA1
    Write-Result 33 "Empty Israeli ID treated as missing" ($emptyId.StatusCode -eq 201) "HTTP $($emptyId.StatusCode)"

    # 34: Another valid checksum - 123456782 (verified Luhn-valid 9-digit test ID)
    $validIdBody = (@{ headOfHouseholdName = "Valid ID"; headIdNumber = "123456782" } | ConvertTo-Json -Compress)
    $validId = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" -Body $validIdBody -CookieFile $cookieCoordA1
    Write-Result 34 "Valid Israeli ID checksum accepted" ($validId.StatusCode -eq 201) "HTTP $($validId.StatusCode)"

    # === Family edit (Coordinator) ===

    # 35: Coordinator A1 edits own family
    $editBody = (@{ phone = "052-9999999"; householdSize = 5 } | ConvertTo-Json -Compress)
    $edit = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam1Id" `
        -Body $editBody -CookieFile $cookieCoordA1 -Headers @{ "If-Match" = "$fam1Version" }
    $editPhone = Get-JsonField $edit.Content "family.phone"
    $fam1Version = Get-JsonField $edit.Content "family.version"
    Write-Result 35 "Coordinator edits own family (200)" ($edit.StatusCode -eq 200 -and $editPhone -eq "052-9999999") "HTTP $($edit.StatusCode)"

    # 36: AUD-008 written
    $audQ = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-008' AND entity_id = '$fam1Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 36 "AUD-008 written on family update" ($audRows -match 'AUD-008') ""

    # 37: Coordinator A2 cannot edit A1's family -> 403
    $crossEdit = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam1Id" `
        -Body $editBody -CookieFile $cookieCoordA2 -Headers @{ "If-Match" = "$fam1Version" }
    Write-Result 37 "Coordinator cannot edit family owned by other coord (403)" ($crossEdit.StatusCode -eq 403) "HTTP $($crossEdit.StatusCode)"

    # 38: Cross-org access blocked (org B coordinator vs org A family) -> 404 (cannot reveal existence)
    $crossOrg = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam1Id" `
        -Body $editBody -CookieFile $cookieCoordB -Headers @{ "If-Match" = "$fam1Version" }
    Write-Result 38 "Org B coordinator cannot edit org A family (404)" ($crossOrg.StatusCode -eq 404) "HTTP $($crossOrg.StatusCode)"

    # 39: Wrong If-Match -> VERSION_CONFLICT
    $badVer = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam1Id" `
        -Body (@{ phone = "x" } | ConvertTo-Json -Compress) -CookieFile $cookieCoordA1 -Headers @{ "If-Match" = "99" }
    $badVerErr = Get-JsonField $badVer.Content "code"
    Write-Result 39 "Family update wrong If-Match returns 409 VERSION_CONFLICT" ($badVer.StatusCode -eq 409 -and $badVerErr -eq "VERSION_CONFLICT") "HTTP $($badVer.StatusCode) code=$badVerErr"

    # === Family deactivate ===

    # 40: Reason required (short reason rejected)
    $shortReason = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam2Id/deactivate" `
        -Body (@{ reason = "xx" } | ConvertTo-Json -Compress) -CookieFile $cookieCoordA1 -Headers @{ "If-Match" = "$fam2Version" }
    Write-Result 40 "Family deactivate short reason returns 400" ($shortReason.StatusCode -eq 400) "HTTP $($shortReason.StatusCode)"

    # 41: Deactivate with reason
    $deact = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/families/$fam2Id/deactivate" `
        -Body (@{ reason = "Family moved away (verification test)" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieCoordA1 -Headers @{ "If-Match" = "$fam2Version" }
    $deactStatus = Get-JsonField $deact.Content "family.status"
    Write-Result 41 "Family deactivate (200)" ($deact.StatusCode -eq 200 -and $deactStatus -eq "inactive") "HTTP $($deact.StatusCode) status=$deactStatus"

    # 42: AUD-009 written with reason
    $audQ = "SELECT event_code, reason FROM audit_logs WHERE event_code = 'AUD-009' AND entity_id = '$fam2Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 42 "AUD-009 written on family deactivate with reason" (($audRows -match 'AUD-009') -and ($audRows -match 'verification test')) ""

    # === Assistance Types (Finance) ===

    # 43: Anonymous GET /org/assistance-types -> 401
    $anonT = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types"
    Write-Result 43 "Anonymous GET /org/assistance-types returns 401" ($anonT.StatusCode -eq 401) ""

    # 44: Coordinator GET /org/assistance-types -> 403
    $coordT = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types" -CookieFile $cookieCoordA1
    Write-Result 44 "Coordinator GET /org/assistance-types returns 403" ($coordT.StatusCode -eq 403) ""

    # 45: Coordinator POST /org/assistance-types -> 403
    $coordTC = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" `
        -Body (@{ typeCode = "X"; name = "x"; frequency = "monthly" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieCoordA1
    Write-Result 45 "Coordinator POST /org/assistance-types returns 403" ($coordTC.StatusCode -eq 403) ""

    # 46: OrgAdmin POST /org/assistance-types -> 403
    $oaTC = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" `
        -Body (@{ typeCode = "X"; name = "x"; frequency = "monthly" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOaA
    Write-Result 46 "OrgAdmin POST /org/assistance-types returns 403" ($oaTC.StatusCode -eq 403) ""

    # 47: Finance creates type
    $typeBody = (@{
        typeCode = "FOOD-MONTHLY"
        name = "Monthly food assistance"
        description = "Monthly food basket"
        defaultAmount = 500.50
        frequency = "monthly"
    } | ConvertTo-Json -Compress)
    $type1 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body $typeBody -CookieFile $cookieFinanceA
    $type1Id = Get-JsonField $type1.Content "assistanceType.id"
    $type1Code = Get-JsonField $type1.Content "assistanceType.typeCode"
    $type1Currency = Get-JsonField $type1.Content "assistanceType.currency"
    $type1Version = Get-JsonField $type1.Content "assistanceType.version"
    Write-Result 47 "Finance creates assistance type (201, ILS default)" ($type1.StatusCode -eq 201 -and $type1Code -eq "FOOD-MONTHLY" -and $type1Currency -eq "ILS") "HTTP $($type1.StatusCode) code=$type1Code currency=$type1Currency"

    # 48: AUD-010 written
    $audQ = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-010' AND entity_id = '$type1Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 48 "AUD-010 written on assistance type create" ($audRows -match 'AUD-010') ""

    # 49: Duplicate typeCode rejected
    $dupType = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body $typeBody -CookieFile $cookieFinanceA
    $dupTypeErr = Get-JsonField $dupType.Content "code"
    Write-Result 49 "Duplicate typeCode returns 409 DUPLICATE_TYPE_CODE" ($dupType.StatusCode -eq 409 -and $dupTypeErr -eq "DUPLICATE_TYPE_CODE") "HTTP $($dupType.StatusCode) code=$dupTypeErr"

    # 50: Invalid typeCode format (lowercase)
    $badCodeBody = (@{ typeCode = "food"; name = "x"; frequency = "monthly" } | ConvertTo-Json -Compress)
    $badCode = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body $badCodeBody -CookieFile $cookieFinanceA
    Write-Result 50 "Lowercase typeCode rejected" ($badCode.StatusCode -eq 400) "HTTP $($badCode.StatusCode)"

    # 51: Invalid frequency
    $badFreqBody = (@{ typeCode = "TEST-X"; name = "x"; frequency = "weekly" } | ConvertTo-Json -Compress)
    $badFreq = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body $badFreqBody -CookieFile $cookieFinanceA
    Write-Result 51 "Invalid frequency rejected" ($badFreq.StatusCode -eq 400) "HTTP $($badFreq.StatusCode)"

    # 52: Finance edits assistance type
    $editType = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/assistance-types/$type1Id" `
        -Body (@{ defaultAmount = 750.00; description = "Enhanced food basket" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieFinanceA -Headers @{ "If-Match" = "$type1Version" }
    $editTypeAmount = Get-JsonField $editType.Content "assistanceType.defaultAmount"
    $type1Version = Get-JsonField $editType.Content "assistanceType.version"
    Write-Result 52 "Finance edits assistance type (200)" ($editType.StatusCode -eq 200 -and [decimal]$editTypeAmount -eq 750.00) "HTTP $($editType.StatusCode) amount=$editTypeAmount"

    # 53: AUD-011 written
    $audQ = "SELECT event_code FROM audit_logs WHERE event_code = 'AUD-011' AND entity_id = '$type1Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 53 "AUD-011 written on assistance type update" ($audRows -match 'AUD-011') ""

    # 54: Create second type for deactivate
    $type2Body = (@{ typeCode = "RENT-ONETIME"; name = "Rent one-time assistance"; frequency = "one_time"; defaultAmount = 2000 } | ConvertTo-Json -Compress)
    $type2 = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" -Body $type2Body -CookieFile $cookieFinanceA
    $type2Id = Get-JsonField $type2.Content "assistanceType.id"
    $type2Version = Get-JsonField $type2.Content "assistanceType.version"
    Write-Result 54 "Second type created for deactivate" ($type2.StatusCode -eq 201) ""

    # 55: Finance deactivates type with reason
    $deactT = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/assistance-types/$type2Id/deactivate" `
        -Body (@{ reason = "Type no longer relevant (verification test)" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieFinanceA -Headers @{ "If-Match" = "$type2Version" }
    $deactTStatus = Get-JsonField $deactT.Content "assistanceType.status"
    Write-Result 55 "Finance deactivates type (200)" ($deactT.StatusCode -eq 200 -and $deactTStatus -eq "inactive") "HTTP $($deactT.StatusCode) status=$deactTStatus"

    # 56: AUD-012 written with reason
    $audQ = "SELECT event_code, reason FROM audit_logs WHERE event_code = 'AUD-012' AND entity_id = '$type2Id';"
    $audRows = docker compose exec -T postgres psql -U fam -d family_assistance -c $audQ 2>&1
    Write-Result 56 "AUD-012 written on type deactivate with reason" (($audRows -match 'AUD-012') -and ($audRows -match 'verification test')) ""

    # 57: Deactivate without reason
    $noReasonBody = (@{ reason = "ab" } | ConvertTo-Json -Compress)
    # Re-fetch a fresh active type version for this test
    $type1Latest = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types/$type1Id" -CookieFile $cookieFinanceA
    $type1LatestVersion = Get-JsonField $type1Latest.Content "assistanceType.version"
    $noReason = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/assistance-types/$type1Id/deactivate" `
        -Body $noReasonBody -CookieFile $cookieFinanceA -Headers @{ "If-Match" = "$type1LatestVersion" }
    Write-Result 57 "Type deactivate short reason returns 400" ($noReason.StatusCode -eq 400) "HTTP $($noReason.StatusCode)"

    # === Visibility / read-only ===

    # 58: Manager can READ families
    $mgrF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieManagerA
    $mgrFCount = (Get-JsonArray $mgrF.Content "families").Count
    Write-Result 58 "Manager can GET /org/families (200)" ($mgrF.StatusCode -eq 200 -and $mgrFCount -ge 3) "HTTP $($mgrF.StatusCode) count=$mgrFCount"

    # 59: Manager can READ assistance types
    $mgrT = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types" -CookieFile $cookieManagerA
    Write-Result 59 "Manager can GET /org/assistance-types (200)" ($mgrT.StatusCode -eq 200) "HTTP $($mgrT.StatusCode)"

    # 60: OrgAdmin can READ families
    $oaF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieOaA
    Write-Result 60 "OrgAdmin can GET /org/families (200)" ($oaF.StatusCode -eq 200) "HTTP $($oaF.StatusCode)"

    # 61: OrgAdmin can READ assistance types
    $oaT = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types" -CookieFile $cookieOaA
    Write-Result 61 "OrgAdmin can GET /org/assistance-types (200)" ($oaT.StatusCode -eq 200) "HTTP $($oaT.StatusCode)"

    # 62: Coordinator A1 sees only own families
    $a1Fam = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieCoordA1
    $a1FamList = Get-JsonArray $a1Fam.Content "families"
    $a1AllOwn = ($a1FamList | Where-Object { $_.assignedCoordinatorId -ne $coordA1Id }).Count -eq 0
    Write-Result 62 "Coordinator sees only own families" ($a1Fam.StatusCode -eq 200 -and $a1FamList.Count -ge 1 -and $a1AllOwn) "count=$($a1FamList.Count)"

    # 63: Manager sees all org A families (including A2's)
    $mgrFamiliesArr = Get-JsonArray $mgrF.Content "families"
    $a2Families = @($mgrFamiliesArr | Where-Object { $_.assignedCoordinatorId -eq $coordA2Id })
    Write-Result 63 "Manager sees all org families (including A2's)" ($a2Families.Count -ge 1) "a2_families=$($a2Families.Count)"

    # === Cross-org isolation ===

    # 64: Org B can't see org A families
    $bF = Invoke-CurlJson -Uri "$baseApi/api/v1/org/families" -CookieFile $cookieOaB
    $bFList = Get-JsonArray $bF.Content "families"
    $orgALeak = $bFList | Where-Object { $_.headOfHouseholdName -eq "Cohen Test Family" }
    Write-Result 64 "Org B cannot see org A families" ($bF.StatusCode -eq 200 -and $orgALeak.Count -eq 0) ""

    # 65: Org B GET org A type ID -> 404
    $bGetTypeA = Invoke-CurlJson -Uri "$baseApi/api/v1/org/assistance-types/$type1Id" -CookieFile $cookieOaB
    Write-Result 65 "Org B GET org A type returns 404" ($bGetTypeA.StatusCode -eq 404) "HTTP $($bGetTypeA.StatusCode)"

    # === Orphan prevention ===

    # 66: Try to disable Coordinator A1 (has active families) -> 409 COORDINATOR_HAS_ACTIVE_FAMILIES
    $coordA1Latest = Invoke-CurlJson -Uri "$baseApi/api/v1/org/users" -CookieFile $cookieOaA
    $coordA1Version = ((Get-JsonArray $coordA1Latest.Content "users") | Where-Object { $_.id -eq $coordA1Id }).version
    $tryDisable = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/users/$coordA1Id/disable" `
        -Body (@{ reason = "Attempt to disable with active families" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOaA -Headers @{ "If-Match" = "$coordA1Version" }
    $tryDisableErr = Get-JsonField $tryDisable.Content "code"
    Write-Result 66 "Cannot disable Coordinator with active families (409)" ($tryDisable.StatusCode -eq 409 -and $tryDisableErr -eq "COORDINATOR_HAS_ACTIVE_FAMILIES") "HTTP $($tryDisable.StatusCode) code=$tryDisableErr"

    # 67: Manager has no families - can be disabled
    $managerLatest = ((Get-JsonArray $coordA1Latest.Content "users") | Where-Object { $_.id -eq $managerAId }).version
    $disableMgr = Invoke-CurlJson -Method PATCH -Uri "$baseApi/api/v1/org/users/$managerAId/disable" `
        -Body (@{ reason = "Manager not needed (verification test)" } | ConvertTo-Json -Compress) `
        -CookieFile $cookieOaA -Headers @{ "If-Match" = "$managerLatest" }
    Write-Result 67 "Manager (no families) can be disabled" ($disableMgr.StatusCode -eq 200) "HTTP $($disableMgr.StatusCode)"

    # === Frequency / amount edge cases ===

    # 68: Negative amount rejected
    $negAmt = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/assistance-types" `
        -Body (@{ typeCode = "NEG-AMT"; name = "x"; frequency = "monthly"; defaultAmount = -10 } | ConvertTo-Json -Compress) `
        -CookieFile $cookieFinanceA
    Write-Result 68 "Negative defaultAmount rejected" ($negAmt.StatusCode -eq 400) "HTTP $($negAmt.StatusCode)"

    # 69: Family households size out of range
    $bigSize = Invoke-CurlJson -Method POST -Uri "$baseApi/api/v1/org/families" `
        -Body (@{ headOfHouseholdName = "Big"; householdSize = 100 } | ConvertTo-Json -Compress) `
        -CookieFile $cookieCoordA1
    Write-Result 69 "Household size > 50 rejected" ($bigSize.StatusCode -eq 400) "HTTP $($bigSize.StatusCode)"

    # 70: Activity log (OrgAdmin) shows AUD-007..AUD-012 entries
    $act = Invoke-CurlJson -Uri "$baseApi/api/v1/org/activity?limit=200" -CookieFile $cookieOaA
    $actEntries = Get-JsonArray $act.Content "entries"
    $codes = ($actEntries | ForEach-Object { $_.eventCode }) | Sort-Object -Unique
    $hasAll = @('AUD-007','AUD-008','AUD-009','AUD-010','AUD-011','AUD-012') | Where-Object { $codes -contains $_ }
    Write-Result 70 "Activity log includes AUD-007..AUD-012" ($act.StatusCode -eq 200 -and $hasAll.Count -eq 6) "found=$($hasAll -join ',')"

    # 71: No Step 5+ APIs exposed yet
    $noStep5 = $true
    foreach ($p in @("/api/v1/org/suppliers", "/api/v1/org/committee-decisions", "/api/v1/org/reports", "/api/v1/org/billing")) {
        $r = Invoke-CurlJson -Uri "$baseApi$p" -CookieFile $cookieOaA
        if ($r.StatusCode -ne 404) { $noStep5 = $false; Write-Host "  unexpected $p -> $($r.StatusCode)" -ForegroundColor Yellow }
    }
    Write-Result 71 "No Step 5+ APIs exposed" $noStep5 ""

    # 72: Frontend Hebrew RTL (regression)
    try {
        $html = Invoke-WebRequest -Uri $baseWeb -UseBasicParsing -TimeoutSec 10
        $rtl = $html.Content -match 'dir="rtl"' -and $html.Content -match 'lang="he"'
        Write-Result 72 "Frontend Hebrew RTL (regression)" $rtl ""
    } catch {
        Write-Result 72 "Frontend Hebrew RTL (regression)" $false "frontend unreachable"
    }

} finally {
    Pop-Location
    foreach ($c in $cookies) {
        if (Test-Path $c) { Remove-Item $c -Force -ErrorAction SilentlyContinue }
    }
}

$passed = ($results | Where-Object { $_.Passed }).Count
$total = $results.Count
Write-Host "`n=== Step 4 Verification: $passed / $total PASS ===" -ForegroundColor Cyan
if ($passed -lt $total) {
    Write-Host "`nFAILURES:" -ForegroundColor Red
    $results | Where-Object { -not $_.Passed } | ForEach-Object {
        Write-Host "  [$($_.Id)] $($_.Name)" -ForegroundColor Red
        if ($_.Detail) { Write-Host "       $($_.Detail)" -ForegroundColor Red }
    }
    exit 1
}
