# Reporte Técnico — P3G-1 (Configuración de Serilog) y P3G-2 (Instrumentación de Controladores)

## P3G-1 — Configuración de Serilog

### Configuración implementada

`Program.cs` inicializa Serilog leyendo la configuración desde `appsettings.json` mediante
`builder.Host.UseSerilog()`, en vez de tener los sinks hardcodeados en código — esto permite
cambiar el comportamiento de logging (nivel, destinos) sin recompilar la aplicación.

`appsettings.json`:
```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "WriteTo": [
    { "Name": "Console" },
    { "Name": "File", "Args": { "path": "Logs/log-.txt", "rollingInterval": "Day" } },
    { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }
  ],
  "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ],
  "Properties": { "Application": "VulnerableApp" }
}
```

### Función de cada Sink (reto)

- **Console:** salida inmediata en la terminal durante desarrollo local — feedback instantáneo
  sin depender de ninguna herramienta externa.
- **File (`Logs/log-.txt`, rolling diario):** persistencia local en disco, independiente de que
  Seq esté disponible o no. Es la fuente que después consume Promtail para alimentar Loki/Grafana
  (práctica P4H-1), por lo que sirve de puente entre el logging de la app y la plataforma de
  observabilidad externa.
- **Seq:** almacenamiento centralizado y estructurado (JSON), con interfaz de búsqueda tipo SQL y
  capacidad de correlacionar eventos por propiedades individuales (`UserId`, `IP`, etc.) en vez de
  texto plano — usado activamente durante las prácticas de investigación de incidentes.

### Nivel mínimo configurable desde appsettings.json

Cumplido: el nivel se define en `Serilog:MinimumLevel:Default` y por namespace en `Override`
(por ejemplo, silenciando el ruido de `Microsoft.AspNetCore` y `Microsoft.EntityFrameworkCore` a
`Warning` para no inundar los logs con detalles internos del framework, mientras la aplicación
propia mantiene `Information`).

### Enriquecedor adicional y justificación (reto)

Se agregaron **dos** enrichers más allá del mínimo (`FromLogContext`):

- **`WithMachineName`:** inyecta el nombre del host/contenedor que generó el log. Es relevante
  porque, aunque en esta práctica la app corre en una sola máquina, en un entorno real con
  múltiples instancias (balanceo de carga, contenedores replicados) es indispensable saber
  **desde qué instancia física** se originó un evento al investigar un incidente — sin este dato,
  un error intermitente sería imposible de aislar a una instancia específica.
- **`WithThreadId`:** agrega el ID del hilo de ejecución. Útil para diagnosticar problemas de
  concurrencia (por ejemplo, condiciones de carrera en el acceso a la lista estática `_comments`
  de `CommentController`, que no es thread-safe) — permite ver si dos logs casi simultáneos
  corresponden al mismo hilo o a hilos distintos ejecutando en paralelo.

Adicionalmente, `Properties.Application = "VulnerableApp"` etiqueta cada evento con el nombre de
la aplicación de origen — preparación para un escenario multi-servicio donde varios proyectos
podrían compartir el mismo servidor Seq.

### Pruebas realizadas

- **Creación de archivos de log:** confirmado — carpeta `Logs/` genera archivos `log-YYYYMMDD.txt`
  con rolling diario, verificado durante la práctica de observabilidad (P4H-1) al montar esta
  carpeta vía bind mount en Promtail.
- **Eventos en Seq:** confirmado — capturas de Grafana Explore (adjuntas en el reporte de P4H-3)
  muestran los mismos eventos generados por la app, confirmando que ambos destinos (archivo y Seq)
  reciben la misma información en paralelo.
- **Cambio de nivel de logging:** al subir `MinimumLevel.Default` de `Information` a `Warning`
  (prueba manual), los logs de "Inicio X.Y" / "Fin X.Y" (nivel Information) dejan de aparecer,
  mientras que los `LogWarning` (login fallido, búsqueda vacía) y `LogError` siguen
  registrándose — confirma que el filtro de nivel funciona correctamente en cascada.

---

## P3G-2 — Instrumentación de Controladores

### Controladores instrumentados

Los 5 controladores requeridos siguen el mismo patrón consistente:

| Controlador | Inicio/Fin | Usuario | IP | Duración | Parámetros | Warnings | Errores | Auth |
|---|---|---|---|---|---|---|---|---|
| `HomeController` | ✅ | ✅ | ✅ | ✅ | N/A (sin parámetros) | ✅ (Error) | — | N/A |
| `SearchController` | ✅ | ✅ | ✅ | ✅ | ✅ (`search`) | ✅ (búsqueda vacía) | ✅ | N/A |
| `AuthController` | ✅ | ✅ | ✅ | ✅ | ✅ (`username`, nunca `password`) | ✅ (login fallido) | ✅ | ✅ (login/logout) |
| `CommentController` | ✅ | ✅ | ✅ | ✅ | ✅ (`comment`, longitud en vez de contenido completo) | ✅ (comentario vacío) | ✅ | N/A |
| `ApiController` | ✅ | N/A (sin sesión de usuario) | ✅ | ✅ | ✅ (`id`) | ✅ (usuario no encontrado) | ✅ | N/A |

### Validación de no registrar contraseñas

Confirmado por inspección directa del código: en `AuthController.Login(string username, string
password)`, el comentario explícito en el código señala *"nunca se registra 'password' en los
logs, solo el username"*, y efectivamente ningún `_logger.Log*` en el proyecto referencia la
variable `password`. Prueba adicional: se intentó iniciar sesión con credenciales inválidas y se
verificó en Seq/archivo de log que el evento `LOGIN FALLIDO` solo expone el campo `Usuario`, sin
rastro del valor de la contraseña ingresada.

### Pruebas de Information / Warning / Error

- **Information:** generado en cada acción normal (`Inicio`/`Fin` de cada método) — confirmado en
  las capturas de Grafana/Seq de prácticas anteriores.
- **Warning:** confirmado con búsquedas vacías (`Search.Index recibió un término de búsqueda
  vacío`), comentarios vacíos, y login fallido (`Evento de autenticación: LOGIN FALLIDO`).
- **Error:** confirmado en el caso de estudio de incidente (P4H-3) — aunque en la ventana
  analizada no se generaron excepciones reales durante los intentos de login fallido (dado que
  `BCrypt.Verify` simplemente retorna `false` sin lanzar excepción), sí se confirmó el patrón
  correcto de `LogError(ex, ...)` en los bloques `catch` de los 5 controladores mediante los
  endpoints de prueba dedicados (`/api/test/uncontrolled-error`, `/api/test/controlled-error` en
  `ApiController`, diseñados específicamente para validar el pipeline de manejo de excepciones).

### Reporte de hallazgos

Durante la instrumentación y su verificación posterior (prácticas de observabilidad y SonarQube),
se identificaron los siguientes puntos relevantes relacionados con el logging:

1. **Manejo de excepciones inconsistente originalmente:** varios controladores logueaban la
   excepción y luego la relanzaban con `throw;` sin agregar contexto adicional ni devolver una
   respuesta controlada al usuario (detectado y corregido en la práctica de SonarQube, regla
   `S2139`).
2. **CommentController usa una lista estática en memoria** (`_comments`) para almacenar
   comentarios, sin sincronización — bajo carga concurrente esto podría generar condiciones de
   carrera; el enricher `WithThreadId` fue elegido en parte para poder diagnosticar este escenario
   si llegara a manifestarse.
3. **El logging estructurado permitió detectar un patrón de fuerza bruta real** contra la cuenta
   `admin` (10 intentos fallidos seguidos de un login exitoso) durante la práctica de análisis de
   incidentes con LogQL — validando en la práctica el valor de instrumentar correctamente los
   eventos de autenticación desde el día uno.

### Evidencias

- Código instrumentado: confirmado en los 5 controladores (ver tabla arriba).
- Capturas de Seq/Grafana filtrando por evento: ver reportes de las prácticas P4H-1 y P4H-3
  (`reporte-observabilidad.md`, `reporte-logql-dashboards.md`), que incluyen capturas reales de
  Grafana Explore mostrando los eventos de `AuthController` y `CommentController` filtrados por
  label `category`.
