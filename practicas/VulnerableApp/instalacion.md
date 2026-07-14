# VulnerableApp - Instalacion y ejecucion

Aplicacion ASP.NET Core MVC educativa para las practicas SEGG-U1-P2A, SEGG-U1-P2B y SEGG-U1-P2C.

## Requisitos

- .NET 10 SDK
- SQL Server LocalDB
- Entity Framework Core Tools

## Comandos usados

```powershell
dotnet new mvc -n VulnerableApp -f net10.0
cd VulnerableApp
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.8
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.8
dotnet tool run dotnet-ef migrations add InitialCreate
dotnet tool run dotnet-ef database update
dotnet build
dotnet run
```

## Rutas de prueba

- `/Search/Index`: busqueda vulnerable a SQL Injection.
- `/Auth/Login`: login vulnerable con credenciales predeterminadas y consulta concatenada.
- `/Auth/Dashboard`: dashboard accesible despues de autenticacion.

## Usuarios semilla

| Usuario | Contrasena | Email | Balance |
| --- | --- | --- | --- |
| admin | admin | admin@test.com | 1000 |
| user1 | 123456 | user@test.com | 500 |
| user2 | password | user2@test.com | 750 |
