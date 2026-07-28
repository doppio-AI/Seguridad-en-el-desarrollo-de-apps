# SEGG-U2-P4H-1 — Plataforma de Observabilidad con Grafana, Loki y Promtail

## 1. Arquitectura implementada

VulnerableApp continúa ejecutándose de forma local (`dotnet run`), sin contenerizar, generando logs vía Serilog a tres destinos: consola, archivos de texto (`Logs/*.txt`) y Seq. Se añadió un stack independiente en Docker (`docker-compose.observability.yml`) con:

- **Loki** (`grafana/loki:2.9.8`) — almacenamiento e indexado de logs.
- **Promtail** (`grafana/promtail:2.9.8`) — agente que lee `Logs/*.txt` mediante bind mount de solo lectura y los envía a Loki.
- **Grafana** (`grafana/grafana:11.1.0`) — visualización, conectado a Loki como data source.

## 2. Bind mount de la carpeta Logs

```yaml
volumes:
  - ./Logs:/var/log/vulnerableapp:ro
```

Es necesario porque Promtail corre **dentro de un contenedor Docker**, aislado del sistema de archivos de Windows. El bind mount expone la carpeta `Logs/` del proyecto (donde Serilog ya escribe) dentro del contenedor, en modo solo lectura (`:ro`) para que Promtail únicamente consuma los archivos sin poder modificarlos ni el host exponer más de lo necesario. Sin este mount, Promtail no tendría forma de acceder a los archivos generados por la aplicación en el host.

## 3. Reto — Labels por categoría (Security / Audit / General)

**¿Qué modificaciones se realizaron en Promtail?**
Se agregó un bloque `pipeline_stages` al `scrape_config` con tres etapas:
1. `regex` — detecta si la línea contiene el texto `"Evento de autenticación"` (eventos de login/logout) o palabras clave de operaciones (`"Comentario agregado"`, `"resultados encontrados"`, `"LOGIN EXITOSO"`, `"LOGIN FALLIDO"`, `"LOGOUT"`).
2. `template` — construye un valor `Security`, `Audit` o `General` según qué grupo de la regex hizo match.
3. `labels` — promueve ese valor calculado a un label real de Loki llamado `category`, indexable y filtrable.

**¿Cómo utiliza Loki las labels?**
Loki indexa únicamente las labels (no el contenido completo de cada línea), y las usa para organizar los logs en "streams" — un stream es la combinación única de todos los valores de labels (`job`, `app`, `category`, etc.). Cada línea de log se almacena comprimida dentro del stream correspondiente. Esto es lo que permite que las consultas por label sean extremadamente rápidas, ya que Loki no necesita indexar el texto completo (a diferencia de Elasticsearch, por ejemplo).

**¿Qué ventajas ofrecen para consultas con LogQL?**
Permiten filtrar de forma instantánea sin tener que escanear todo el volumen de logs, por ejemplo:
```
{job="vulnerableapp", category="Audit"}
{job="vulnerableapp", category="Security"} |= "FALLIDO"
```
La primera parte del filtro (labels) reduce el conjunto de streams a revisar antes de aplicar cualquier búsqueda de texto adicional (`|=`), lo que hace las consultas más eficientes que un `grep` sobre archivos completos.

## 4. Tabla comparativa — Seq vs Grafana + Loki

| Característica | Seq | Grafana + Loki |
|---|---|---|
| Búsqueda textual | Motor propio, sintaxis SQL-like sobre eventos estructurados (CLEF/JSON) | LogQL, con indexado por labels y filtros de texto (`\|=`, `\|~`) sobre el stream ya reducido |
| Dashboards | Paneles básicos propios, orientados a eventos de aplicación | Dashboards completos y altamente personalizables, con soporte para combinar múltiples fuentes de datos (no solo logs) |
| Alertas | Sí, basadas en consultas guardadas sobre eventos | Sí, mediante Grafana Alerting, con más opciones de enrutamiento (Slack, email, PagerDuty, etc.) |
| Consultas | Orientadas a eventos estructurados individuales (ideal para debugging de una app específica) | Orientadas a series temporales y volumen/tendencias de logs entre múltiples fuentes |
| Visualización | Enfocada en el detalle de cada evento (propiedades estructuradas de Serilog) | Enfocada en tendencias, correlación temporal y comparación entre múltiples servicios |
| Uso principal | Debugging profundo de una sola aplicación .NET durante desarrollo | Observabilidad centralizada de una plataforma completa (múltiples apps/servicios) en producción |

## 5. Pregunta de reflexión

**¿Qué ventajas ofrece incorporar una plataforma de observabilidad sin modificar la aplicación existente? ¿Cómo facilita la evolución de un sistema en producción?**

Desacoplar la observabilidad de la lógica de negocio permite que el equipo de plataforma/DevOps evolucione las herramientas de monitoreo (cambiar de Seq a Grafana+Loki, agregar Prometheus para métricas, etc.) sin requerir que el equipo de desarrollo toque, recompile o vuelva a desplegar la aplicación. La app solo necesita seguir escribiendo logs a un destino estable (consola, archivo, o un sink como Seq); todo lo demás —recolección, almacenamiento, indexado y visualización— ocurre en una capa de infraestructura independiente.

Esto reduce significativamente el riesgo de cambios: no hay que tocar código productivo para mejorar la observabilidad, lo cual es clave en sistemas críticos donde cada despliegue implica riesgo. También permite escalar horizontalmente: a medida que se agregan más microservicios o instancias, todos pueden apuntar al mismo stack de Loki/Grafana sin cambios arquitectónicos, logrando una vista centralizada de toda la plataforma en vez de herramientas aisladas por servicio.

## 6. Evidencias a incluir en la entrega

- [ ] Captura de VulnerableApp generando registros (consola o terminal de `dotnet run`)
- [ ] Captura de los archivos en `Logs\`
- [ ] Captura de Seq mostrando los eventos
- [ ] Captura de Grafana con Loki configurado como data source (Save & Test exitoso)
- [ ] Captura de la consulta `{job="vulnerableapp", category="Audit"}` en Grafana Explore
- [ ] Salida de `docker compose -f docker-compose.observability.yml ps`
- [ ] `docker-compose.observability.yml` final
- [ ] `promtail-config.yaml` final (con pipeline_stages)
