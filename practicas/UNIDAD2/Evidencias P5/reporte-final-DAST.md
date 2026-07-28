# Práctica 5 — DAST OWASP ZAP contra VulnerableApp

## 1. Entorno

- Red Docker interna (`internal: true`), sin acceso a Internet, verificada con `docker network inspect`.
- Servicios: `vulnerable-app` (ASP.NET Core), `vulnerable_sqlserver` (MSSQL 2022), `zap` (OWASP ZAP stable).
- Alcance de la prueba: exclusivamente `vulnerable-app:8080`, dentro de la red interna `pentest-net`.

## 2. Escaneo pasivo (Baseline Scan)

Comando ejecutado:
```
docker exec zap zap-baseline.py -t http://vulnerable-app:8080 -r baseline-report.html -x baseline-report.xml -J baseline-report.json -l WARN
```

Resultado: 21 URLs analizadas, 0 FAIL, 5 WARN-NEW, 61 PASS.

## 3. Escaneo activo (Full Scan)

Se utilizó un plan de Automation Framework (`full-scan-plan.yaml`) con un job `requestor` previo, porque el spider estándar no descubría por sí solo las rutas `/Auth/Login`, `/Search/Index` y `/Comment/Index` (no enlazadas desde el menú principal).

Comando ejecutado:
```
docker exec zap zap.sh -cmd -autorun /zap/wrk/full-scan-plan.yaml
```

Resultado: 23 URLs analizadas — **0 High, 2 Medium, 2 Low, 3 Informational**.

## 4. Tabla de hallazgos

| # | Nombre del hallazgo | Riesgo ZAP | CWE | CVSS estimado | URL afectada | Evidencia | Remediación propuesta |
|---|---|---|---|---|---|---|---|
| 1 | Content Security Policy (CSP) Header Not Set | Medium | CWE-693 | 5.3 (AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:L/A:N) | Todas las URLs (systemic) | Ausencia de header `Content-Security-Policy` en todas las respuestas | Agregar middleware en `Program.cs` que fije `Content-Security-Policy: default-src 'self'` |
| 2 | Missing Anti-clickjacking Header | Medium | CWE-1021 | 4.3 (AV:N/AC:L/PR:N/UI:R/S:U/C:N/I:L/A:N) | `/`, `/Comment/Index`, `/Home/Privacy`, `/Search` | Ausencia de `X-Frame-Options` / `frame-ancestors` en 5 respuestas | Agregar `X-Frame-Options: DENY` o `frame-ancestors 'none'` vía middleware |
| 3 | Application Error Disclosure | Low | CWE-550 | 3.1 (AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N) | `/Search/Index?search=ZAP`, `/Search?search=ZAP` | Respuesta HTTP 500 ante un término de búsqueda normal (no malicioso) | Corregir el bug de fondo (ver análisis detallado); en producción, capturar excepciones y devolver una respuesta genérica ya implementada parcialmente |
| 4 | X-Content-Type-Options Header Missing | Low | CWE-693 | 5.3 | Todas las URLs (systemic) | Ausencia de `X-Content-Type-Options: nosniff` | Agregar el header vía middleware |

## 5. Análisis detallado del hallazgo más crítico

**1. Nombre del hallazgo y tipo (OWASP Top 10 2021):**
Application Error Disclosure / fallo funcional en `/Search/Index` — A05:2021 (Security Misconfiguration).

**2. Request HTTP exacto:**
```
GET /Search/Index?search=ZAP HTTP/1.1
Host: vulnerable-app:8080
```

**3. Response HTTP real:**
```
HTTP/1.1 500 Internal Server Error
Content-Type: application/json; charset=utf-8
{"title":"Ocurrio un error inesperado.","status":500,"detail":"Use el CorrelationId para localizar el evento en los registros.","correlationId":"21cb27eb38214879921a886057bedd57"}
```

**4. Por qué esta respuesta confirma el problema (mecanismo):**
La app responde con error 500 ante un término de búsqueda completamente benigno (`ZAP`), no ante un payload malicioso. Esto indica una falla funcional de fondo — muy probablemente la cadena de conexión en `appsettings.json` (`Server=(localdb)\mssqllocaldb`) es una configuración de LocalDB **exclusiva de Windows**, incompatible con el contenedor Linux donde corre la app, causando que toda consulta a la base de datos falle. **Nota positiva de seguridad:** a diferencia de una mala práctica común, la respuesta no expone stack trace ni rutas de archivo — solo un `correlationId` para trazabilidad en logs internos, lo cual es correcto.

**5. Qué impacto tiene en un escenario real:**
No es una fuga de información explotable directamente, pero representa una **denegación de servicio funcional**: cualquier usuario que use la búsqueda recibe error 500. En un entorno real esto rompe una funcionalidad core de negocio y podría enmascarar fallos de seguridad más profundos si no se monitorea adecuadamente.

**6. Corrección propuesta:**
```csharp
// appsettings.json / appsettings.Production.json
"ConnectionStrings": {
  "DefaultConnection": "Server=vulnerable_sqlserver;Database=VulnerableDb;User Id=sa;Password=VulnerableApp#2026!;TrustServerCertificate=true;"
}
```
Además, envolver la consulta en `SearchController.Index` con manejo explícito de `SqlException` para devolver un mensaje amigable en vez de propagar el error 500 genérico al usuario final.

## 6. Hallazgo investigado y descartado (transparencia metodológica)

Se sospechó inicialmente un **Stored XSS** en `/Comment/AddComment`, basado en el uso de `@Html.Raw(comment)` en el código fuente (`Views/Comment/Index.cshtml`), que omite el escapado automático de Razor.

**Prueba realizada:** POST con token `__RequestVerificationToken` válido y payload `<script>alert(document.cookie)</script>`.

**Resultado observado:** el servidor devuelve el contenido **doblemente codificado como entidad HTML** (`&amp;lt;script&amp;gt;`), por lo que el navegador lo renderiza como texto literal, no como script ejecutable.

**Conclusión:** el hallazgo **no es explotable en el estado actual del contenedor desplegado**, pese a que el patrón de código sugiere lo contrario. Se documenta como discrepancia entre revisión estática de código y comportamiento observado en runtime — posible indicio de que la imagen Docker en ejecución no corresponde exactamente a la versión de código revisada, o de que existe una capa de codificación adicional no identificada en el pipeline. Se recomienda como trabajo futuro reconstruir la imagen desde el código fuente actual y repetir la prueba para confirmar.

## 7. Verificación post-corrección

*(Pendiente — aplicar las correcciones de P2 al código, reconstruir la imagen, y re-ejecutar:)*
```
docker compose build vulnerable-app
docker compose up -d vulnerable-app
docker exec zap zap-baseline.py -t http://vulnerable-app:8080 -r baseline-after-fix.html -l WARN
```
Comparar `baseline-report.html` (antes) vs `baseline-after-fix.html` (después) y documentar qué hallazgos desaparecieron.

## 8. GitHub Actions y reglas ZAP

- Job `dast-zap` agregado a `.github/workflows/security.yml` (ver archivo adjunto), apuntando a `http://localhost:8080`.
- `.zap/rules.tsv` configurado con los 5 hallazgos reales detectados, cada uno con justificación (ver archivo adjunto).

## 9. Cierre ético

Al finalizar, destruir el entorno vulnerable:
```
docker compose down --volumes --remove-orphans
```
**Confirmar en el reporte final entregado que este comando fue ejecutado.**
