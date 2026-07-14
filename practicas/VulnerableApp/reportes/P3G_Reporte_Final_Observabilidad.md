# Reporte técnico final de observabilidad

## SEGG-U2-P3G-3, P3G-4 y P3G-5

- **Proyecto:** VulnerableApp
- **Tecnología:** ASP.NET Core 10, Serilog y Seq
- **Fecha de ejecución:** 3 de julio de 2026
- **Zona horaria:** America/Mexico_City
**Lote analizado:** `P3G-20260703-213558`

---

## Objetivo

Implementar logging global con CorrelationId, captura segura de excepciones y
medición de solicitudes HTTP; generar un volumen representativo de eventos;
analizarlo en Seq y en archivos rotativos; y documentar resultados que permitan
reconstruir solicitudes, detectar anomalías y localizar indicios de SQL
Injection y XSS.

## Implementación

### Middleware global

La aplicación incorpora tres componentes en el pipeline:

1. `CorrelationIdMiddleware` genera o valida `X-Correlation-ID`, lo devuelve en
   la respuesta y lo agrega al `LogContext`.
2. `RequestLoggingMiddleware` registra método, ruta, código HTTP, tiempo,
   usuario, IP y CorrelationId.
3. `ExceptionLoggingMiddleware` captura excepciones no controladas, registra el
   objeto `Exception` y devuelve un problema HTTP 500 sin exponer detalles
   internos.

Cada solicitud de carga también utiliza `X-Test-Run-ID`. Esta propiedad permitió
aislar el lote final en Seq sin eliminar los registros de ejecuciones anteriores.

### Detección de escenarios de seguridad

- `SearchController` registra un Warning cuando detecta patrones similares a
  SQL Injection.
- `CommentController` registra un Warning ante etiquetas, esquemas o atributos
  típicos de XSS.
- Los valores se envían como propiedades estructuradas y los caracteres de
  control se neutralizan antes de escribirlos.
- Las contraseñas no forman parte de ninguna plantilla, propiedad ni archivo.

### Formato de archivo

Los sinks de consola y archivo incluyen fecha, nivel, CorrelationId,
`SourceContext`, mensaje y excepción:

```text
2026-07-03 21:35:58.627 -06:00 [INF] [a5db704a9fd74dcd875eb586ce057ce4]
VulnerableApp.Middleware.RequestLoggingMiddleware HTTP GET / respondio 200...
```

## Pruebas ejecutadas

El generador `run-p3g-observability-load.ps1` produjo el siguiente volumen:

| Escenario | Cantidad |
| --- | ---: |
| Visitas a la página principal | 30 |
| Búsquedas válidas | 100 |
| Búsquedas vacías | 20 |
| Búsquedas con caracteres especiales | 20 |
| Búsquedas similares a SQL Injection | 30 |
| Inicios de sesión exitosos | 50 |
| Inicios de sesión fallidos | 100 |
| Comentarios válidos | 100 |
| Comentarios con posibles cargas XSS | 30 |
| Consultas API válidas | 200 |
| Consultas API con identificadores inválidos/inexistentes | 20 |
| Excepciones controladas | 20 |
| Excepciones no controladas | 10 |

El lote tardó 42.35 segundos. Todas las respuestas incluyeron CorrelationId y
ninguna respuesta tuvo un estado distinto del esperado:

```text
CorrelationId faltantes: 0
Respuestas inesperadas: 0
```

La suite automatizada obtuvo:

```text
17 pruebas superadas
0 pruebas con error
```

Las pruebas verifican controladores, middleware, encabezados de correlación,
problemas HTTP 500, excepciones, tiempos, niveles y detección de SQLi/XSS.

## Resultados obtenidos

El analizador procesó 3,730 líneas correspondientes exclusivamente a la ventana
del lote final.

| Métrica | Resultado |
| --- | ---: |
| Eventos Information | 3,499 |
| Eventos Warning | 211 |
| Eventos Error | 20 |
| CorrelationId distintos | 1,063 |
| Intentos de autenticación fallidos | 100 |
| Intentos de SQL Injection identificados | 30 |
| Posibles intentos XSS identificados | 30 |
| Excepciones controladas | 20 |
| Excepciones no controladas | 10 |

Una búsqueda literal sobre todos los archivos de log obtuvo **0 coincidencias**
para la contraseña válida, las contraseñas inválidas de prueba y el nombre de la
variable que las contiene.

## Respuestas al análisis solicitado

1. **¿Cuántos eventos Information fueron registrados?**
   3,499.

2. **¿Cuántos eventos Warning fueron registrados?**
   211.

3. **¿Cuántos eventos Error fueron registrados?**
   20. Cada excepción no controlada produjo un Error del controlador y otro del
   middleware global.

4. **¿Qué controlador generó más registros?**
   `AuthController`, con 702 eventos.

5. **¿Cuál fue el endpoint con mayor número de solicitudes?**
   `GET /Search`, con 170 solicitudes.

6. **¿Cuál fue la dirección IP con mayor actividad?**
   `::1`, con 2,126 eventos. Corresponde al loopback IPv6 local.

7. **¿Cuántos intentos de autenticación fallidos existieron?**
   100.

8. **¿Cuántos intentos de SQL Injection fueron identificados?**
   30.

9. **¿Cuántos posibles intentos de XSS fueron registrados?**
   30.

10. **¿Cuál fue la solicitud con mayor tiempo de ejecución?**
    `GET /Search`, con 2,175 ms y CorrelationId
    `9a7841d335eb4beaa172a266185ec594`.

11. **¿Fue posible localizar una petición utilizando únicamente el
    CorrelationId?**
    Sí. El identificador `a5db704a9fd74dcd875eb586ce057ce4` devolvió los tres
    eventos de la misma solicitud: entrada del controlador, salida y resumen
    HTTP global.

## Evidencias

### Datos y archivos

- [Resultado del generador de carga](../evidencias/P3G-Continuacion/load-results.json)
- [Resultado del análisis](../evidencias/P3G-Continuacion/analysis-results.json)
- [Evidencia verificable de carpeta y archivo de logs](../evidencias/P3G-Continuacion/logs-evidence.html)

### Capturas de Seq

- [Filtro Information](../evidencias/P3G-Continuacion/01-seq-information.png)
- [Filtro Warning](../evidencias/P3G-Continuacion/02-seq-warning.png)
- [Filtro Error](../evidencias/P3G-Continuacion/03-seq-error.png)
- [Búsqueda por CorrelationId](../evidencias/P3G-Continuacion/04-seq-correlation-id.png)
- [Endpoint GET /Search](../evidencias/P3G-Continuacion/05-seq-endpoint-search.png)
- [Evento de autenticación](../evidencias/P3G-Continuacion/06-seq-authentication.png)
- [Excepciones no controladas](../evidencias/P3G-Continuacion/07-seq-exception.png)
- [Intentos de SQL Injection](../evidencias/P3G-Continuacion/08-seq-sql-injection.png)
- [Intentos de XSS](../evidencias/P3G-Continuacion/09-seq-xss.png)

### Código solicitado

- [Program.cs](../Program.cs)
- [CorrelationIdMiddleware.cs](../Middleware/CorrelationIdMiddleware.cs)
- [ExceptionLoggingMiddleware.cs](../Middleware/ExceptionLoggingMiddleware.cs)
- [RequestLoggingMiddleware.cs](../Middleware/RequestLoggingMiddleware.cs)
- [Ejemplo de controlador instrumentado](../Controllers/SearchController.cs)
- [Generador reproducible](../scripts/run-p3g-observability-load.ps1)
- [Analizador reproducible](../scripts/analyze-p3g-logs.ps1)

## Hallazgos y conclusiones

1. El CorrelationId permite reconstruir una solicitud sin depender de texto
   libre, incluso cuando participan el controlador y varios middleware.
2. El `TestRunId` evita mezclar ejecuciones y permite repetir la práctica sin
   incumplir la instrucción de conservar archivos previos.
3. El endpoint de búsqueda concentró la mayor actividad y también produjo la
   solicitud más lenta; es el primer candidato para análisis de rendimiento.
4. Los patrones de SQLi y XSS fueron detectados sin ejecutar SQL dinámico ni
   renderizar contenido sin codificación. Son indicios, no pruebas definitivas
   de explotación.
5. El middleware global devuelve una respuesta segura con CorrelationId y
   conserva la excepción completa únicamente en los sinks.
6. No se encontraron contraseñas en los registros, aun después de 150 intentos
   de autenticación.

## Referencias

- [Serilog.AspNetCore - integración y request logging](https://github.com/serilog/serilog-aspnetcore)
- [Serilog - logging estructurado](https://github.com/serilog/serilog)
- [Seq - sintaxis de filtros](https://docs.datalust.co/docs/query-syntax)
- [Seq - búsqueda y análisis de logs](https://docs.datalust.co/docs/the-seq-query-language)
- [ASP.NET Core - escribir middleware personalizado](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/write?view=aspnetcore-10.0)
- [ASP.NET Core - manejo de errores](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
