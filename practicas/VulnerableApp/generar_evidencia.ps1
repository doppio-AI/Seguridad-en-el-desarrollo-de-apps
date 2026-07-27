# =============================================================================
# generar_evidencia.ps1
# Automatiza TODO el tráfico requerido por SEGG-U2-P3G-5 contra VulnerableApp.
#
# USO:
#   1. Corre la app con `dotnet run` en otra terminal y anota el puerto
#      (ej. http://localhost:5271).
#   2. Ajusta $BaseUrl abajo si tu puerto es distinto.
#   3. Ejecuta este script:  .\generar_evidencia.ps1
#   4. Ve a Seq (http://localhost:8081) a revisar los resultados.
#
# No borra logs existentes (tal como pide la práctica). Tarda unos minutos.
# =============================================================================

Add-Type -AssemblyName System.Web

$BaseUrl = "http://localhost:5271"
$Session = $null   # se llena con la sesión autenticada más abajo

function Show-Progress($texto) {
    Write-Host ""
    Write-Host "== $texto ==" -ForegroundColor Cyan
}

function Invoke-Safe {
    param([scriptblock]$Action)
    try { & $Action | Out-Null }
    catch { } # los 4xx/5xx esperados no deben detener el script
}

# -----------------------------------------------------------------------
# 1. NAVEGACIÓN GENERAL
# -----------------------------------------------------------------------
Show-Progress "Navegación general: 30 visitas a Home + todos los controladores"

for ($i = 1; $i -le 30; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing }
}
Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Home/Privacy" -UseBasicParsing }
Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Auth/Login" -UseBasicParsing }
Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Search" -UseBasicParsing }
Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Comment" -UseBasicParsing }
Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/users" -UseBasicParsing }
Write-Host "Listo." -ForegroundColor Green

# -----------------------------------------------------------------------
# 2. BÚSQUEDAS
# -----------------------------------------------------------------------
Show-Progress "Búsquedas: 100 válidas, 20 vacías, 20 caracteres especiales, 20 tipo SQL Injection"

$terminosValidos = @("admin", "user1", "user2", "test", "a", "us", "min", "e")
for ($i = 1; $i -le 100; $i++) {
    $t = $terminosValidos[$i % $terminosValidos.Count]
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Search?search=$t" -UseBasicParsing }
}

for ($i = 1; $i -le 20; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Search?search=" -UseBasicParsing }
}

$especiales = @("@#$%", "!!!???", "??%%&&", "<<>>", "aeiou-acentos", "comillas'dobles", '"comillas"', "%20%20", "null%00byte", "tab%09char")
for ($i = 1; $i -le 20; $i++) {
    $t = [System.Web.HttpUtility]::UrlEncode($especiales[$i % $especiales.Count])
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Search?search=$t" -UseBasicParsing }
}

$sqli = @(
    "' OR '1'='1", "admin'--", "' OR 1=1--", "1' UNION SELECT NULL--",
    "'; DROP TABLE Users--", "' UNION SELECT username, password FROM Users--"
)
for ($i = 1; $i -le 20; $i++) {
    $t = [System.Web.HttpUtility]::UrlEncode($sqli[$i % $sqli.Count])
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Search?search=$t" -UseBasicParsing }
}
Write-Host "Listo." -ForegroundColor Green

# -----------------------------------------------------------------------
# 3. AUTENTICACIÓN
# -----------------------------------------------------------------------
Show-Progress "Autenticación: 50 logins exitosos, 100 fallidos, usuarios inexistentes"

$usuariosValidos = @(
    @{u="admin"; p="admin123"},
    @{u="user1"; p="user123"},
    @{u="user2"; p="password123"}
)

for ($i = 1; $i -le 50; $i++) {
    $cred = $usuariosValidos[$i % $usuariosValidos.Count]
    $body = @{ username = $cred.u; password = $cred.p }
    Invoke-Safe {
        Invoke-WebRequest -Uri "$BaseUrl/Auth/Login" -Method POST -Body $body `
            -SessionVariable tempSession -UseBasicParsing
    }
}

# 80 fallidos con usuarios válidos pero password incorrecta
for ($i = 1; $i -le 80; $i++) {
    $cred = $usuariosValidos[$i % $usuariosValidos.Count]
    $body = @{ username = $cred.u; password = "clave-incorrecta-$i" }
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Auth/Login" -Method POST -Body $body -UseBasicParsing }
}

# 20 fallidos con usuarios inexistentes
for ($i = 1; $i -le 20; $i++) {
    $body = @{ username = "usuario_fantasma_$i"; password = "loquesea" }
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/Auth/Login" -Method POST -Body $body -UseBasicParsing }
}

# Guarda una sesión autenticada real para usarla en Comment/AddComment más abajo
$body = @{ username = "admin"; password = "admin123" }
Invoke-Safe {
    Invoke-WebRequest -Uri "$BaseUrl/Auth/Login" -Method POST -Body $body `
        -SessionVariable Session -UseBasicParsing
}
Write-Host "Listo." -ForegroundColor Green

# -----------------------------------------------------------------------
# 4. COMENTARIOS
# -----------------------------------------------------------------------
Show-Progress "Comentarios: 100 válidos, 30 con posible carga XSS"

$comentariosValidos = @(
    "Excelente servicio, muy recomendado.",
    "El armazón que compré es de buena calidad.",
    "Atención rápida y amable.",
    "Los precios son justos.",
    "Volveré pronto por mis lentes nuevos."
)
for ($i = 1; $i -le 100; $i++) {
    $c = $comentariosValidos[$i % $comentariosValidos.Count] + " ($i)"
    $body = @{ comment = $c }
    Invoke-Safe {
        Invoke-WebRequest -Uri "$BaseUrl/Comment/AddComment" -Method POST -Body $body `
            -WebSession $Session -UseBasicParsing
    }
}

$xssPayloads = @(
    "<script>alert(1)</script>",
    "<img src=x onerror=alert(1)>",
    "<svg onload=alert(1)>",
    "javascript:alert(document.cookie)",
    "<iframe src=javascript:alert(1)>"
)
for ($i = 1; $i -le 30; $i++) {
    $c = $xssPayloads[$i % $xssPayloads.Count]
    $body = @{ comment = $c }
    Invoke-Safe {
        Invoke-WebRequest -Uri "$BaseUrl/Comment/AddComment" -Method POST -Body $body `
            -WebSession $Session -UseBasicParsing
    }
}
Write-Host "Listo." -ForegroundColor Green

# -----------------------------------------------------------------------
# 5. API — mínimo 200 llamadas + recursos inexistentes + ids inválidos
# -----------------------------------------------------------------------
Show-Progress "API: 200+ llamadas, recursos inexistentes, IDs inválidos"

for ($i = 1; $i -le 150; $i++) {
    $id = ($i % 3) + 1
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/user/$id" -UseBasicParsing }
}
for ($i = 1; $i -le 50; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/users" -UseBasicParsing }
}
for ($i = 1; $i -le 15; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/user/999$i" -UseBasicParsing }  # inexistentes
}
for ($i = 1; $i -le 15; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/user/no-es-numero-$i" -UseBasicParsing }  # id inválido
}
Write-Host "Listo." -ForegroundColor Green

# -----------------------------------------------------------------------
# 6. EXCEPCIONES CONTROLADAS Y NO CONTROLADAS
# -----------------------------------------------------------------------
Show-Progress "Excepciones: 20 controladas, 10 no controladas"

for ($i = 1; $i -le 20; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/test/controlled-error" -UseBasicParsing }
}
for ($i = 1; $i -le 10; $i++) {
    Invoke-Safe { Invoke-WebRequest -Uri "$BaseUrl/api/test/uncontrolled-error" -UseBasicParsing }
}
Write-Host "Listo." -ForegroundColor Green

Write-Host ""
Write-Host "======================================================" -ForegroundColor Yellow
Write-Host " Tráfico generado. Ve a Seq (http://localhost:8081)"   -ForegroundColor Yellow
Write-Host " y a la carpeta Logs/ para recolectar tus evidencias." -ForegroundColor Yellow
Write-Host "======================================================" -ForegroundColor Yellow
