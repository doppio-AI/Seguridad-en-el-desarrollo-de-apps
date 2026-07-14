# Practica P3G - Serilog e instrumentacion de controladores

## Objetivo

Configurar Serilog como plataforma centralizada de logging para `VulnerableApp`,
enviar eventos a consola, archivo y Seq, e instrumentar todas las acciones de los
cinco controladores sin registrar contrasenas.

## Configuracion implementada

- Serilog se inicializa desde `Program.cs` y lee su configuracion de
  `appsettings.json`.
- El nivel base es `Information`; `appsettings.Development.json` lo cambia a
  `Debug` para mostrar como un ambiente puede aceptar eventos mas detallados.
- Se habilito `FromLogContext` y se agrego la propiedad global
  `Application=VulnerableApp`.
- `UseSerilogRequestLogging()` registra metodo, ruta, estado HTTP y duracion de
  cada solicitud, enriquecidos con usuario de sesion e IP.
- Seq se ejecuta en Docker y expone ingestion en `http://localhost:5341` e
  interfaz en `http://localhost:8081`.

### Funcion de cada sink

| Sink | Funcion | Uso en la practica |
| --- | --- | --- |
| Console | Escribe los eventos en la terminal del proceso. | Diagnostico inmediato durante desarrollo. |
| File | Persiste eventos en archivos de texto. | `Logs/log-.txt`, con rotacion diaria y retencion de 14 archivos. |
| Seq | Envia eventos estructurados al servidor Seq. | Consultas por controlador, nivel, usuario, IP y demas propiedades. |

El enriquecedor adicional `Application=VulnerableApp` identifica el origen
global de los eventos. Es util cuando una misma instancia de Seq recibe logs de
varias aplicaciones. `FromLogContext` conserva propiedades contextuales durante
una solicitud.

## Instrumentacion segura

`InstrumentedController<TController>` centraliza el patron comun de cada accion:

1. Registra entrada con controlador, accion, usuario, IP y parametros seguros.
2. Mide el tiempo con `Stopwatch`.
3. Registra excepciones con `LogError` y conserva el objeto `Exception`.
4. Registra la salida en `finally`, incluido el resultado y `DuracionMs`.

Se instrumentaron 14 acciones:

| Controlador | Acciones |
| --- | --- |
| HomeController | `Index`, `Privacy`, `ControlledException`, `UnhandledException`, `Error` |
| SearchController | `Index` |
| AuthController | `Login` GET/POST, `Dashboard`, `Logout` |
| CommentController | `Index`, `AddComment` |
| ApiController | `GetUser`, `GetAllUsers` |

Los mensajes usan plantillas estructuradas y no interpolacion de cadenas. El
login solo envia `Username` como parametro seguro; `password` nunca se pasa al
logger. Los comentarios se registran por longitud y no por contenido.

## Pruebas y resultados

Comando ejecutado:

```powershell
dotnet test VulnerableApp.Tests\VulnerableApp.Tests.csproj `
  --artifacts-path .artifacts --no-restore
```

Resultado final:

```text
Superado: 17
Con error: 0
Omitido: 0
```

Las pruebas recorren las 14 acciones y validan:

- eventos de entrada y salida en nivel `Information`;
- presencia de `DuracionMs`;
- eventos `Warning` para entradas invalidas y accesos no autorizados;
- evento `Error` y salida cuando una dependencia lanza una excepcion;
- autenticacion fallida y exitosa;
- ausencia literal de las contrasenas señuelo en todos los eventos capturados.

La revision del archivo `Logs/log-20260702.txt` tambien obtuvo cero
coincidencias para las contrasenas utilizadas durante el recorrido real.

### Cambio del nivel minimo

| Configuracion | Nivel minimo | Diferencia observable |
| --- | --- | --- |
| `appsettings.json` | `Information` | Acepta Information, Warning, Error y Fatal; omite Debug y Verbose. |
| `appsettings.Development.json` | `Debug` | Agrega eventos Debug durante desarrollo, conservando los overrides de Microsoft/System en Warning. |

Esto permite aumentar detalle localmente sin elevar el volumen de logs en el
ambiente base.

## Evidencias

- Aplicacion y formulario de autenticacion:
  [`01-vulnerableapp-login.png`](../evidencias/P3G/01-vulnerableapp-login.png)
- Seq filtrado por `HomeController`:
  [`02-seq-homecontroller.png`](../evidencias/P3G/02-seq-homecontroller.png)
- Seq filtrado por `SearchController`:
  [`03-seq-searchcontroller.png`](../evidencias/P3G/03-seq-searchcontroller.png)
- Seq filtrado por `AuthController`:
  [`04-seq-authcontroller.png`](../evidencias/P3G/04-seq-authcontroller.png)
- Seq filtrado por `CommentController`:
  [`05-seq-commentcontroller.png`](../evidencias/P3G/05-seq-commentcontroller.png)
- Seq filtrado por `ApiController`:
  [`06-seq-apicontroller.png`](../evidencias/P3G/06-seq-apicontroller.png)
- Seq filtrado por nivel `Error`:
  [`07-seq-errores-excepciones.png`](../evidencias/P3G/07-seq-errores-excepciones.png)

Las consultas por controlador usan:

```text
SourceContext = 'VulnerableApp.Controllers.NombreController'
```

## Hallazgos

1. Los eventos estructurados permiten separar `User`, `IP`, `Parameters`,
   `Outcome` y `ElapsedMs` en Seq, en lugar de almacenar una sola cadena.
2. Se generaron y observaron eventos Information, Warning y Error.
3. El hallazgo inicial de `ApiController.GetUser(2)` fue corregido: la accion
   devuelve explicitamente HTTP 403 y ya no depende de un esquema de
   autenticacion para procesar `ForbidResult`.
4. El `docker run` de la guia ya no es suficiente para la imagen actual de Seq:
   el primer inicio exige una contrasena administrativa o
   `SEQ_FIRSTRUN_NOAUTHENTICATION=true`. Para esta practica local se eligio la
   segunda opcion y no se almaceno ninguna credencial en Git.

## Lista de verificacion

- [x] Paquetes Serilog instalados.
- [x] Configuracion centralizada en `appsettings.json`.
- [x] Consola, archivo rotativo y Seq habilitados.
- [x] Carpeta `Logs` creada.
- [x] Seq levantado y conectividad validada.
- [x] Enriquecedor adicional agregado y justificado.
- [x] Cinco controladores y 14 acciones instrumentados.
- [x] Usuario, IP, parametros seguros y tiempos registrados.
- [x] Warnings, errores, autenticacion y excepciones validados.
- [x] Contrasenas ausentes de los logs.
- [x] Capturas y reporte tecnico generados.
