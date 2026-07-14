# VulnerableApp - Rama secure

Aplicacion ASP.NET Core MVC para laboratorio educativo de vulnerabilidades OWASP y remediacion.

## Alcance

Esta aplicacion debe ejecutarse solo en entorno local de laboratorio. No debe usarse para probar sistemas, servicios, redes o cuentas reales.

## Requisitos

- .NET 10 SDK
- SQL Server LocalDB
- Herramienta local `dotnet-ef` restaurada desde `dotnet-tools.json`

## Preparacion

```powershell
cd C:\Projects\cuatrimestre8\sdapps\SDA\practicas\VulnerableApp
dotnet tool restore
dotnet restore
dotnet tool run dotnet-ef database update
dotnet build
```

## Ejecucion segura

```powershell
dotnet run --launch-profile https
```

Abrir en el navegador:

- `https://localhost:7243/Auth/Login`
- `https://localhost:7243/Search/Index`
- `https://localhost:7243/Comment/Index`

## Credenciales seguras de prueba

Las credenciales inseguras anteriores, como `admin/admin`, ya no deben permitir acceso.

| Usuario | Contrasena segura |
| --- | --- |
| admin | Admin#2026! |
| user1 | User1#2026! |
| user2 | User2#2026! |

## Mitigaciones incluidas

| Vulnerabilidad | Control aplicado |
| --- | --- |
| SQL Injection | Busqueda con LINQ en lugar de SQL concatenado |
| Autenticacion insegura | Validacion con usuario y `PasswordHash` BCrypt |
| Contrasenas en texto plano | Campo `PasswordHash`; no se guarda `Password` |
| XSS | Salida codificada con `Html.Encode` |
| IDOR | Validacion de sesion y ownership en `/api/user/{id}` |
| API expuesta | Respuestas sin `Password` ni `PasswordHash` |

## Pruebas de mitigacion esperadas

| Prueba | Resultado esperado |
| --- | --- |
| Buscar `' OR '1'='1` | No debe devolver todos los usuarios |
| Login `admin/admin` | No debe permitir acceso |
| Comentario `<script>alert('XSS')</script>` | Debe mostrarse como texto, sin ejecutar alerta |
| GET `/api/user/2` autenticado como usuario 1 | Debe responder `403 Forbid` |
| Respuesta de API | No debe incluir `Password` ni `PasswordHash` |
