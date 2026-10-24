# Sistema para Administración del Asilo de Ancianos "Cabeza de Algodón"

Proyecto Final — Análisis y Diseño de Sistemas II
Universidad Mariano Gálvez de Guatemala, Centro Regional de Mazatenango
Docente: Ing. Angel Atilio Maltez Cifuentes

**Christopher Alexey Robles Morales** — Carné 3090-23-13774

---

## Qué resuelve

El asilo lleva sus operaciones en papel: una ficha física por interno, coordinación manual con la fundación de apoyo para las consultas con especialistas, y consolidación a mano de cobros, donaciones y deuda con la fundación. El sistema digitaliza y centraliza esos tres frentes.

El recorrido completo que cubre: el médico registra un hallazgo clínico y emite una remisión → la fundación le asigna cita con un especialista → el especialista atiende, deja su diagnóstico y ordena exámenes o receta medicamentos → laboratorio y farmacia ejecutan y cargan sus costos → al cierre del mes se emite la factura del residente, con el descuento del convenio aplicado → la familia recibe aviso por correo en cada uno de esos momentos.

## Sistema publicado

```
https://asilo-cabeza-de-algodon-cr-d5egf2dcc6cbh9d3.canadacentral-01.azurewebsites.net
```

Documentación de la API en `/swagger`. Sonda de diagnóstico en `/api/salud`, que responde si la aplicación está arriba y si la base contesta.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core Web API REST (.NET 10) |
| Acceso a datos | Dapper (SQL escrito a mano, sin ORM de alto nivel) |
| Base de datos | Microsoft SQL Server / Azure SQL Database |
| Autenticación | JWT Bearer, contraseñas con BCrypt |
| Correo | MailKit sobre SMTP |
| Frontend | HTML, CSS y JavaScript (cliente estático) |
| Documentación de API | Swagger / OpenAPI |
| Despliegue | Azure App Service + Azure SQL Database |

## Estructura

```
PROYECTO_FINAL_DESARROLLO/
├── Asilo.Api/            API REST
│   ├── Controllers/      Endpoints HTTP
│   ├── Models/           Entidades que mapean a las tablas
│   ├── DTOs/             Objetos de entrada y salida de la API
│   ├── Data/             DapperContext (fábrica de conexiones)
│   ├── Repositories/     Todo el SQL del sistema vive aquí
│   ├── Services/         Token, semilla de seguridad y notificaciones
│   └── Program.cs        Inyección de dependencias y pipeline
├── database/             Scripts SQL numerados y versionados
└── frontend/             Cliente estático que consume la API
```

## Estado de avance

| Módulo | Estado |
|---|---|
| Esquema de base de datos (FK, CHECK, índices filtrados, triggers de auditoría) | Completo |
| Residentes: alta, consulta, edición y baja lógica | Completo |
| Familiares y contactos de emergencia | Completo |
| Remisiones a especialista | Completo |
| Citas de fundación y bitácora de atención | Completo |
| Laboratorio: el especialista ordena, el técnico carga resultado y costo | Completo |
| Farmacia: el especialista receta, farmacia despacha y carga costo | Completo |
| Calculadora financiera, facturación y pagos parciales | Completo |
| Donaciones y gastos operativos | Completo |
| Reportes (RF-09): cinco reportes con filtros e impresión | Completo |
| Autenticación JWT y autorización por roles | Completo |
| Administración de usuarios del sistema | Completo |
| Notificaciones a la familia con patrón Outbox | Completo |
| Despliegue en Azure | Completo |

### Pendientes conocidos

Quedan anotados como trabajo futuro, no como carencias del alcance entregado:

- **Paginación del lado del servidor.** Hoy la API devuelve el conjunto completo y el cliente recorta. El `ORDER BY` de cada consulta ya incluye desempate por `Id`, que es el requisito para paginar sin que las filas salten entre páginas.
- **Manejador global de excepciones.** Una excepción no prevista hoy sale como 500 con su texto crudo.
- **`IAnexoRepository` agrupa laboratorio y farmacia** y debería ser una interfaz por cada uno.
- **Parámetro de cuota base.** La cuota mensual de estancia se captura por residente; una tabla de parámetros permitiría un valor por omisión administrable.
- **Estado `EnEvaluacion`.** Aparece en el diagrama de estados y ninguna transición llega a él; falta decidir si se implementa o se retira del diagrama.

## Roles

| Rol | Qué hace |
|---|---|
| Administrador | Acceso completo: admisiones, facturación, caja, usuarios, reportes |
| Médico general | Registro clínico del residente y emisión de remisiones |
| Fundación | Asigna las citas con especialista según la remisión |
| Especialista | Atiende la consulta, diagnostica, ordena exámenes y receta |
| Técnico de laboratorio y farmacia | Ejecuta las órdenes y carga resultados y costos |

El acceso se decide en el servidor con `[Authorize(Roles = ...)]` sobre cada endpoint. El menú lateral se filtra por rol, pero eso es comodidad de presentación: esconder un enlace no protege nada, y la API responde 403 aunque se escriba la dirección a mano. Los datos clínicos sensibles se recortan en la respuesta según quién pregunta — la fundación no ve diagnósticos y el técnico tampoco ve sintomatología.

## Cómo levantarlo en local

### 1. Base de datos

Ejecutar en SQL Server Management Studio, en orden:

```
database/01_CrearBaseDatos.sql
database/02_TablasCatalogo.sql
database/03_TablasClinicas.sql
database/04_TablasFinancieras.sql
database/05_Auditoria_Triggers.sql
database/06_DatosSemilla.sql
database/07_CorregirRemisionesSinCita.sql
database/08_RequiereAnexos.sql
database/09_CatalogosYDpi.sql
database/10_Facturacion.sql
database/11_CorregirCuotaMensual.sql
database/12_FacturaComplementaria.sql
database/13_RecetaYDespacho.sql
```

El orden importa: las llaves foráneas exigen que las tablas referenciadas existan antes, y del 07 en adelante son cambios incrementales sobre lo anterior.

Para verificar la instalación: `database/00_VerificarInstalacion.sql`

Para una base nueva en Azure SQL existe `database/99_AzureSQL_Completo.sql`, que reúne del 02 al 13 en un solo archivo sin las sentencias `USE`: Azure SQL no admite cambiar de base dentro de una sesión, porque cada base es su propio punto de conexión.

### 2. API

```powershell
cd Asilo.Api
dotnet restore
dotnet run
```

Swagger queda en `http://localhost:5177/swagger`

Si la cadena de conexión no funciona, ajustar `Server=` en `appsettings.json` al nombre de la instancia local de SQL Server.

### 3. Frontend

Abrir `frontend/login.html` con la extensión **Live Server** de VS Code.
No abrir con doble clic: el protocolo `file://` bloquea las peticiones por CORS.

`api.js` detecta solo si está corriendo bajo Live Server o servido por la propia API, de modo que no hay que cambiarle la dirección al desplegar.

### 4. Primer acceso

Los usuarios de la semilla nacen sin contraseña, porque un hash de BCrypt no se puede escribir a mano en un script SQL: lo calcula la librería, con su propia sal. Al arrancar, la aplicación le asigna a cada uno el valor de `Seguridad:PasswordInicial` y lo registra en el log.

Después de entrar y cambiar las contraseñas desde el panel de usuarios, esa clave de configuración se quita. La columna *Acceso* del panel muestra qué cuentas siguen sin contraseña definida.

### 5. Correo (opcional en local)

Si la sección `Correo` de la configuración está completa, los avisos salen por SMTP; si no, se escriben en el log. Lo decide la configuración y no una línea del código, así que el mismo binario sirve para las dos cosas.

Para probar con Gmail hace falta una contraseña de aplicación, no la de la cuenta. Va en `appsettings.Development.json`, que está en el `.gitignore`. El campo `RedirigirTodoA` manda todos los avisos a una sola bandeja, con el destinatario real en el asunto, para probar sin escribirle a nadie.

## Decisiones de diseño documentadas

**Dapper en lugar de Entity Framework Core.** Las consultas de agregación financiera y los reportes cruzados se expresan con naturalidad en SQL y de forma forzada en LINQ. Evita además mantener dos mecanismos de persistencia. Todo el SQL vive en la carpeta `Repositories`.

**Web API REST en lugar de MVC con Razor.** Frontera explícita entre presentación y negocio, y permite incorporar otros clientes sin tocar el backend.

**Auditoría con triggers de SQL Server** y no con un interceptor de la aplicación: así quedan registrados también los cambios hechos desde herramientas externas.

**JWT en lugar de Identity con cookies.** Es consecuencia de la decisión anterior: al ser una API REST no hay sesión de servidor que proteger. El token lleva el rol en una afirmación y los atributos de autorización deciden sobre ella.

**Reglas de negocio en el esquema y no solo en el código.** Un índice único filtrado impide dos cuotas de estancia en el mismo período; restricciones `CHECK` emparejadas impiden que un estado y su fecha se contradigan, y que algo se entregue sin precio. Lo que la base rechaza no depende de que nadie olvide una validación.

**Patrón Outbox para las notificaciones.** La cita se guarda en SQL Server y el correo sale por otro medio: son dos sistemas y no hay transacción que abarque a los dos. Enviar dentro de la operación hace que un fallo del correo tumbe la cita; enviar después del commit deja la cita guardada y a nadie avisado si hay un corte en medio. Escribir el aviso en una tabla, en la misma transacción, elimina las dos posibilidades. Un proceso en segundo plano los entrega después, con reintentos y tope.

**El contenido del aviso es una fotografía del hecho**, no una referencia a la fila. Si guardara solo el identificador de la cita, el despachador tendría que releerla al entregar, y para entonces el horario pudo haber cambiado.

**El canal de notificación es una interfaz.** Mandar correo o escribir en el log es la misma operación vista desde el despachador, y cuál se usa lo decide la configuración.

**PDF por impresión del navegador** en lugar de generarlo en el servidor. El requerimiento pide generar reportes; la librería de PDF era una nota de implementación del documento de análisis, no un requerimiento. La salida es sustituible sin tocar las consultas, que son la parte sustantiva.

**Un solo App Service para API y frontend.** El frontend se sirve desde `wwwroot` de la misma aplicación: un servicio en lugar de dos, y al compartir origen desaparece el problema de CORS.

## Registro de cambios

`Registro_de_Cambios_Fase_Desarrollo.md` documenta las veintinueve diferencias entre lo que decía el documento de análisis y lo que exigió la construcción, cada una con cómo se detectó, qué ocurría en el código, qué se implementó y qué habría pasado sin el cambio.
