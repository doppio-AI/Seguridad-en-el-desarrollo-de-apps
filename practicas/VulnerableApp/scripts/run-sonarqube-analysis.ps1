param(
    [string]$ProjectKey = "VulnerableApp",
    [string]$HostUrl = "http://localhost:9000"
)

if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
    throw "Define SONAR_TOKEN antes de ejecutar el analisis. Ejemplo: `$env:SONAR_TOKEN = 'token'"
}

$exclusions = "**/Migrations/**,**/wwwroot/lib/**,**/bin/**,**/obj/**"

dotnet sonarscanner begin `
    /k:$ProjectKey `
    /d:sonar.host.url=$HostUrl `
    /d:sonar.token=$env:SONAR_TOKEN `
    /d:sonar.exclusions=$exclusions `
    /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" `
    /d:sonar.cs.vstest.reportsPaths="**/*.trx"

dotnet build --no-incremental

$testProjects = Get-ChildItem -Recurse -Filter *.csproj |
    Where-Object { Select-String -Path $_.FullName -Pattern "Microsoft.NET.Test.Sdk" -Quiet }

foreach ($testProject in $testProjects) {
    dotnet test $testProject.FullName `
        --collect:"XPlat Code Coverage" `
        --logger "trx" `
        -- `
        DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
}

if ($testProjects.Count -eq 0) {
    Write-Host "No se encontraron proyectos de prueba. El analisis continuara sin cobertura."
}

dotnet sonarscanner end /d:sonar.token=$env:SONAR_TOKEN
