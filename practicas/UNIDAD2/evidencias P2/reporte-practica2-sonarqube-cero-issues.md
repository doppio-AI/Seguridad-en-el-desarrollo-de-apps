# Reporte de Práctica — Resolución completa de Issues de SonarQube (Security, Reliability, Maintainability)

## 1. Objetivo

Partir del análisis inicial de SonarQube sobre `MiAppNetCore` (VulnerableApp) y resolver la
totalidad de los issues abiertos en las tres categorías de calidad — Security, Reliability y
Maintainability — hasta llevar el conteo de cada una a **0**, documentando el proceso y
manteniendo la trazabilidad en Git entre el estado original y las correcciones.

## 2. Control de versiones

- **Commit original:** rama `main`, estado tal como quedó de las prácticas de DAST/observabilidad
  previas — contiene las vulnerabilidades intencionales del proyecto sin modificar.
- **Rama de trabajo:** `fix/sonarqube-hallazgos`, creada a partir de `main`, donde se aplicaron
  todas las correcciones de esta práctica en varios commits incrementales.
- `main` se conserva intacta para no afectar las prácticas de DAST (ZAP) ya realizadas contra la
  app vulnerable original.

## 3. Estado inicial del análisis

| Categoría | Issues iniciales |
|---|---|
| Security | 6 |
| Reliability | 27 |
| Maintainability | ~1,200 |
| Security Hotspots | 2 (sin revisar) |
| Coverage | 0% |
| Duplications | 47.3% |

## 4. Hallazgo clave: ruido por artefactos generados

Antes de corregir código, se identificó que la mayoría de los ~1,200 issues de Maintainability y
gran parte de los 27 de Reliability **no correspondían a código de la aplicación**, sino al
contenido de los reportes HTML de ZAP (`zap-reports/baseline-report.html`,
`zap-reports/fullscan-report.html`) generados en la práctica de DAST, y a código autogenerado por
EF Core en `Migrations/`. SonarQube analizaba esos archivos como si fueran código fuente propio
(detectando problemas de accesibilidad e IDs duplicados en la plantilla HTML de ZAP).

**Corrección:** se agregó `sonar.exclusions` al comando `begin`:
```
/d:sonar.exclusions="zap-reports/**,bin/**,obj/**,Migrations/**"
```
Esto redujo Reliability de 27 a 6 issues reales de forma inmediata, sin tocar una sola línea de
código de la aplicación — una lección importante sobre configurar correctamente el alcance del
análisis antes de invertir tiempo corrigiendo falsos hallazgos.

## 5. Hallazgos de Security resueltos

| # | Hallazgo | Severidad | Archivo | Resolución |
|---|---|---|---|---|
| 1 | Token de SonarQube expuesto en texto plano | **Blocker** | `.env` | Token revocado en SonarQube, `.env` removido de Git (`git rm --cached`) y agregado a `.gitignore`, nuevo token generado |
| 2-6 | 5× "password" detectado, posible credencial hardcodeada | Medium | `Data/AppDbContext.cs` | 3 de los 5 correspondían a un campo `Password` en texto plano real en los datos semilla → **eliminado del modelo y de la API** (CWE-256). Los 2 restantes correspondían a `PasswordHash` (hash BCrypt unidireccional) → marcados como **False Positive** con justificación, ya que no son una credencial explotable |

**Regla adicional corregida (no listada en el conteo de Security pero relacionada):** `SqlInjectionPattern` y `XssPattern` en `Security/SecurityPatternsDetector.cs` no tenían timeout, exponiendo un riesgo de ReDoS (regex backtracking catastrófico) ante un input malicioso diseñado específicamente. Se agregó `TimeSpan.FromMilliseconds(500)` a ambos `Regex`.

## 6. Hallazgos de Reliability resueltos (tras excluir artefactos: 6 reales)

| Archivo | Línea | Hallazgo | Resolución |
|---|---|---|---|
| `Program.cs` | 30 | Usar `MigrateAsync` en vez de `Migrate` | `await db.Database.MigrateAsync();` |
| `Program.cs` | 53 | Usar `RunAsync` en vez de `Run` | `await app.RunAsync();` |
| `Views/Auth/Login.cshtml` | 7-8 | Inputs sin `id`/`label` (accesibilidad) | Se agregaron `id` y `<label for="...">` a ambos campos |
| `Views/Comment/Index.cshtml` | 6 | Textarea sin `id`/`label` | Se agregó `id="comment"` y `<label>` |
| `Views/Search/Index.cshtml` | 5 | Input sin `id`/`label` | Se agregó `id="search"` y `<label>` |

## 7. Hallazgos de Maintainability resueltos

| Archivo | Hallazgo | Resolución |
|---|---|---|
| ~20 instancias en 6 controllers | `CA1873` — evaluación de argumento de log potencialmente costosa | Suprimida vía `.editorconfig` (`dotnet_diagnostic.CA1873.severity = none`), justificado: los argumentos son accesos a propiedades de bajo costo (IP, IDs), no cómputos costosos |
| `Models/User.cs` | 4× CS8618, propiedades no-nullable sin valor garantizado | Se agregó el modificador `required` a `Username`, `PasswordHash`, `Email` (y se eliminó `Password`, ver sección Security) |
| `Middleware/RequestLoggingMiddleware.cs` L26 | Ternario anidado confuso | Extraído a método `GetLogLevel(int statusCode)` |
| `Data/AppDbContext.cs` (×3) | Falta especificar `DateTimeKind` | Se agregó `DateTimeKind.Utc` a las 3 fechas semilla |
| `Controllers/ApiController.cs` (GetUser, GetAllUsers) | `S2139` — loguear y relanzar sin manejar | Cambiado a `return StatusCode(500, ...)` tras loguear, en vez de `throw;` |
| `Controllers/AuthController.cs` (Login) | `S2139` | Cambiado a `ViewBag.Error = "..."; return View();` |
| `Controllers/CommentController.cs` (AddComment) | `S2139` | Cambiado a `return RedirectToAction("Index");` |
| `Controllers/SearchController.cs` (Index) | `S2139` | Cambiado a `return View(new List<User>());` |
| `Program.cs` L35 | Falso positivo: "TODO" detectado dentro de la palabra española "todo" | Reescrito el comentario para evitar la coincidencia textual |
| `wwwroot/css/site.css` | Selector `html` duplicado + bloque `@media` vacío | Bloques `html {}` fusionados en uno solo; `@media (min-width: 768px) {}` vacío eliminado |
| `Migrations/*.cs` | Sugerencia sobre código autogenerado por EF Core | Excluido del análisis vía `sonar.exclusions` (no es código propio) |

## 8. Bug de seguridad adicional detectado durante la corrección

Al eliminar el campo `Password` del modelo, se detectó que `ApiController.GetUser` **devolvía el
password en texto plano en la respuesta JSON de la API** (`user.Password` incluido en el `Ok(new {...})`).
Se corrigió eliminando el campo de la respuesta — este hallazgo no aparecía directamente listado
por SonarQube como issue de "Security" bajo esa regla específica, pero es una instancia real de
CWE-200 (Exposure of Sensitive Information) descubierta como efecto colateral positivo del proceso
de refactorización.

## 9. Resultado final

| Categoría | Antes | Después |
|---|---|---|
| Security | 6 | **0** |
| Reliability | 27 (6 reales tras exclusiones) | **0** |
| Maintainability | ~1,200 (drásticamente menor tras exclusiones) | **0** |

## 10. Commits en Git

Rama `fix/sonarqube-hallazgos`, historial de commits (resumen):
1. `fix: corrige hallazgos de seguridad (token expuesto, ReDoS en regex, password en texto plano)`
2. `fix: corrige issues de Reliability (async/await, accesibilidad de formularios)`
3. `fix: agrega id attribute faltante en Search/Index.cshtml`
4. `fix: resuelve issues de Maintainability (required properties, ternario anidado, CSS duplicado, DateTimeKind, manejo de excepciones, elimina Password del modelo y de la API)`
5. `fix: corrige S2139 en SearchController, DateTimeKind en AppDbContext, empty block en CSS, falso positivo TODO en Program.cs`
6. `fix: corrige S2139 pendiente en ApiController.GetAllUsers`

## 11. Conclusiones

- La configuración correcta del **alcance del análisis** (`sonar.exclusions`) es tan importante
  como la corrección de código: sin excluir artefactos generados (reportes de ZAP, migraciones de
  EF Core, carpetas de build), el conteo de issues estaba inflado con ruido que no representaba
  deuda técnica real del proyecto.
- Algunos hallazgos de Security fueron **falsos positivos genuinos** (hash BCrypt marcado como
  "posible credencial hardcodeada" por contener la palabra "password"), mientras que otros con el
  mismo mensaje resultaron ser **vulnerabilidades reales** (contraseña en texto plano) — es
  necesario revisar cada hallazgo individualmente en vez de aplicar una regla ciega.
- El proceso de corrección expuso un bug de seguridad adicional no capturado directamente por
  SonarQube (exposición de password en respuesta de API), reforzando que el análisis estático es
  un complemento, no un sustituto, de la revisión manual de código.
- Mantener el trabajo en una rama separada (`fix/sonarqube-hallazgos`) permitió resolver todos los
  issues sin perder la versión vulnerable original necesaria para las prácticas de DAST.
