# Sistema para Administración del Asilo de Ancianos "Cabeza de Algodón"

Proyecto Final — Análisis y Diseño de Sistemas II
Universidad Mariano Gálvez de Guatemala, Centro Regional de Mazatenango
Docente: Ing. Angel Atilio Maltez Cifuentes

**Christopher Alexey Robles Morales** — Carné 3090-23-13774

---

## Qué resuelve

El asilo lleva sus operaciones en papel: una ficha física por interno, coordinación manual con la fundación de apoyo para las consultas con especialistas, y consolidación a mano de cobros, donaciones y deuda con la fundación. El sistema digitaliza y centraliza esos tres frentes.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core Web API REST (.NET 10) |
| Acceso a datos | Dapper (SQL escrito a mano, sin ORM de alto nivel) |
| Base de datos | Microsoft SQL Server |
| Frontend | HTML, CSS y JavaScript (cliente estático) |
| Documentación de API | Swagger / OpenAPI |
| Despliegue previsto | Microsoft Azure (App Service + Azure SQL) |

## Estructura

```
PROYECTO_FINAL_DESARROLLO/
├── Asilo.Api/            API REST
│   ├── Controllers/      Endpoints HTTP
│   ├── Models/           Entidades que mapean a las tablas
│   ├── DTOs/             Objetos de entrada y salida de la API
│   ├── Data/             DapperContext (fábrica de conexiones)
│   ├── Repositories/     Todo el SQL del sistema vive aquí
│   └── Program.cs        Inyección de dependencias y pipeline
├── database/             Scripts SQL numerados y versionados
└── frontend/             Cliente estático que consume la API
```

## Cómo levantarlo

### 1. Base de datos

Ejecutar en SQL Server Management Studio, en orden:

```
database/01_CrearBaseDatos.sql
database/02_TablasCatalogo.sql
database/03_TablasClinicas.sql
database/04_TablasFinancieras.sql
database/05_Auditoria_Triggers.sql
database/06_DatosSemilla.sql
```

El orden importa: las llaves foráneas exigen que las tablas referenciadas existan antes.

Para verificar la instalación: `database/00_VerificarInstalacion.sql`

### 2. API

```powershell
cd Asilo.Api
dotnet restore
dotnet run
```

Swagger queda en `http://localhost:5177/swagger`

Si la cadena de conexión no funciona, ajustar `Server=` en `appsettings.json` al nombre de la instancia local de SQL Server.

### 3. Frontend

Abrir `frontend/panel-clinico.html` con la extensión **Live Server** de VS Code.
No abrir con doble clic: el protocolo `file://` bloquea las peticiones por CORS.

El puerto de la API se configura en `frontend/js/api.js`.

## Estado de avance

| Módulo | Estado |
|---|---|
| Esquema de base de datos completo (14 tablas, FK, CHECK, triggers) | Completo |
| Residentes: alta, consulta, edición y baja lógica | Completo |
| Remisiones a especialista: alta, consulta y cambio de estado | Completo |
| Interfaz de residentes y remisiones conectada a la API | Completo |
| Citas de fundación | Pendiente |
| Laboratorio y farmacia | Pendiente |
| Calculadora financiera y caja | Pendiente |
| Autenticación JWT y autorización por roles | Pendiente |
| Microservicio de notificaciones | Pendiente |
| Despliegue en Azure | Pendiente |

## Decisiones de diseño documentadas

- **Dapper en lugar de Entity Framework Core**: las consultas de agregación financiera y los reportes cruzados se expresan con naturalidad en SQL. Evita además mantener dos mecanismos de persistencia.
- **Auditoría con triggers de SQL Server** y no con un interceptor de la aplicación: así quedan registrados también los cambios hechos desde herramientas externas.
- **Web API REST en lugar de MVC con Razor**: frontera explícita entre presentación y negocio, y permite incorporar otros clientes sin tocar el backend.
