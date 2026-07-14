param(
    [string]$LoadResultsPath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LoadResultsPath)) {
    $LoadResultsPath = Join-Path $PSScriptRoot "..\evidencias\P3G-Continuacion\load-results.json"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "..\evidencias\P3G-Continuacion\analysis-results.json"
}

$load = Get-Content -Raw $LoadResultsPath | ConvertFrom-Json
$start = [DateTimeOffset]::Parse($load.StartedAt)
$end = [DateTimeOffset]::Parse($load.FinishedAt).AddSeconds(5)
$logDirectory = Join-Path $PSScriptRoot "..\Logs"
$timestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz"
$culture = [Globalization.CultureInfo]::InvariantCulture
$lines = New-Object System.Collections.Generic.List[string]

Get-ChildItem $logDirectory -Filter "log-*.txt" | Sort-Object Name | ForEach-Object {
    Get-Content $_.FullName | ForEach-Object {
        if ($_ -match '^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2})') {
            $timestamp = [DateTimeOffset]::ParseExact(
                $Matches.timestamp,
                $timestampFormat,
                $culture)
            if ($timestamp -ge $start -and $timestamp -le $end) {
                $lines.Add($_)
            }
        }
    }
}

$levelCounts = [ordered]@{
    Information = ($lines | Where-Object { $_ -match '\[INF\]' }).Count
    Warning = ($lines | Where-Object { $_ -match '\[WRN\]' }).Count
    Error = ($lines | Where-Object { $_ -match '\[(ERR|FTL)\]' }).Count
}

$controllerCounts = @{}
$endpointCounts = @{}
$ipCounts = @{}
$correlationIds = [System.Collections.Generic.HashSet[string]]::new()
$slowestRequest = $null

foreach ($line in $lines) {
    if ($line -match '\b(?<controller>HomeController|SearchController|AuthController|CommentController|ApiController)\.') {
        $controller = $Matches.controller
        $controllerCounts[$controller] = 1 + [int]$controllerCounts[$controller]
    }

    if ($line -match 'HTTP (?<method>[A-Z]+) (?<path>\S+) respondio (?<status>\d+) en (?<duration>\d+) ms') {
        $endpoint = "$($Matches.method) $($Matches.path)"
        $endpointCounts[$endpoint] = 1 + [int]$endpointCounts[$endpoint]
        $duration = [int]$Matches.duration
        if ($null -eq $slowestRequest -or $duration -gt $slowestRequest.DurationMs) {
            $slowestRequest = [ordered]@{
                Endpoint = $endpoint
                StatusCode = [int]$Matches.status
                DurationMs = $duration
                LogLine = $line
            }
        }
    }

    if ($line -match '\| IP: (?<ip>[^ |]+)') {
        $ip = $Matches.ip
        $ipCounts[$ip] = 1 + [int]$ipCounts[$ip]
    }

    if ($line -match '\[(?<correlation>[A-Za-z0-9._-]{8,64})\]\s') {
        [void]$correlationIds.Add($Matches.correlation)
    }
}

$topController = $controllerCounts.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 1
$topEndpoint = $endpointCounts.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 1
$topIp = $ipCounts.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 1

$analysis = [ordered]@{
    TestRunId = $load.TestRunId
    Window = [ordered]@{
        StartedAt = $load.StartedAt
        FinishedAt = $load.FinishedAt
        ParsedLogLines = $lines.Count
    }
    Levels = $levelCounts
    TopController = [ordered]@{
        Name = $topController.Name
        Events = $topController.Value
    }
    TopEndpoint = [ordered]@{
        Name = $topEndpoint.Name
        Requests = $topEndpoint.Value
    }
    TopIp = [ordered]@{
        Address = $topIp.Name
        Events = $topIp.Value
    }
    FailedAuthentications = ($lines | Where-Object {
        $_ -match 'Autenticacion fallida|credenciales incompletas'
    }).Count
    SqlInjectionAttempts = ($lines | Where-Object {
        $_ -match 'Posible intento de SQL Injection'
    }).Count
    XssAttempts = ($lines | Where-Object {
        $_ -match 'Posible intento de XSS'
    }).Count
    ControlledExceptions = ($lines | Where-Object {
        $_ -match 'Excepcion controlada atendida'
    }).Count
    UnhandledExceptions = ($lines | Where-Object {
        $_ -match 'Excepcion no controlada en'
    }).Count
    DistinctCorrelationIds = $correlationIds.Count
    SampleCorrelationId = $load.SampleCorrelationId
    CorrelationIdLocated = $correlationIds.Contains([string]$load.SampleCorrelationId)
    SlowestRequest = $slowestRequest
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$analysis | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputPath -Encoding UTF8
$analysis | ConvertTo-Json -Depth 6
