# Practica 3 - SonarQube en Docker y analisis de VulnerableApp

## Objetivo

Analizar el proyecto ASP.NET Core 10 `VulnerableApp` con SonarQube Community Edition, identificar hallazgos de seguridad/mantenibilidad y corregir al menos tres de ellos sin exponer credenciales.

## Proyecto analizado

- Ruta: `C:\Projects\cuatrimestre8\sdapps\SDA\practicas\VulnerableApp`
- Framework: `.NET 10`
- Tipo: ASP.NET Core MVC
- Herramienta: SonarScanner para .NET

## Preparacion de SonarQube

Desde la raiz de practicas:

```powershell
cd C:\Projects\cuatrimestre8\sdapps\SDA\practicas\sonarqube-infra
docker compose up -d
docker compose ps
docker compose logs -f sonarqube
```

Cuando el log indique que SonarQube esta operativo:

1. Abrir `http://localhost:9000`.
2. Entrar con `admin/admin`.
3. Cambiar la contrasena inicial.
4. Crear un proyecto local con:
   - Project display name: `VulnerableApp`
   - Project key: `VulnerableApp`
   - Main branch: `main`
5. Generar un token de analisis y guardarlo fuera del repositorio.

## Ejecucion del analisis

Instalar o actualizar SonarScanner:

```powershell
dotnet tool install --global dotnet-sonarscanner
# o, si ya existe:
dotnet tool update --global dotnet-sonarscanner
```

Definir el token solo en la terminal:

```powershell
$env:SONAR_TOKEN = "token_generado_en_sonarqube"
```

Ejecutar el analisis:

```powershell
cd C:\Projects\cuatrimestre8\sdapps\SDA\practicas\VulnerableApp
.\scripts\run-sonarqube-analysis.ps1
```

El script excluye `Migrations`, `bin`, `obj` y librerias de terceros en `wwwroot/lib`, porque son codigo generado, historial de base de datos o dependencias externas. El objetivo de esta practica es evaluar el codigo fuente mantenido por el equipo.

## Hallazgos corregidos

| Hallazgo | Archivo | Correccion aplicada |
| --- | --- | --- |
| Formularios POST sin proteccion CSRF | `Controllers/AuthController.cs`, `Controllers/CommentController.cs`, `Views/Auth/Login.cshtml`, `Views/Comment/Index.cshtml` | Se agrego `[ValidateAntiForgeryToken]` y `@Html.AntiForgeryToken()` en formularios. |
| Cookie de sesion con configuracion por defecto | `Program.cs` | Se configuro `HttpOnly`, `SameSite=Strict`, `SecurePolicy=Always`, `IsEssential` e inactividad de 20 minutos. |
| Lista estatica mutable para comentarios | `Controllers/CommentController.cs`, `Services/InMemoryCommentStore.cs` | Se movio el almacenamiento a un servicio singleton con bloqueo, limite de elementos y limite de longitud. |
| Entradas sin normalizacion | `Controllers/AuthController.cs`, `Controllers/SearchController.cs`, `Services/InMemoryCommentStore.cs` | Se valida `null`, texto vacio y se usa `Trim()` antes de consultar o almacenar. |
| Contrasenas semilla procesadas desde literales en tiempo de modelo | `Data/AppDbContext.cs` | Se sustituyo el calculo de BCrypt en `OnModelCreating` por hashes ya generados. |
| Respuesta 403 construida manualmente | `Controllers/ApiController.cs` | Se reemplazo `StatusCode(403)` por `Forbid()` y se expandieron retornos para mejorar legibilidad. |
| Hosts permitidos demasiado amplios | `appsettings.json` | Se reemplazo `AllowedHosts=*` por `localhost;127.0.0.1` para entorno local. |

## Verificacion local

Compilacion ejecutada:

```powershell
dotnet build
```

Resultado:

```text
Compilacion correcta.
0 Advertencia(s)
0 Errores
```

Nota: el proyecto actual no contiene un proyecto de pruebas con `Microsoft.NET.Test.Sdk`, por lo que el script de SonarQube continua sin cobertura si no encuentra pruebas. Para reportar cobertura, se puede agregar un proyecto de pruebas en una practica posterior.

## Lista de verificacion

- [x] `docker-compose.yml` creado con `sonarqube` y `sonar_db`.
- [x] `docker compose up -d` ejecutado.
- [x] SonarQube accesible en `http://localhost:9000`.
- [x] Contrasena de `admin` cambiada.
- [x] Proyecto `VulnerableApp` creado en SonarQube.
- [x] Token generado y guardado fuera de Git.
- [x] Script de analisis creado sin token hardcodeado.
- [x] Proyecto compila correctamente.
- [x] Al menos 3 hallazgos corregidos en codigo.
- [x] Analisis posterior ejecutado y dashboard revisado.

## Preguntas de reflexion

1. Un `Security Hotspot` es una zona del codigo que podria ser riesgosa y requiere revision humana para confirmar si es explotable. Un `Bug` es un defecto que la herramienta considera incorrecto por comportamiento, confiabilidad o logica.

2. Concatenar input en SQL es inseguro porque permite que el usuario cambie la estructura de la consulta. Por ejemplo, un payload como `' OR '1'='1` puede convertir una busqueda o login en una condicion siempre verdadera. La mitigacion es usar consultas parametrizadas o LINQ con Entity Framework.

3. El `Code Coverage` mide que porcentaje del codigo fue ejecutado por pruebas automatizadas. SonarQube lo considera relevante porque una mitigacion sin pruebas puede romperse despues sin que el equipo lo note.

4. Si un token de SonarQube se sube a GitHub, se debe revocar inmediatamente, generar uno nuevo, revisar accesos/logs, removerlo del historial si aplica y rotar cualquier credencial relacionada. Tambien conviene agregar reglas de secret scanning.

5. En GitHub Actions se integraria instalando .NET y SonarScanner, leyendo el token desde `GitHub Secrets`, ejecutando `begin`, `dotnet build`, `dotnet test` con cobertura y `end`. El pipeline debe fallar si el Quality Gate no cumple los criterios definidos.
