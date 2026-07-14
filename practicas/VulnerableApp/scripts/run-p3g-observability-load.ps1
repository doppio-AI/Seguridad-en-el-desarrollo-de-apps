param(
    [string]$BaseUrl = "http://localhost:5088",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

if ([string]::IsNullOrWhiteSpace($env:P3G_TEST_PASSWORD)) {
    throw "Defina P3G_TEST_PASSWORD con la contrasena del usuario de prueba."
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "..\evidencias\P3G-Continuacion\load-results.json"
}

$testRunId = "P3G-" + (Get-Date -Format "yyyyMMdd-HHmmss")
$startedAt = [DateTimeOffset]::Now
$script:missingCorrelationIds = 0
$script:sampleCorrelationId = $null
$script:unexpectedResponses = 0
$script:cookieContainers = @{}

function New-P3GClient {
    param([string]$RunId)

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $true
    $handler.UseCookies = $true
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.BaseAddress = [Uri]::new($BaseUrl)
    $client.Timeout = [TimeSpan]::FromSeconds(30)
    $client.DefaultRequestHeaders.Add("X-Test-Run-ID", $RunId)
    $script:cookieContainers[$client.GetHashCode()] = $handler.CookieContainer
    return $client
}

function Enable-LocalHttpSessionCookie {
    param([System.Net.Http.HttpClient]$Client)

    if (-not $Client.BaseAddress.IsLoopback -or $Client.BaseAddress.Scheme -ne "http") {
        return
    }

    $httpsUri = [UriBuilder]::new($Client.BaseAddress)
    $httpsUri.Scheme = "https"
    $httpsUri.Port = 443
    $container = $script:cookieContainers[$Client.GetHashCode()]
    foreach ($cookie in $container.GetCookies($httpsUri.Uri)) {
        if ($cookie.Name -like ".AspNetCore.Session*") {
            $cookie.Secure = $false
        }
    }
}

function Register-CorrelationId {
    param([System.Net.Http.HttpResponseMessage]$Response)

    if ($Response.Headers.Contains("X-Correlation-ID")) {
        $correlationId = ($Response.Headers.GetValues("X-Correlation-ID") | Select-Object -First 1)
        if ([string]::IsNullOrWhiteSpace($script:sampleCorrelationId)) {
            $script:sampleCorrelationId = $correlationId
        }
    }
    else {
        $script:missingCorrelationIds++
    }
}

function Invoke-P3GGet {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [int[]]$ExpectedStatus = @(200)
    )

    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    Register-CorrelationId $response
    $statusCode = [int]$response.StatusCode
    $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $response.Dispose()

    if ($ExpectedStatus -notcontains $statusCode) {
        $script:unexpectedResponses++
    }

    return [pscustomobject]@{
        StatusCode = $statusCode
        Content = $content
    }
}

function Invoke-P3GForm {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path,
        [hashtable]$Fields,
        [int[]]$ExpectedStatus = @(200)
    )

    $values = [System.Collections.Generic.Dictionary[string, string]]::new()
    foreach ($key in $Fields.Keys) {
        $values.Add([string]$key, [string]$Fields[$key])
    }

    $formContent = [System.Net.Http.FormUrlEncodedContent]::new($values)
    $response = $Client.PostAsync($Path, $formContent).GetAwaiter().GetResult()
    Register-CorrelationId $response
    $statusCode = [int]$response.StatusCode
    $response.Dispose()
    $formContent.Dispose()

    if ($ExpectedStatus -notcontains $statusCode) {
        $script:unexpectedResponses++
    }

    return $statusCode
}

function Get-AntiForgeryToken {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Path
    )

    $page = Invoke-P3GGet -Client $Client -Path $Path
    $match = [regex]::Match(
        $page.Content,
        'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if (-not $match.Success) {
        throw "No se encontro token antiforgery en $Path."
    }

    return $match.Groups[1].Value
}

$generalClient = New-P3GClient $testRunId
$validLoginClient = New-P3GClient $testRunId
$invalidLoginClient = New-P3GClient $testRunId
$commentClient = New-P3GClient $testRunId

$counts = [ordered]@{
    HomeVisits = 0
    ValidSearches = 0
    EmptySearches = 0
    SpecialCharacterSearches = 0
    SqlInjectionSearches = 0
    ValidLogins = 0
    InvalidLogins = 0
    ValidComments = 0
    XssComments = 0
    ApiRequests = 0
    InvalidApiRequests = 0
    ControlledExceptions = 0
    UnhandledExceptions = 0
}

try {
    Write-Host "TestRunId: $testRunId"

    1..30 | ForEach-Object {
        Invoke-P3GGet $generalClient "/" | Out-Null
        $counts.HomeVisits++
    }
    Invoke-P3GGet $generalClient "/Home/Privacy" | Out-Null

    1..100 | ForEach-Object {
        $query = [Uri]::EscapeDataString("user$($_ % 3)")
        Invoke-P3GGet $generalClient "/Search?search=$query" | Out-Null
        $counts.ValidSearches++
    }

    1..20 | ForEach-Object {
        Invoke-P3GGet $generalClient "/Search?search=" | Out-Null
        $counts.EmptySearches++
    }

    $specialSearches = @("admin+test", "user@example.com", "nombre/apellido", "uno%dos")
    1..20 | ForEach-Object {
        $query = [Uri]::EscapeDataString($specialSearches[$_ % $specialSearches.Count])
        Invoke-P3GGet $generalClient "/Search?search=$query" | Out-Null
        $counts.SpecialCharacterSearches++
    }

    $sqlPayloads = @(
        "' OR '1'='1",
        "admin' UNION SELECT password FROM users--",
        "1; DROP TABLE Users",
        "' AND 1=1--"
    )
    1..30 | ForEach-Object {
        $query = [Uri]::EscapeDataString($sqlPayloads[$_ % $sqlPayloads.Count])
        Invoke-P3GGet $generalClient "/Search?search=$query" | Out-Null
        $counts.SqlInjectionSearches++
    }

    1..50 | ForEach-Object {
        $token = Get-AntiForgeryToken $validLoginClient "/Auth/Login"
        Invoke-P3GForm $validLoginClient "/Auth/Login" @{
            username = "admin"
            password = $env:P3G_TEST_PASSWORD
            __RequestVerificationToken = $token
        } | Out-Null
        Enable-LocalHttpSessionCookie $validLoginClient
        $counts.ValidLogins++
    }

    1..100 | ForEach-Object {
        $token = Get-AntiForgeryToken $invalidLoginClient "/Auth/Login"
        $username = if ($_ % 2 -eq 0) { "usuario-inexistente-$_" } else { "admin" }
        Invoke-P3GForm $invalidLoginClient "/Auth/Login" @{
            username = $username
            password = "invalid-test-value-$_"
            __RequestVerificationToken = $token
        } | Out-Null
        $counts.InvalidLogins++
    }

    $commentToken = Get-AntiForgeryToken $commentClient "/Comment"
    1..100 | ForEach-Object {
        Invoke-P3GForm $commentClient "/Comment/AddComment" @{
            comment = "Comentario valido de carga $_"
            __RequestVerificationToken = $commentToken
        } | Out-Null
        $counts.ValidComments++
    }

    $xssPayloads = @(
        "<script>alert(1)</script>",
        "<img src=x onerror=alert(1)>",
        "<svg onload=alert(1)>"
    )
    1..30 | ForEach-Object {
        Invoke-P3GForm $commentClient "/Comment/AddComment" @{
            comment = $xssPayloads[$_ % $xssPayloads.Count]
            __RequestVerificationToken = $commentToken
        } | Out-Null
        $counts.XssComments++
    }

    1..100 | ForEach-Object {
        Invoke-P3GGet $validLoginClient "/api/user/1" | Out-Null
        Invoke-P3GGet $validLoginClient "/api/users" | Out-Null
        $counts.ApiRequests += 2
    }

    1..10 | ForEach-Object {
        Invoke-P3GGet $validLoginClient "/api/user/999" @(403) | Out-Null
        Invoke-P3GGet $validLoginClient "/api/user/no-valido" @(400) | Out-Null
        $counts.InvalidApiRequests += 2
    }

    1..20 | ForEach-Object {
        Invoke-P3GGet $generalClient "/Home/ControlledException" @(422) | Out-Null
        $counts.ControlledExceptions++
    }

    1..10 | ForEach-Object {
        Invoke-P3GGet $generalClient "/Home/UnhandledException" @(500) | Out-Null
        $counts.UnhandledExceptions++
    }
}
finally {
    $generalClient.Dispose()
    $validLoginClient.Dispose()
    $invalidLoginClient.Dispose()
    $commentClient.Dispose()
}

$finishedAt = [DateTimeOffset]::Now
$result = [ordered]@{
    TestRunId = $testRunId
    BaseUrl = $BaseUrl
    StartedAt = $startedAt.ToString("o")
    FinishedAt = $finishedAt.ToString("o")
    DurationSeconds = [math]::Round(($finishedAt - $startedAt).TotalSeconds, 2)
    Counts = $counts
    MissingCorrelationIds = $script:missingCorrelationIds
    UnexpectedResponses = $script:unexpectedResponses
    SampleCorrelationId = $script:sampleCorrelationId
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$result | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host "Carga completada en $($result.DurationSeconds) segundos."
Write-Host "CorrelationId faltantes: $($result.MissingCorrelationIds)"
Write-Host "Respuestas inesperadas: $($result.UnexpectedResponses)"
Write-Host "Resultados: $OutputPath"
