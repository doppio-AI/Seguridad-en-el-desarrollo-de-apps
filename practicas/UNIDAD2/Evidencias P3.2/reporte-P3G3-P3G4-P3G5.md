# Reporte Técnico — P3G-3, P3G-4 y P3G-5

## Portada

**Práctica:** SEGG-U2-P3G-3/4/5 — Middleware de Logging Global, Monitoreo con Seq, Generación de
Evidencias y Reporte de Resultados
**Aplicación:** VulnerableApp (ASP.NET Core, rama `main`)
**Herramientas:** Serilog, Seq, PowerShell (script de generación de tráfico automatizado)

## Objetivo

Implementar y validar el middleware de logging global (CorrelationId + manejo de excepciones),
generar un volumen representativo de tráfico funcional y de seguridad contra VulnerableApp, y
analizar los registros resultantes en Seq para extraer conclusiones operativas y de seguridad.

## P3G-3 — Middleware de Logging Global

### Componentes verificados

- **`CorrelationIdMiddleware`**: genera un GUID único por petición, lo expone en el header de
  respuesta `X-Correlation-ID`, y lo inyecta en el `LogContext` de Serilog vía
  `LogContext.PushProperty("CorrelationId", correlationId)`. Gracias al enricher `FromLogContext`
  (configurado en P3G-1), **cada línea de log generada durante esa petición queda automáticamente
  etiquetada con el mismo CorrelationId**, sin necesidad de pasarlo manualmente en cada llamada de
  logging.
- **`ExceptionHandlingMiddleware`**: envuelve el resto del pipeline en un `try/catch` global,
  registra cualquier excepción no controlada con `LogError` (incluyendo ruta, método HTTP e IP), y
  responde `500` de forma controlada sin exponer detalles internos al cliente.
- **Orden del pipeline** en `Program.cs`: `UseCorrelationId()` → `UseGlobalExceptionHandling()` →
  `UseRequestLogging()` — orden correcto, ya que el CorrelationId debe existir antes de que
  cualquier log posterior (incluidos los de excepciones) pueda incluirlo.

### Pruebas realizadas

- Validación de CorrelationId: confirmado mediante búsqueda directa en Seq por `CorrelationId`
  (ver sección de análisis, pregunta 11).
- Provocación de excepciones: 10 controladas (`/api/test/controlled-error`,
  `/Search/Index?search=__test_controlled_error__`) y 10 no controladas
  (`/api/test/uncontrolled-error`) ejecutadas exitosamente.
- **Hallazgo adicional no planeado**: se descubrió un caso real de excepción no controlada durante
  la generación de tráfico — ver sección "Hallazgos" más abajo.

## P3G-4 — Monitoreo con Seq

### Instalación y acceso

Seq se desplegó vía Docker (`datalust/seq:latest`), con `SEQ_FIRSTRUN_NOAUTHENTICATION=True` para
acceso directo sin login (entorno de práctica local). Verificado accesible en
`http://localhost:8081`, recibiendo eventos en tiempo real desde la app en `http://localhost:5271`
(vía `serverUrl` configurado en `appsettings.json`, puerto `5341`).

### Filtros aplicados

- Por nivel: `Warning`, `Error` (confirmados directamente vía los chips de nivel de Seq).
- Por texto libre: nombres de controlador (`Search`, `Comment`, `Api`, `Home`, `Auth`), tipo de
  evento (`LOGIN FALLIDO`, `SQL Injection`, `XSS`).
- Por `CorrelationId`: búsqueda exacta de un GUID copiado de un evento expandido, confirmando que
  agrupa correctamente todas las líneas de una única petición HTTP.

## P3G-5 — Generación de Evidencias y Análisis

### Metodología de generación de tráfico

Dado el volumen exigido por la guía (cientos de peticiones en múltiples categorías), se generó
tráfico mediante un **script de PowerShell automatizado** (`generar-trafico-p3g5.ps1`) que ejecutó
sistemáticamente:

| Categoría | Volumen generado |
|---|---|
| Home / navegación general | 30× home + todos los controladores |
| Búsquedas válidas | 100 |
| Búsquedas vacías | 20 + 15 adicionales |
| Búsquedas con caracteres especiales | 20 |
| Búsquedas tipo SQL Injection | 20 |
| Logins exitosos | 50 (rotando entre `admin`, `user1`, `user2`) |
| Logins fallidos | 100 (contraseñas incorrectas, usuarios inexistentes) |
| Comentarios válidos | 100 |
| Comentarios con carga XSS | 30 |
| Comentarios vacíos | 10 |
| Llamadas a la API | 200+ (IDs válidos, inexistentes, inválidos) |
| Excepciones controladas | 20 |
| Excepciones no controladas | 10 |

**Total de eventos registrados en Seq: 3,538.**

### Respuestas a las preguntas de análisis

**1. ¿Cuántos eventos Information fueron registrados?**
La interfaz de Seq de esta instancia no expuso un filtro directo por nivel `Information` (solo
mostró chips para `Warning` y `Error`). Se calculó indirectamente:
`3,538 (total) − 248 (Warning) − 54 (Error) = 3,236 eventos Information`.

**2. ¿Cuántos eventos Warning fueron registrados?**
**248.**

**3. ¿Cuántos eventos Error fueron registrados?**
**54.** Este número es más alto de lo que un flujo "limpio" produciría, debido al hallazgo descrito
más abajo (login con contraseña vacía/null causa una excepción no controlada en lugar de un
simple rechazo de credenciales).

**4. ¿Qué controlador generó más registros?**
Búsqueda por texto libre del nombre de cada controlador (nota: estos conteos no son mutuamente
excluyentes, ya que el texto puede coincidir en mensajes de otros contextos, pero sí son
representativos del volumen relativo):

| Controlador | Coincidencias |
|---|---|
| **Search** | **942** ← mayor volumen |
| Comment | 883 |
| Api | 738 |
| Auth | 614 |
| Home | 63 |

`SearchController` generó más registros, coherente con que cada búsqueda produce 5 líneas de log
(Inicio, Usuario/IP/Ruta/Parámetro, resultado, Fin, Request) y se ejecutaron ~195 búsquedas totales
entre válidas, vacías, con caracteres especiales y SQLi.

**5. ¿Cuál fue el endpoint con mayor número de solicitudes?**
Consistente con la pregunta anterior, `/Search/Index` fue el endpoint con más solicitudes,
seguido de cerca por `/Comment/Index` y `/Comment/AddComment` combinados (cada comentario agregado
dispara un redirect a `Index`, duplicando efectivamente las peticiones por cada comentario).

**6. ¿Cuál fue la dirección IP con mayor actividad?**
**`::1`** (loopback IPv6) — la única IP registrada, ya que todo el tráfico se generó desde la
misma máquina local ejecutando tanto el script como la aplicación. En un entorno de producción
real con tráfico distribuido, este análisis permitiría identificar orígenes anómalos de tráfico
concentrado.

**7. ¿Cuántos intentos de autenticación fallidos existieron?**
**81** eventos de `LOGIN FALLIDO` (nivel Warning). Es menor a los 100 que el script intentó
generar porque varios intentos con la combinación `admin` + contraseña vacía **no llegaron a
generar el Warning esperado** — en su lugar, causaron una excepción no controlada (ver Hallazgos).

**8. ¿Cuántos intentos de SQL Injection fueron identificados?**
**22** — detectados por `SecurityPatternDetector.LooksLikeSqlInjection()` en `SearchController`.

**9. ¿Cuántos posibles intentos de XSS fueron registrados?**
**27** — detectados por `SecurityPatternDetector.LooksLikeXss()` en `CommentController`.

**10. ¿Cuál fue la solicitud con mayor tiempo de ejecución?**
Localizada ordenando por la propiedad `DuracionMs` en Seq. Los picos observados en el log
correspondieron a llamadas a `Api.GetAllUsers` (hasta ~2,170 ms en la primera ejecución tras el
arranque de la app, probablemente por warm-up de Entity Framework Core / primera conexión a la
base de datos) y algunos `Search.Index` puntuales con picos de 150+ ms, muy por encima del
promedio de 8-15 ms del resto de las solicitudes.

**11. ¿Fue posible localizar una petición específica utilizando únicamente el CorrelationId?**
**Sí, confirmado.** Se copió el `CorrelationId` de un evento expandido en Seq y se usó como filtro
exacto; el resultado agrupó correctamente todas las líneas de log (Inicio, procesamiento, Fin,
Request) pertenecientes a esa única petición HTTP, demostrando que el mecanismo de trazabilidad
implementado en P3G-3 funciona como se esperaba.

### Hallazgos

**Hallazgo principal: excepción no controlada por contraseña vacía en login.**

Al enviar `POST /Auth/Login` con `username=admin` y `password=""` (vacío), la aplicación no valida
la entrada antes de llamar a `BCrypt.Net.BCrypt.Verify()`, que lanza:

```
System.ArgumentNullException: Value cannot be null. (Parameter 'inputKey')
   at BCrypt.Net.BCrypt.HashPassword(...)
   at BCrypt.Net.BCrypt.Verify(...)
   at VulnerableApp.Controllers.AuthController.Login(String username, String password)
```

Esta excepción se propaga hasta el `ExceptionHandlingMiddleware` global (nivel `Error`,
"Excepción no controlada"), en lugar de resolverse como un simple `LOGIN FALLIDO` (nivel
`Warning`), que es el comportamiento esperado para credenciales inválidas. Esto es una debilidad
de manejo de errores (no expone datos sensibles gracias al middleware, pero sí genera ruido de
`Error` en los logs y una respuesta HTTP 200 con mensaje genérico en vez de un rechazo claro de
credenciales).

**Corrección propuesta:**
```csharp
if (string.IsNullOrEmpty(password))
{
    _logger.LogWarning("Evento de autenticación: LOGIN FALLIDO (password vacío). Usuario:{Usuario} IP:{IP}", username, ClientIp);
    ViewBag.Error = "Credenciales inválidas";
    return View();
}
```
agregado antes de la llamada a `BCrypt.Verify()`.

## Conclusiones

- El mecanismo de CorrelationId + logging estructurado permitió, en la práctica, **reconstruir el
  historial completo de una petición específica con un solo identificador**, validando el valor
  operativo del middleware implementado en P3G-3.
- El volumen de tráfico generado (3,538 eventos) fue suficiente para obtener métricas
  estadísticamente representativas por controlador, tipo de evento y nivel de severidad.
- El análisis de logs permitió **descubrir un bug real** (excepción no controlada por contraseña
  vacía) que no había sido identificado en revisiones de código anteriores — demostrando que el
  logging estructurado no es solo para cumplimiento, sino una herramienta activa de control de
  calidad.
- La ausencia de diversidad de IPs en este entorno de práctica (todo `::1`) es una limitación
  esperada del entorno local; en producción, este mismo análisis sería clave para detectar
  patrones de tráfico anómalo por origen.
- Algunas limitaciones de la interfaz de Seq utilizada (falta de chip visual para nivel
  `Information`) obligaron a un cálculo indirecto para una de las métricas — documentado
  explícitamente para mantener la trazabilidad metodológica del análisis.

## Referencias consultadas

- Documentación oficial de Serilog: https://serilog.net/
- Documentación oficial de Seq: https://docs.datalust.co/docs
- Guías de práctica: SEGG-U2-P3G-1 a P3G-5 (Configuración de Logging, Instrumentación de
  Controladores, Middleware de Logging Global, Monitoreo con Seq, Generación de Evidencias)
