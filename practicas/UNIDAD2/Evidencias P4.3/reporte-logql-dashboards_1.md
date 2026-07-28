# SEGG-U2-P4H-3 — Consultas LogQL y Construcción de Dashboards

> **Nota de mapeo de labels:** la guía usa como ejemplo el label `application` con valores
> `VulnerableApp` / `Security` / `Audit`. En nuestra infraestructura (configurada en P4H-1) los
> labels reales son `job="vulnerableapp"` (todo el tráfico de la app) y `category` con valores
> `Security`, `Audit` o `General` (asignado por el pipeline de Promtail). Todas las consultas de
> este documento usan los nombres reales de nuestros labels — funcionalmente equivalentes al
> ejemplo de la guía.

## 1. Sintaxis básica de LogQL

| Consulta | Propósito | Resultado esperado |
|---|---|---|
| `{job="vulnerableapp"}` | Traer **todos** los logs de la aplicación, sin filtrar por categoría | Stream completo: requests HTTP, comentarios, búsquedas, login/logout |
| `{job="vulnerableapp", category="Security"}` | Filtrar solo eventos de autenticación (login/logout) | Únicamente líneas con `"Evento de autenticación: ..."` |
| `{job="vulnerableapp", category="Audit"}` | Filtrar solo eventos de auditoría de operaciones (CRUD) | Líneas de `"Comentario agregado"`, `"resultados encontrados"`, etc. |
| `{job="vulnerableapp"} \|= "[ERR]"` | Filtrar mensajes que contengan "Error" (nivel ERR de Serilog) | Solo líneas con excepciones capturadas por `_logger.LogError(...)` |
| `{job="vulnerableapp"} \|= "[WRN]"` | Filtrar mensajes que contengan "Warning" (nivel WRN de Serilog) | Líneas como `"LOGIN FALLIDO"`, `"comentario vacío"`, etc. |
| `{job="vulnerableapp", category="Security"} \|= "FALLIDO"` | Combinar labels y texto: solo eventos de seguridad que además contengan "FALLIDO" | Únicamente intentos de login fallidos, excluyendo logins exitosos y logouts |

**Por qué funciona así:** LogQL primero resuelve el **selector de labels** (lo que va entre `{}`) para elegir qué *streams* consultar — esto es rápido porque los labels están indexados. Después, cualquier filtro de texto con `|=` (contiene), `!=` (no contiene) o `|~` (regex) se aplica **solo sobre las líneas de esos streams ya reducidos**, no sobre todo el volumen de logs.

## 2. Interfaz de Explore

| Elemento | Función |
|---|---|
| **Editor de consultas** (barra superior, con el selector de data source "Loki") | Donde se escribe la expresión LogQL. Incluye autocompletado de labels existentes al presionar `{` |
| **Selector de tiempo** (esquina superior derecha, ej. "Last 1 hour") | Define la ventana temporal de la consulta — Loki solo trae logs dentro de ese rango, lo que también mejora el rendimiento |
| **Panel de resultados** (área central) | Muestra las líneas de log que hicieron match, ordenadas por tiempo, con sus labels asociados visibles a la izquierda de cada línea |
| **Botón "Run query" / Shift+Enter** | Ejecuta la consulta actual contra Loki |
| **Vista Logs / Table** (arriba a la derecha del panel de resultados) | Alterna entre ver las líneas crudas ("Logs") o una tabla estructurada por campos ("Table"), útil si se usó `| json` o `| logfmt` en la consulta |

## 3. Consultas específicas construidas

```logql
# Errores
{job="vulnerableapp"} |= "[ERR]"

# Advertencias
{job="vulnerableapp"} |= "[WRN]"

# Eventos de autenticación (login/logout, exitosos y fallidos)
{job="vulnerableapp", category="Security"}

# Solo logins fallidos
{job="vulnerableapp", category="Security"} |= "LOGIN FALLIDO"

# Solo logins exitosos
{job="vulnerableapp", category="Security"} |= "LOGIN EXITOSO"
```

**Cómo se modificaron:** se partió de la consulta base por label (`{job="vulnerableapp"}` o
`{job="vulnerableapp", category="Security"}`) y se le agregó un filtro de línea `|= "texto"` para
acotar el resultado a un evento específico. El operador `|=` es un filtro de "contiene" literal
(no regex), más rápido que `|~` cuando no se necesita un patrón complejo.

## 4. Análisis de incidente (caso de estudio) — RESUELTO con datos reales

**Consulta usada:** `{job="vulnerableapp", category="Security"} |= "LOGIN FALLIDO"`, ordenada
*Oldest first* en Explore.

**¿Cuándo comenzaron?**
El primer intento fallido registrado en la ventana consultada es:
```
2026-07-28 14:24:13.756 -06:00 [WRN] Evento de autenticación: LOGIN FALLIDO. Usuario:admin IP:::1
```
A partir de ahí se observan **16 intentos fallidos** distribuidos en ráfagas, concentradas en
tres momentos: `~14:24`, `~14:37` y `~14:41-14:42`, con un último intento aislado a las `15:11:58`
y `15:12:13`.

**¿Qué módulo los generó?**
Todas las líneas provienen de `AuthController` (acción `Login` en su variante `POST`), identificable
por el mensaje `"Evento de autenticación: LOGIN FALLIDO"`, que en el código fuente solo se emite
desde ese controlador tras un `BCrypt.Verify` fallido.

**¿Qué usuarios estuvieron involucrados?**
Extraídos del campo `Usuario:` en cada línea:

| Usuario probado | Intentos fallidos |
|---|---|
| `admin` | 10 |
| `Admin` (con mayúscula) | 1 |
| `user1` | 2 |
| `user2` | 1 |
| `user` | 1 |

El intento con `Admin` (mayúscula inicial) junto a `admin` en minúscula sugiere un **intento de
enumeración/fuerza bruta con variaciones del mismo nombre de usuario**, consistente con un ataque
automatizado o manual probando credenciales conocidas por defecto.

Con la consulta complementaria `{job="vulnerableapp", category="Security"} |= "LOGIN EXITOSO"` se
confirma que el atacante (o el propio usuario legítimo) **eventualmente tuvo éxito**:
```
2026-07-28 14:59:19.355 -06:00 [INF] Evento de autenticación: LOGIN EXITOSO. Usuario:admin UserId:1 IP:::1
2026-07-28 15:12:02.755 -06:00 [INF] Evento de autenticación: LOGIN EXITOSO. Usuario:admin UserId:1 IP:::1
2026-07-28 15:12:18.624 -06:00 [INF] Evento de autenticación: LOGIN EXITOSO. Usuario:user1 UserId:2 IP:::1
```
Es decir, tras **10 fallos consecutivos sobre la cuenta `admin`**, esa cuenta finalmente logró
autenticarse — un patrón clásico de fuerza bruta exitosa o de un usuario legítimo que olvidó su
contraseña, indistinguible solo con estos logs (se necesitaría correlacionar la IP de origen real,
no `::1`/loopback, ya que aquí las pruebas se hicieron localmente).

**¿Qué excepción se registró?**
**Ninguna.** Los intentos fallidos se registran únicamente en nivel `[WRN]`, no `[ERR]` — no hay
excepción de por medio, ya que `BCrypt.Net.BCrypt.Verify()` simplemente retorna `false` ante
credenciales inválidas, sin lanzar una excepción. Esto se confirma con la consulta
`{job="vulnerableapp"} |= "[ERR]"`, que no devuelve resultados en la ventana analizada. La única
otra advertencia `[WRN]` presente en el mismo periodo, no relacionada con autenticación, es una
advertencia de configuración de Entity Framework Core sobre la propiedad `Balance` sin tipo de
columna explícito — ruido de esquema, sin relación con el incidente de seguridad.

**¿Qué evidencia respalda estas conclusiones?**
Las tres capturas de Grafana Explore adjuntas por el estudiante:
- `Explore-logs-2026-07-28_15_14_50.txt` — timeline completo de eventos `Security` (login/logout, éxitos y fallos)
- `Explore-logs-2026-07-28_15_15_33.txt` / `15_15_21.txt` — aislamiento de los `LOGIN FALLIDO`
- Ausencia de resultados en `{job="vulnerableapp"} |= "[ERR]"` (no adjunta por no arrojar líneas)

**Conclusión del incidente:** se detectó un patrón de fuerza bruta / prueba de credenciales contra
la cuenta `admin` entre las `14:24` y `14:42` (10 intentos fallidos), seguido de un login exitoso a
las `14:59:19`. No se generaron excepciones de aplicación; el mecanismo de autenticación se
comportó como se esperaba (rechazando credenciales inválidas sin fallos), pero **no hay evidencia
de bloqueo de cuenta ni rate-limiting** — la cuenta `admin` pudo seguir intentando indefinidamente
sin ninguna restricción, lo cual es en sí mismo un hallazgo de seguridad a documentar (ausencia de
protección contra fuerza bruta, CWE-307).

## 5. Dashboard — 3 paneles

Ver `dashboard-seguridad.json` (importable directo en Grafana: **Dashboards → New → Import →
pegar JSON**). Incluye:

| Panel | Consulta | Propósito |
|---|---|---|
| **Errores en el tiempo** | `sum(count_over_time({job="vulnerableapp"} \|= "[ERR]" [5m]))` | Gráfica de barras/series para detectar picos de errores y correlacionarlos con despliegues o incidentes |
| **Autenticaciones (éxito vs fallo)** | `sum by (resultado) (count_over_time({job="vulnerableapp", category="Security"} \|= "LOGIN" [5m]))` con label derivado `resultado` | Detectar patrones de fuerza bruta o cuentas comprometidas (muchos fallos seguidos de un éxito) |
| **Auditoría reciente** | `{job="vulnerableapp", category="Audit"}` (panel tipo Logs) | Trazabilidad de operaciones CRUD para investigación forense |

## 6. Tabla comparativa Seq vs Grafana + Loki

| Característica | Seq | Grafana + Loki |
|---|---|---|
| Búsqueda textual | Motor propio sobre eventos estructurados (sintaxis tipo SQL) | LogQL: filtros de texto (`\|=`, `\|~`, `!=`) aplicados sobre streams ya reducidos por labels |
| Filtros por labels | No usa labels; filtra por propiedades estructuradas del evento JSON/CLEF | Labels indexados nativamente (`job`, `category`, etc.), base de cómo Loki organiza y acelera las consultas |
| Dashboards | Paneles simples orientados a una sola aplicación | Dashboards ricos y reutilizables, capaces de combinar múltiples fuentes (logs, métricas, trazas) en un solo panel |
| Investigación de incidentes | Fuerte para inspeccionar el detalle de eventos individuales de una app .NET | Fuerte para correlacionar volumen/tendencias entre múltiples servicios y ventanas de tiempo |
| Uso principal | Debugging profundo durante desarrollo de una aplicación específica | Observabilidad centralizada de una plataforma completa en producción |

**Conclusión:** Seq es superior para el desarrollador que necesita inspeccionar el detalle
estructurado de un evento .NET puntual durante desarrollo. Grafana + Loki es superior para
operación/producción, donde se necesita correlacionar tendencias, generar alertas, y tener una
vista unificada de múltiples servicios sin depender de que cada uno tenga su propio visor.

## 7. Pregunta de reflexión

**¿Qué ventajas aporta LogQL frente a revisar archivos de texto directamente o hacer búsquedas
simples en Seq?**

Revisar archivos de texto obliga a usar herramientas ad-hoc (`grep`, `Select-String`) sin
indexado, por lo que cada búsqueda escanea el archivo completo línea por línea, sin memoria de
consultas anteriores y sin forma de correlacionar múltiples archivos o servicios a la vez. LogQL,
al basarse en labels indexados, reduce primero el conjunto de datos relevante antes de aplicar
cualquier filtro de texto, lo que lo hace escalable incluso con millones de líneas. Además,
LogQL permite funciones de agregación temporal (`count_over_time`, `rate`, `sum by (...)`) que no
existen en una simple búsqueda de texto — esto es lo que permite construir gráficas de
tendencias (por ejemplo, "errores por minuto") en vez de solo listar coincidencias. Comparado con
Seq, LogQL gana en la capacidad de construir dashboards visuales reutilizables y de combinar
logs con otras fuentes de datos (métricas de Prometheus, por ejemplo) en el mismo panel — algo
que Seq, al ser una herramienta dedicada solo a logs de una aplicación, no ofrece nativamente.

## 8. Reto — Dashboard de monitoreo de seguridad

Incluido en `dashboard-seguridad.json`: 3 paneles — **Autenticaciones fallidas** (serie temporal
con `count_over_time({job="vulnerableapp", category="Security"} |= "LOGIN FALLIDO" [5m])`),
**Eventos de auditoría** (panel de logs con `{job="vulnerableapp", category="Audit"}`), y
**Errores críticos** (serie temporal con `count_over_time({job="vulnerableapp"} |= "[ERR]" [5m])`).

**Uso en producción:** este dashboard se dejaría abierto en una pantalla de monitoreo (o
configurado con alertas de Grafana) para que el equipo de operaciones detecte en tiempo real
picos de intentos de login fallidos (posible fuerza bruta o credential stuffing), errores
críticos recurrentes (posible caída parcial del servicio o bug en producción), y mantenga
trazabilidad continua de las operaciones de auditoría sin tener que revisar logs manualmente.
Con Grafana Alerting, cada panel podría disparar una notificación (Slack, email) al superar un
umbral, por ejemplo más de 10 logins fallidos en 5 minutos desde el mismo usuario.

## 9. Evidencias pendientes de capturar

- [ ] Grafana/Loki/Promtail corriendo (Paso 1)
- [ ] Interfaz de Explore con cada elemento señalado (Paso 2)
- [ ] Las 6 consultas de la tabla de sintaxis básica ejecutadas
- [x] Resultado real del caso de estudio (timestamps, usuarios, excepción) — completado en sección 4
- [ ] Dashboard importado con los 3 paneles mostrando datos reales
- [ ] Dashboard del reto (seguridad) con datos reales
