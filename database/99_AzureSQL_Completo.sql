/* ============================================================
   99_AzureSQL_Completo.sql
   Los scripts 02 al 13 en uno solo, para correrlos contra Azure
   SQL Database.

   El unico cambio respecto a los originales es que quite las
   lineas USE: en Azure SQL cada base es su propio punto de
   conexion y no se puede cambiar de base dentro de una sesion,
   asi que USE da error. La base la elijo al conectarme.

   Lo corro una sola vez, con la base AsiloCabezaAlgodonDB ya
   creada en el portal y seleccionada en la conexion.
   ============================================================ */


/* ---------- 02_TablasCatalogo.sql ---------- */
/* ============================================================
   02_TablasCatalogo.sql
   Tablas que no dependen de ninguna otra. Van primero porque
   Remisiones y Facturas las referencian con llaves foraneas.
   ============================================================ */

/* ---------------- USUARIOS ----------------
   Sustituye a ASP.NET Core Identity. La contrasena se guarda
   hasheada con BCrypt desde la aplicacion. */
IF OBJECT_ID('Usuarios', 'U') IS NULL
CREATE TABLE Usuarios (
    Id             INT IDENTITY(1,1) NOT NULL,
    NombreCompleto VARCHAR(150)      NOT NULL,
    Email          VARCHAR(150)      NOT NULL,
    PasswordHash   VARCHAR(255)      NOT NULL,
    Rol            VARCHAR(50)       NOT NULL,
    Especialidad   VARCHAR(100)      NULL,
    Activo         BIT               NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT 1,
    FechaRegistro  DATETIME          NOT NULL CONSTRAINT DF_Usuarios_Fecha  DEFAULT GETDATE(),

    CONSTRAINT PK_Usuarios       PRIMARY KEY (Id),
    CONSTRAINT UQ_Usuarios_Email UNIQUE (Email),
    /* El CHECK evita que un rol invalido entre aunque la aplicacion falle */
    CONSTRAINT CK_Usuarios_Rol CHECK (Rol IN
        ('Administrador','MedicoGeneral','Fundacion','Especialista','TecnicoLabFarmacia'))
);
GO

/* ---------------- CONVENIOS DE LA FUNDACION ----------------
   Parametriza el descuento de la calculadora financiera.
   El porcentaje no va escrito en el codigo: se lee de aqui. */
IF OBJECT_ID('ConveniosFundacion', 'U') IS NULL
CREATE TABLE ConveniosFundacion (
    Id                  INT IDENTITY(1,1) NOT NULL,
    Descripcion         VARCHAR(200)      NOT NULL,
    PorcentajeDescuento DECIMAL(5,2)      NOT NULL CONSTRAINT DF_Convenios_Pct DEFAULT 0,
    FechaInicio         DATE              NOT NULL,
    FechaFin            DATE              NULL,
    Vigente             BIT               NOT NULL CONSTRAINT DF_Convenios_Vig DEFAULT 1,

    CONSTRAINT PK_ConveniosFundacion PRIMARY KEY (Id),
    CONSTRAINT CK_Convenios_Pct CHECK (PorcentajeDescuento BETWEEN 0 AND 100)
);
GO

/* Indice filtrado: solo puede existir un convenio vigente a la vez.
   Es una regla de negocio que vive en la base, no en el codigo. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Convenios_UnicoVigente')
CREATE UNIQUE INDEX UX_Convenios_UnicoVigente
    ON ConveniosFundacion(Vigente) WHERE Vigente = 1;
GO

PRINT 'Tablas de catalogo listas.';
GO
GO

/* ---------- 03_TablasClinicas.sql ---------- */
/* ============================================================
   03_TablasClinicas.sql
   Expediente del residente y flujo remision -> cita -> anexos.
   Requiere 02_TablasCatalogo.sql ejecutado antes.
   ============================================================ */

/* ---------------- RESIDENTES (RF-01) ---------------- */
IF OBJECT_ID('Residentes', 'U') IS NULL
CREATE TABLE Residentes (
    Id                INT IDENTITY(1,1) NOT NULL,
    Nombres           VARCHAR(100)      NOT NULL,
    Apellidos         VARCHAR(100)      NOT NULL,
    FechaNacimiento   DATE              NOT NULL,
    Psicopatologias   NVARCHAR(MAX)     NULL,
    MedicamentosCajon NVARCHAR(MAX)     NULL,
    CuotaMensual      DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Residentes_Cuota  DEFAULT 0,
    FechaIngreso      DATETIME          NOT NULL CONSTRAINT DF_Residentes_Fecha  DEFAULT GETDATE(),
    Activo            BIT               NOT NULL CONSTRAINT DF_Residentes_Activo DEFAULT 1,

    CONSTRAINT PK_Residentes PRIMARY KEY (Id),
    CONSTRAINT CK_Residentes_Cuota      CHECK (CuotaMensual >= 0),
    CONSTRAINT CK_Residentes_Nacimiento CHECK (FechaNacimiento < GETDATE())
);
GO

/* ---------------- FAMILIARES ---------------- */
IF OBJECT_ID('Familiares', 'U') IS NULL
CREATE TABLE Familiares (
    Id          INT IDENTITY(1,1) NOT NULL,
    ResidenteId INT               NOT NULL,
    Nombre      VARCHAR(150)      NOT NULL,
    Email       VARCHAR(150)      NOT NULL,
    Telefono    VARCHAR(30)       NULL,

    CONSTRAINT PK_Familiares PRIMARY KEY (Id),
    CONSTRAINT FK_Familiares_Residentes FOREIGN KEY (ResidenteId)
        REFERENCES Residentes(Id) ON DELETE NO ACTION
);
GO

CREATE INDEX IX_Familiares_ResidenteId ON Familiares(ResidenteId);
GO

/* ---------------- REMISIONES (RF-02) ----------------
   Estado: 0=Pendiente, 1=Programada, 2=Cancelada */
IF OBJECT_ID('Remisiones', 'U') IS NULL
CREATE TABLE Remisiones (
    Id                     INT IDENTITY(1,1) NOT NULL,
    ResidenteId            INT               NOT NULL,
    MedicoGeneralId        INT               NOT NULL,
    Sintomatologia         NVARCHAR(MAX)     NOT NULL,
    EspecialidadSolicitada VARCHAR(100)      NOT NULL,
    EnfermeroAsignado      VARCHAR(100)      NULL,
    FechaSolicitud         DATETIME          NOT NULL CONSTRAINT DF_Remisiones_Fecha DEFAULT GETDATE(),
    Estado                 TINYINT           NOT NULL CONSTRAINT DF_Remisiones_Est   DEFAULT 0,

    CONSTRAINT PK_Remisiones PRIMARY KEY (Id),
    CONSTRAINT FK_Remisiones_Residentes FOREIGN KEY (ResidenteId)
        REFERENCES Residentes(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Remisiones_Usuarios FOREIGN KEY (MedicoGeneralId)
        REFERENCES Usuarios(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Remisiones_Estado CHECK (Estado IN (0,1,2))
);
GO

/* Indices sobre las FK y sobre Estado: la consola de la fundacion
   filtra por Estado = 0 en cada carga. */
CREATE INDEX IX_Remisiones_Estado      ON Remisiones(Estado);
CREATE INDEX IX_Remisiones_ResidenteId ON Remisiones(ResidenteId);
GO

/* ---------------- CITAS DE LA FUNDACION (RF-03, RF-04) ----------------
   Estado: 0=Programada, 1=EnEvaluacion, 2=EsperandoAnexos,
           3=BitacoraCompletada, 4=CostosCalculados
   Liquidada evita que un cargo ya cobrado vuelva a facturarse. */
IF OBJECT_ID('CitasFundacion', 'U') IS NULL
CREATE TABLE CitasFundacion (
    Id             INT IDENTITY(1,1) NOT NULL,
    RemisionId     INT               NOT NULL,
    EspecialistaId INT               NOT NULL,
    BloqueHorario  DATETIME          NOT NULL,
    Diagnostico    NVARCHAR(MAX)     NULL,
    CostoConsulta  DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Citas_Costo  DEFAULT 0,
    Estado         TINYINT           NOT NULL CONSTRAINT DF_Citas_Estado DEFAULT 0,
    Liquidada      BIT               NOT NULL CONSTRAINT DF_Citas_Liq    DEFAULT 0,

    CONSTRAINT PK_CitasFundacion PRIMARY KEY (Id),
    CONSTRAINT FK_Citas_Remisiones FOREIGN KEY (RemisionId)
        REFERENCES Remisiones(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Citas_Usuarios FOREIGN KEY (EspecialistaId)
        REFERENCES Usuarios(Id) ON DELETE NO ACTION,
    /* Relacion 1 a 0..1: una remision origina como maximo una cita */
    CONSTRAINT UQ_Citas_Remision UNIQUE (RemisionId),
    CONSTRAINT CK_Citas_Estado CHECK (Estado IN (0,1,2,3,4)),
    CONSTRAINT CK_Citas_Costo  CHECK (CostoConsulta >= 0)
);
GO

CREATE INDEX IX_Citas_Especialista ON CitasFundacion(EspecialistaId, BloqueHorario);
GO

/* ---------------- ORDENES DE LABORATORIO (RF-05a) ---------------- */
IF OBJECT_ID('OrdenesLaboratorio', 'U') IS NULL
CREATE TABLE OrdenesLaboratorio (
    Id             INT IDENTITY(1,1) NOT NULL,
    CitaId         INT               NOT NULL,
    TipoExamen     VARCHAR(150)      NOT NULL,
    Resultado      NVARCHAR(MAX)     NULL,
    CostoBruto     DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Ordenes_Costo  DEFAULT 0,
    FechaSolicitud DATETIME          NOT NULL CONSTRAINT DF_Ordenes_FSol   DEFAULT GETDATE(),
    FechaResultado DATETIME          NULL,
    Estado         TINYINT           NOT NULL CONSTRAINT DF_Ordenes_Estado DEFAULT 0,

    CONSTRAINT PK_OrdenesLaboratorio PRIMARY KEY (Id),
    CONSTRAINT FK_Ordenes_Citas FOREIGN KEY (CitaId)
        REFERENCES CitasFundacion(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Ordenes_Estado CHECK (Estado IN (0,1)),
    CONSTRAINT CK_Ordenes_Costo  CHECK (CostoBruto >= 0)
);
GO

CREATE INDEX IX_Ordenes_CitaId ON OrdenesLaboratorio(CitaId);
GO

/* ---------------- DESPACHOS DE FARMACIA (RF-05b) ----------------
   CostoTotal es columna calculada: la mantiene el motor, la
   aplicacion no puede desincronizarla. */
IF OBJECT_ID('DespachosFarmacia', 'U') IS NULL
CREATE TABLE DespachosFarmacia (
    Id               INT IDENTITY(1,1) NOT NULL,
    CitaId           INT               NOT NULL,
    Medicamento      VARCHAR(150)      NOT NULL,
    Cantidad         INT               NOT NULL,
    TiempoAplicacion VARCHAR(100)      NULL,
    CostoUnitario    DECIMAL(10,2)     NOT NULL,
    CostoTotal       AS (Cantidad * CostoUnitario) PERSISTED,
    FechaDespacho    DATETIME          NOT NULL CONSTRAINT DF_Despachos_Fecha DEFAULT GETDATE(),

    CONSTRAINT PK_DespachosFarmacia PRIMARY KEY (Id),
    CONSTRAINT FK_Despachos_Citas FOREIGN KEY (CitaId)
        REFERENCES CitasFundacion(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Despachos_Cantidad CHECK (Cantidad > 0),
    CONSTRAINT CK_Despachos_Costo    CHECK (CostoUnitario >= 0)
);
GO

CREATE INDEX IX_Despachos_CitaId ON DespachosFarmacia(CitaId);
GO

PRINT 'Tablas clinicas listas.';
GO
GO

/* ---------- 04_TablasFinancieras.sql ---------- */
/* ============================================================
   04_TablasFinancieras.sql
   Modulo de caja: facturacion, pagos, donaciones y gastos.
   Incluye la tabla Outbox del microservicio de notificaciones.
   ============================================================ */

/* ---------------- FACTURAS (RF-06) ----------------
   Estado: 0=Emitida, 1=SaldoPendiente, 2=Liquidada */
IF OBJECT_ID('Facturas', 'U') IS NULL
CREATE TABLE Facturas (
    Id                INT IDENTITY(1,1) NOT NULL,
    ResidenteId       INT               NOT NULL,
    ConvenioId        INT               NOT NULL,
    Periodo           DATE              NOT NULL,
    TotalBruto        DECIMAL(10,2)     NOT NULL,
    DescuentoAplicado DECIMAL(10,2)     NOT NULL CONSTRAINT DF_Facturas_Desc  DEFAULT 0,
    TotalNeto         DECIMAL(10,2)     NOT NULL,
    FechaEmision      DATETIME          NOT NULL CONSTRAINT DF_Facturas_Fecha DEFAULT GETDATE(),
    Estado            TINYINT           NOT NULL CONSTRAINT DF_Facturas_Est   DEFAULT 0,

    CONSTRAINT PK_Facturas PRIMARY KEY (Id),
    CONSTRAINT FK_Facturas_Residentes FOREIGN KEY (ResidenteId)
        REFERENCES Residentes(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Facturas_Convenios FOREIGN KEY (ConvenioId)
        REFERENCES ConveniosFundacion(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Facturas_Estado CHECK (Estado IN (0,1,2)),
    CONSTRAINT CK_Facturas_Montos CHECK (TotalBruto >= 0 AND DescuentoAplicado >= 0 AND TotalNeto >= 0),
    /* Si el neto supera al bruto hubo un error de calculo: lo atrapo aqui */
    CONSTRAINT CK_Facturas_Coherencia CHECK (TotalNeto <= TotalBruto)
);
GO

CREATE INDEX IX_Facturas_ResidenteId ON Facturas(ResidenteId);
GO

/* ---------------- PAGOS (RF-07) ---------------- */
IF OBJECT_ID('Pagos', 'U') IS NULL
CREATE TABLE Pagos (
    Id            INT IDENTITY(1,1) NOT NULL,
    FacturaId     INT               NOT NULL,
    MontoRecibido DECIMAL(10,2)     NOT NULL,
    FechaPago     DATETIME          NOT NULL CONSTRAINT DF_Pagos_Fecha DEFAULT GETDATE(),
    MetodoPago    VARCHAR(50)       NOT NULL,
    RegistradoPor INT               NULL,

    CONSTRAINT PK_Pagos PRIMARY KEY (Id),
    CONSTRAINT FK_Pagos_Facturas FOREIGN KEY (FacturaId)
        REFERENCES Facturas(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Pagos_Usuarios FOREIGN KEY (RegistradoPor)
        REFERENCES Usuarios(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Pagos_Monto  CHECK (MontoRecibido > 0),
    CONSTRAINT CK_Pagos_Metodo CHECK (MetodoPago IN ('Efectivo','Transferencia','Cheque','Deposito'))
);
GO

CREATE INDEX IX_Pagos_FacturaId ON Pagos(FacturaId);
GO

/* ---------------- DONACIONES (RF-07) ----------------
   Tipo: 0=Persona, 1=Empresa nacional, 2=Empresa internacional, 3=Gobierno */
IF OBJECT_ID('Donaciones', 'U') IS NULL
CREATE TABLE Donaciones (
    Id            INT IDENTITY(1,1) NOT NULL,
    NombreDonante VARCHAR(150)      NOT NULL,
    Tipo          TINYINT           NOT NULL,
    Monto         DECIMAL(10,2)     NOT NULL,
    Fecha         DATE              NOT NULL CONSTRAINT DF_Donaciones_Fecha DEFAULT GETDATE(),
    Descripcion   VARCHAR(300)      NULL,

    CONSTRAINT PK_Donaciones PRIMARY KEY (Id),
    CONSTRAINT CK_Donaciones_Tipo  CHECK (Tipo IN (0,1,2,3)),
    CONSTRAINT CK_Donaciones_Monto CHECK (Monto > 0)
);
GO

CREATE INDEX IX_Donaciones_Fecha ON Donaciones(Fecha);
GO

/* ---------------- GASTOS OPERATIVOS (RF-07) ---------------- */
IF OBJECT_ID('GastosOperativos', 'U') IS NULL
CREATE TABLE GastosOperativos (
    Id        INT IDENTITY(1,1) NOT NULL,
    Concepto  VARCHAR(150)      NOT NULL,
    Categoria VARCHAR(50)       NOT NULL,
    Monto     DECIMAL(10,2)     NOT NULL,
    Fecha     DATE              NOT NULL CONSTRAINT DF_Gastos_Fecha DEFAULT GETDATE(),

    CONSTRAINT PK_GastosOperativos PRIMARY KEY (Id),
    CONSTRAINT CK_Gastos_Monto     CHECK (Monto > 0),
    CONSTRAINT CK_Gastos_Categoria CHECK (Categoria IN
        ('ServiciosBasicos','Alimentacion','Mantenimiento','PagoFundacion','Personal','Otros'))
);
GO

CREATE INDEX IX_Gastos_Fecha ON GastosOperativos(Fecha);
GO

/* ---------------- OUTBOX (RF-08) ----------------
   El INSERT va dentro de la misma transaccion que la remision.
   O se guardan ambos, o ninguno: asi la notificacion nunca se pierde
   aunque el SMTP este caido. */
IF OBJECT_ID('OutboxMensajes', 'U') IS NULL
CREATE TABLE OutboxMensajes (
    Id             BIGINT IDENTITY(1,1) NOT NULL,
    TipoEvento     VARCHAR(100)         NOT NULL,
    Payload        NVARCHAR(MAX)        NOT NULL,
    Procesado      BIT                  NOT NULL CONSTRAINT DF_Outbox_Proc  DEFAULT 0,
    Intentos       INT                  NOT NULL CONSTRAINT DF_Outbox_Int   DEFAULT 0,
    UltimoError    NVARCHAR(1000)       NULL,
    FechaCreacion  DATETIME             NOT NULL CONSTRAINT DF_Outbox_FCrea DEFAULT GETDATE(),
    FechaProcesado DATETIME             NULL,

    CONSTRAINT PK_OutboxMensajes  PRIMARY KEY (Id),
    CONSTRAINT CK_Outbox_Intentos CHECK (Intentos >= 0)
);
GO

/* Indice filtrado: solo indexo los pendientes, que son los unicos
   que el microservicio consulta. */
CREATE INDEX IX_Outbox_Pendientes ON OutboxMensajes(FechaCreacion) WHERE Procesado = 0;
GO

PRINT 'Tablas financieras y Outbox listas.';
GO
GO

/* ---------- 05_Auditoria_Triggers.sql ---------- */
/* ============================================================
   05_Auditoria_Triggers.sql
   Cumple el RNF-01.

   Lo implemento con triggers y no con un interceptor en la
   aplicacion porque asi tambien quedan registrados los cambios
   hechos desde SSMS o cualquier via externa al sistema.
   ============================================================ */

IF OBJECT_ID('LogsAuditoria', 'U') IS NULL
CREATE TABLE LogsAuditoria (
    Id                BIGINT IDENTITY(1,1) NOT NULL,
    Entidad           VARCHAR(100)         NOT NULL,
    EntidadId         VARCHAR(50)          NOT NULL,
    Accion            VARCHAR(20)          NOT NULL,
    ValoresAnteriores NVARCHAR(MAX)        NULL,
    ValoresNuevos     NVARCHAR(MAX)        NULL,
    UsuarioSql        VARCHAR(128)         NOT NULL CONSTRAINT DF_Logs_Usr   DEFAULT SUSER_SNAME(),
    FechaHora         DATETIME             NOT NULL CONSTRAINT DF_Logs_Fecha DEFAULT GETDATE(),

    CONSTRAINT PK_LogsAuditoria PRIMARY KEY (Id),
    CONSTRAINT CK_Logs_Accion CHECK (Accion IN ('INSERT','UPDATE','DELETE'))
);
GO

CREATE INDEX IX_Logs_Entidad ON LogsAuditoria(Entidad, EntidadId);
CREATE INDEX IX_Logs_Fecha   ON LogsAuditoria(FechaHora);
GO

/* ---------------- CitasFundacion ----------------
   inserted y deleted son tablas virtuales que SQL Server pone
   dentro del trigger: deleted trae el antes, inserted el despues. */
IF OBJECT_ID('TR_CitasFundacion_Auditoria', 'TR') IS NOT NULL
    DROP TRIGGER TR_CitasFundacion_Auditoria;
GO

CREATE TRIGGER TR_CitasFundacion_Auditoria
ON CitasFundacion
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'CitasFundacion', CAST(i.Id AS VARCHAR(50)), 'INSERT', NULL,
           (SELECT i.Id, i.RemisionId, i.EspecialistaId, i.Estado
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM inserted i WHERE NOT EXISTS (SELECT 1 FROM deleted);

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'CitasFundacion', CAST(d.Id AS VARCHAR(50)), 'UPDATE',
           (SELECT d.Diagnostico, d.Estado, d.CostoConsulta FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT i.Diagnostico, i.Estado, i.CostoConsulta FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM deleted d INNER JOIN inserted i ON i.Id = d.Id;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'CitasFundacion', CAST(d.Id AS VARCHAR(50)), 'DELETE',
           (SELECT d.Id, d.RemisionId, d.Diagnostico FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), NULL
    FROM deleted d WHERE NOT EXISTS (SELECT 1 FROM inserted);
END
GO

/* ---------------- Facturas ---------------- */
IF OBJECT_ID('TR_Facturas_Auditoria', 'TR') IS NOT NULL
    DROP TRIGGER TR_Facturas_Auditoria;
GO

CREATE TRIGGER TR_Facturas_Auditoria
ON Facturas
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Facturas', CAST(i.Id AS VARCHAR(50)), 'INSERT', NULL,
           (SELECT i.Id, i.ResidenteId, i.TotalBruto, i.DescuentoAplicado, i.TotalNeto, i.Estado
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM inserted i WHERE NOT EXISTS (SELECT 1 FROM deleted);

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Facturas', CAST(d.Id AS VARCHAR(50)), 'UPDATE',
           (SELECT d.TotalBruto, d.DescuentoAplicado, d.TotalNeto, d.Estado FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT i.TotalBruto, i.DescuentoAplicado, i.TotalNeto, i.Estado FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM deleted d INNER JOIN inserted i ON i.Id = d.Id;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Facturas', CAST(d.Id AS VARCHAR(50)), 'DELETE',
           (SELECT d.Id, d.ResidenteId, d.TotalNeto FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), NULL
    FROM deleted d WHERE NOT EXISTS (SELECT 1 FROM inserted);
END
GO

/* ---------------- Pagos ---------------- */
IF OBJECT_ID('TR_Pagos_Auditoria', 'TR') IS NOT NULL
    DROP TRIGGER TR_Pagos_Auditoria;
GO

CREATE TRIGGER TR_Pagos_Auditoria
ON Pagos
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Pagos', CAST(i.Id AS VARCHAR(50)), 'INSERT', NULL,
           (SELECT i.Id, i.FacturaId, i.MontoRecibido, i.MetodoPago FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM inserted i WHERE NOT EXISTS (SELECT 1 FROM deleted);

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Pagos', CAST(d.Id AS VARCHAR(50)), 'UPDATE',
           (SELECT d.MontoRecibido, d.MetodoPago FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT i.MontoRecibido, i.MetodoPago FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
    FROM deleted d INNER JOIN inserted i ON i.Id = d.Id;

    INSERT INTO LogsAuditoria (Entidad, EntidadId, Accion, ValoresAnteriores, ValoresNuevos)
    SELECT 'Pagos', CAST(d.Id AS VARCHAR(50)), 'DELETE',
           (SELECT d.Id, d.FacturaId, d.MontoRecibido FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), NULL
    FROM deleted d WHERE NOT EXISTS (SELECT 1 FROM inserted);
END
GO

PRINT 'Bitacora y triggers de auditoria listos.';
GO
GO

/* ---------- 06_DatosSemilla.sql ---------- */
/* ============================================================
   06_DatosSemilla.sql
   Datos minimos para que el sistema arranque y pueda mostrarse
   en Swagger sin pantallas vacias.

   Los hashes de contrasena son marcadores. Antes de desplegar
   genero los reales desde el endpoint de registro con BCrypt.
   ============================================================ */

/* ---------------- Usuarios ---------------- */
IF NOT EXISTS (SELECT 1 FROM Usuarios)
INSERT INTO Usuarios (NombreCompleto, Email, PasswordHash, Rol, Especialidad) VALUES
 ('Licda. Monica Herrera', 'admin@asilo.org',             'PENDIENTE_HASH_BCRYPT', 'Administrador',      NULL),
 ('Dr. Luis Arevalo',      'medico.general@asilo.org',    'PENDIENTE_HASH_BCRYPT', 'MedicoGeneral',      NULL),
 ('Lic. Andrea Solis',     'coordinacion@fundacion.org',  'PENDIENTE_HASH_BCRYPT', 'Fundacion',          NULL),
 ('Dra. Silvia Paz',       'silvia.paz@fundacion.org',    'PENDIENTE_HASH_BCRYPT', 'Especialista',       'Neurologia'),
 ('Dr. Mario Coronado',    'mario.coronado@fundacion.org','PENDIENTE_HASH_BCRYPT', 'Especialista',       'Cardiologia'),
 ('Tec. Byron Calderon',   'laboratorio@fundacion.org',   'PENDIENTE_HASH_BCRYPT', 'TecnicoLabFarmacia', NULL);
GO

/* ---------------- Convenio vigente ----------------
   20% sobre consulta, laboratorio y farmacia. No aplica sobre
   la cuota mensual de estancia, que es ingreso del asilo. */
IF NOT EXISTS (SELECT 1 FROM ConveniosFundacion)
INSERT INTO ConveniosFundacion (Descripcion, PorcentajeDescuento, FechaInicio, Vigente) VALUES
 ('Convenio general 2026 - 20% sobre servicios clinicos', 20.00, '2026-01-01', 1);
GO

/* ---------------- Residentes ---------------- */
IF NOT EXISTS (SELECT 1 FROM Residentes)
INSERT INTO Residentes (Nombres, Apellidos, FechaNacimiento, Psicopatologias, MedicamentosCajon, CuotaMensual, FechaIngreso) VALUES
 ('Rosa Elena',  'Mendez',  '1945-04-12', 'Demencia senil leve',   'Donepezilo 10 mg cada 24 h; Losartan 50 mg cada 12 h', 1200.00, '2023-03-12'),
 ('Julio Cesar', 'Ramirez', '1949-08-30', 'Hipertension arterial', 'Enalapril 10 mg cada 12 h',                            1200.00, '2024-07-05'),
 ('Marta Lucia', 'Ortiz',   '1942-11-03', 'Diabetes tipo 2',       'Metformina 850 mg cada 12 h',                          1350.00, '2022-11-19'),
 ('Fernando',    'Aguilar', '1953-02-18', NULL,                    NULL,                                                   1200.00, '2026-02-02');
GO

/* ---------------- Familiares ---------------- */
IF NOT EXISTS (SELECT 1 FROM Familiares)
INSERT INTO Familiares (ResidenteId, Nombre, Email, Telefono) VALUES
 (1, 'Ana Mendez',      'ana.mendez@correo.com',      '5555-4444'),
 (2, 'Pedro Ramirez',   'pedro.ramirez@correo.com',   '5555-5555'),
 (3, 'Claudia Ortiz',   'claudia.ortiz@correo.com',   '5555-6666'),
 (4, 'Rodrigo Aguilar', 'rodrigo.aguilar@correo.com', '5555-7777');
GO

/* ---------------- Remisiones de ejemplo ---------------- */
IF NOT EXISTS (SELECT 1 FROM Remisiones)
INSERT INTO Remisiones (ResidenteId, MedicoGeneralId, Sintomatologia, EspecialidadSolicitada, EnfermeroAsignado, FechaSolicitud, Estado) VALUES
 (1, 2, 'Desorientacion nocturna progresiva durante las ultimas tres semanas.', 'Neurologia',    'Karla Jimenez',  '2026-09-22', 0),
 (2, 2, 'Control de presion arterial no estabilizado pese al tratamiento.',     'Cardiologia',   'Oscar Baldizon', '2026-09-24', 0),
 (4, 2, 'Dolor lumbar persistente tras caida en el pabellon.',                  'Traumatologia', 'Karla Jimenez',  '2026-09-17', 0);
GO

PRINT 'Datos semilla insertados.';
GO
GO

/* ---------- 07_CorregirRemisionesSinCita.sql ---------- */
/* ============================================================
   07_CorregirRemisionesSinCita.sql

   Durante las pruebas quedaron remisiones en estado Programada
   sin cita asociada, porque el PATCH de estado permitia ese paso
   sin validar. Ya corregi el endpoint; este script repara los
   datos que quedaron inconsistentes.

   Ejecutar una sola vez.
   ============================================================ */

/* 1. Revisar que remisiones estan mal antes de tocar nada */
PRINT '--- Remisiones en Programada sin cita asignada ---';
SELECT  rem.Id,
        r.Nombres + ' ' + r.Apellidos AS Residente,
        rem.EspecialidadSolicitada,
        rem.FechaSolicitud
FROM Remisiones rem
INNER JOIN Residentes r ON r.Id = rem.ResidenteId
WHERE rem.Estado = 1
  AND NOT EXISTS (SELECT 1 FROM CitasFundacion c WHERE c.RemisionId = rem.Id);
GO

/* 2. Devolverlas a Pendiente para que vuelvan a la consola de la fundacion */
UPDATE rem
SET rem.Estado = 0
FROM Remisiones rem
WHERE rem.Estado = 1
  AND NOT EXISTS (SELECT 1 FROM CitasFundacion c WHERE c.RemisionId = rem.Id);

PRINT CONCAT('Remisiones corregidas: ', @@ROWCOUNT);
GO

/* 3. Confirmar que ya no queda ninguna inconsistente */
PRINT '--- Verificacion: deberia devolver 0 filas ---';
SELECT rem.Id, rem.Estado
FROM Remisiones rem
WHERE rem.Estado = 1
  AND NOT EXISTS (SELECT 1 FROM CitasFundacion c WHERE c.RemisionId = rem.Id);
GO
GO

/* ---------- 08_RequiereAnexos.sql ---------- */
/* ============================================================
   08_RequiereAnexos.sql

   Separo en dos banderas lo que antes era un solo booleano
   "RequiereAnexos" que ni siquiera se guardaba: se usaba para
   elegir el estado y se descartaba.

   El problema que esto resuelve: sin saber QUE se le pidio a la
   cita, no hay forma de decidir cuando la bitacora esta completa,
   y la cita se quedaba para siempre en Estado = 2 (EsperandoAnexos).

   Tambien separa los dos destinos: antes no se podia mandar a un
   residente solo a laboratorio o solo a farmacia, era o los dos
   o ninguno.
   ============================================================ */

/* ---------------- Columnas nuevas ---------------- */

IF COL_LENGTH('CitasFundacion', 'RequiereLaboratorio') IS NULL
    ALTER TABLE CitasFundacion
        ADD RequiereLaboratorio BIT NOT NULL
            CONSTRAINT DF_Citas_ReqLab DEFAULT 0;
GO

IF COL_LENGTH('CitasFundacion', 'RequiereFarmacia') IS NULL
    ALTER TABLE CitasFundacion
        ADD RequiereFarmacia BIT NOT NULL
            CONSTRAINT DF_Citas_ReqFar DEFAULT 0;
GO

/* ---------------- Recuperacion de las citas ya existentes ----------------

   Las citas que quedaron atrapadas en Estado = 2 no tienen registro
   de que se les pidio, porque la version anterior no lo guardaba.
   Lo deduzco de lo que ya tienen anotado:

     - si tiene ordenes de laboratorio, se le pidio laboratorio
     - si tiene despachos, se le pidio farmacia
     - si no tiene ninguno de los dos, asumo laboratorio, que es lo
       que ofrecia la pantalla anterior

   Es una suposicion y la dejo escrita aqui a proposito: es dato
   reconstruido, no dato original. */

UPDATE c
SET RequiereLaboratorio =
        CASE
            WHEN EXISTS (SELECT 1 FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) THEN 1
            WHEN EXISTS (SELECT 1 FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) THEN 0
            ELSE 1
        END,
    RequiereFarmacia =
        CASE
            WHEN EXISTS (SELECT 1 FROM DespachosFarmacia d WHERE d.CitaId = c.Id) THEN 1
            ELSE 0
        END
FROM CitasFundacion c
WHERE c.Estado = 2;
GO

/* ---------------- Cierre de las que ya estaban completas ----------------

   Una cita cuyos anexos ya se cargaron antes de este cambio nunca
   recibio el UPDATE de estado, porque no existia. La cierro aqui
   con el mismo criterio que usa la aplicacion de ahora en adelante. */

UPDATE CitasFundacion
SET Estado = 3
WHERE Estado = 2
  AND (RequiereLaboratorio = 0
       OR (    EXISTS (SELECT 1 FROM OrdenesLaboratorio o
                        WHERE o.CitaId = CitasFundacion.Id)
           AND NOT EXISTS (SELECT 1 FROM OrdenesLaboratorio o
                            WHERE o.CitaId = CitasFundacion.Id AND o.Estado = 0)))
  AND (RequiereFarmacia = 0
       OR EXISTS (SELECT 1 FROM DespachosFarmacia d
                   WHERE d.CitaId = CitasFundacion.Id));
GO

PRINT 'Banderas de anexos agregadas y citas atrapadas recuperadas.';
GO

/* ---------------- Verificacion ----------------
   Despues de ejecutar, esta consulta no deberia devolver ninguna
   cita cuyos anexos esten todos completos. */

SELECT  c.Id,
        c.Estado,
        c.RequiereLaboratorio,
        c.RequiereFarmacia,
        (SELECT COUNT(*) FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) AS Ordenes,
        (SELECT COUNT(*) FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id AND o.Estado = 0) AS OrdenesPendientes,
        (SELECT COUNT(*) FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) AS Despachos
FROM CitasFundacion c
WHERE c.Estado = 2;
GO
GO

/* ---------- 09_CatalogosYDpi.sql ---------- */
/* ============================================================
   09_CatalogosYDpi.sql

   Tres cambios estructurales que corrigen omisiones del analisis:

   1. Tabla Especialidades. Hasta ahora la especialidad era texto
      libre en dos tablas distintas (Usuarios.Especialidad y
      Remisiones.EspecialidadSolicitada) y la consola de la fundacion
      cruzaba las dos cadenas para ofrecer especialistas. Una tilde de
      diferencia rompia el filtro y nada en la base lo impedia.

   2. Tabla Enfermeros. Remisiones.EnfermeroAsignado era un VARCHAR
      suelto, a diferencia del medico y del especialista, que si son
      llaves foraneas a Usuarios. Dos escrituras distintas del mismo
      nombre eran dos enfermeros para el sistema.

   3. Columna DPI en Residentes y Familiares. Sin ella no hay forma de
      identificar sin ambiguedad a la persona a la que se le factura.

   El script retira las columnas de texto despues de migrar su
   contenido. Prefiero el corte limpio a dejar el dato duplicado en
   dos lugares, que es justo el problema que vengo a resolver.
   ============================================================ */

/* ============================================================
   1. CATALOGO DE ESPECIALIDADES
   ============================================================ */

IF OBJECT_ID('Especialidades', 'U') IS NULL
CREATE TABLE Especialidades (
    Id            INT IDENTITY(1,1) NOT NULL,
    Nombre        VARCHAR(100)      NOT NULL,
    Activa        BIT               NOT NULL CONSTRAINT DF_Especialidades_Activa DEFAULT 1,
    FechaRegistro DATETIME          NOT NULL CONSTRAINT DF_Especialidades_Fecha  DEFAULT GETDATE(),

    CONSTRAINT PK_Especialidades     PRIMARY KEY (Id),
    CONSTRAINT UQ_Especialidades_Nom UNIQUE (Nombre)
);
GO

/* Semilla. Las escribo acentuadas porque es como se muestran en
   pantalla; el emparejamiento de abajo ignora tildes. */
IF NOT EXISTS (SELECT 1 FROM Especialidades)
INSERT INTO Especialidades (Nombre) VALUES
 ('Neurología'), ('Cardiología'), ('Traumatología'),
 ('Oftalmología'), ('Medicina interna'), ('Geriatría');
GO

/* Cualquier especialidad que ya estuviera escrita en los datos y no
   figure en la semilla tambien entra: no quiero perder informacion
   existente por no haberla previsto. */
INSERT INTO Especialidades (Nombre)
SELECT DISTINCT LTRIM(RTRIM(u.Especialidad))
FROM Usuarios u
WHERE u.Especialidad IS NOT NULL
  AND LTRIM(RTRIM(u.Especialidad)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Especialidades e
        WHERE e.Nombre = u.Especialidad COLLATE Modern_Spanish_CI_AI);
GO

INSERT INTO Especialidades (Nombre)
SELECT DISTINCT LTRIM(RTRIM(r.EspecialidadSolicitada))
FROM Remisiones r
WHERE r.EspecialidadSolicitada IS NOT NULL
  AND LTRIM(RTRIM(r.EspecialidadSolicitada)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Especialidades e
        WHERE e.Nombre = r.EspecialidadSolicitada COLLATE Modern_Spanish_CI_AI);
GO

/* ============================================================
   2. CATALOGO DE ENFERMEROS
   ============================================================ */

IF OBJECT_ID('Enfermeros', 'U') IS NULL
CREATE TABLE Enfermeros (
    Id            INT IDENTITY(1,1) NOT NULL,
    Nombre        VARCHAR(150)      NOT NULL,
    Telefono      VARCHAR(30)       NULL,
    Activo        BIT               NOT NULL CONSTRAINT DF_Enfermeros_Activo DEFAULT 1,
    FechaRegistro DATETIME          NOT NULL CONSTRAINT DF_Enfermeros_Fecha  DEFAULT GETDATE(),

    CONSTRAINT PK_Enfermeros     PRIMARY KEY (Id),
    CONSTRAINT UQ_Enfermeros_Nom UNIQUE (Nombre)
);
GO

/* Los enfermeros que ya aparecian escritos en las remisiones. */
INSERT INTO Enfermeros (Nombre)
SELECT DISTINCT LTRIM(RTRIM(r.EnfermeroAsignado))
FROM Remisiones r
WHERE r.EnfermeroAsignado IS NOT NULL
  AND LTRIM(RTRIM(r.EnfermeroAsignado)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Enfermeros e
        WHERE e.Nombre = r.EnfermeroAsignado COLLATE Modern_Spanish_CI_AI);
GO

/* ============================================================
   3. LLAVES FORANEAS EN USUARIOS
   ============================================================ */

IF COL_LENGTH('Usuarios', 'EspecialidadId') IS NULL
    ALTER TABLE Usuarios ADD EspecialidadId INT NULL;
GO

UPDATE u
SET EspecialidadId = e.Id
FROM Usuarios u
INNER JOIN Especialidades e
        ON e.Nombre = u.Especialidad COLLATE Modern_Spanish_CI_AI
WHERE u.Especialidad IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Especialidades')
    ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_Especialidades
        FOREIGN KEY (EspecialidadId) REFERENCES Especialidades(Id);
GO

/* Un especialista sin especialidad no tiene sentido, y una
   especialidad en alguien que no es especialista tampoco. La regla
   vivia solo en la cabeza del programador; aqui queda escrita. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Usuarios_EspecialidadRol')
    ALTER TABLE Usuarios ADD CONSTRAINT CK_Usuarios_EspecialidadRol CHECK (
        (Rol = 'Especialista' AND EspecialidadId IS NOT NULL) OR
        (Rol <> 'Especialista' AND EspecialidadId IS NULL));
GO

/* ============================================================
   4. LLAVES FORANEAS EN REMISIONES
   ============================================================ */

IF COL_LENGTH('Remisiones', 'EspecialidadId') IS NULL
    ALTER TABLE Remisiones ADD EspecialidadId INT NULL;
GO

IF COL_LENGTH('Remisiones', 'EnfermeroId') IS NULL
    ALTER TABLE Remisiones ADD EnfermeroId INT NULL;
GO

UPDATE r
SET EspecialidadId = e.Id
FROM Remisiones r
INNER JOIN Especialidades e
        ON e.Nombre = r.EspecialidadSolicitada COLLATE Modern_Spanish_CI_AI;
GO

UPDATE r
SET EnfermeroId = n.Id
FROM Remisiones r
INNER JOIN Enfermeros n
        ON n.Nombre = r.EnfermeroAsignado COLLATE Modern_Spanish_CI_AI
WHERE r.EnfermeroAsignado IS NOT NULL;
GO

/* Freno de seguridad: si alguna remision quedo sin especialidad, el
   script se detiene antes de borrar la columna de texto. Mejor
   abortar que perder el dato. */
IF EXISTS (SELECT 1 FROM Remisiones WHERE EspecialidadId IS NULL)
BEGIN
    RAISERROR('Hay remisiones sin EspecialidadId. Reviso el emparejamiento antes de continuar.', 16, 1);
    RETURN;
END
GO

ALTER TABLE Remisiones ALTER COLUMN EspecialidadId INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Remisiones_Especialidades')
    ALTER TABLE Remisiones ADD CONSTRAINT FK_Remisiones_Especialidades
        FOREIGN KEY (EspecialidadId) REFERENCES Especialidades(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Remisiones_Enfermeros')
    ALTER TABLE Remisiones ADD CONSTRAINT FK_Remisiones_Enfermeros
        FOREIGN KEY (EnfermeroId) REFERENCES Enfermeros(Id);
GO

CREATE INDEX IX_Remisiones_EspecialidadId ON Remisiones(EspecialidadId);
GO

/* ============================================================
   5. DPI EN RESIDENTES Y FAMILIARES

   CHAR(13) y no VARCHAR: el DPI guatemalteco siempre tiene trece
   digitos, asi que la longitud fija documenta la regla.

   Admite NULL porque los registros que ya existen no lo tienen, y
   rellenarlos con un valor inventado seria peor que dejarlo vacio.
   El indice unico filtrado impide repetirlo sin obligar a tenerlo.
   ============================================================ */

IF COL_LENGTH('Residentes', 'Dpi') IS NULL
    ALTER TABLE Residentes ADD Dpi CHAR(13) NULL;
GO

IF COL_LENGTH('Familiares', 'Dpi') IS NULL
    ALTER TABLE Familiares ADD Dpi CHAR(13) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Residentes_Dpi')
CREATE UNIQUE INDEX UX_Residentes_Dpi ON Residentes(Dpi) WHERE Dpi IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Familiares_Dpi')
CREATE UNIQUE INDEX UX_Familiares_Dpi ON Familiares(Dpi) WHERE Dpi IS NOT NULL;
GO

/* Trece digitos exactos. La validacion tambien esta en los DTO, pero
   la quiero aqui para que ningun INSERT directo la esquive. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Residentes_Dpi')
    ALTER TABLE Residentes ADD CONSTRAINT CK_Residentes_Dpi
        CHECK (Dpi IS NULL OR Dpi LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Familiares_Dpi')
    ALTER TABLE Familiares ADD CONSTRAINT CK_Familiares_Dpi
        CHECK (Dpi IS NULL OR Dpi LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
GO

/* ============================================================
   6. RETIRO DE LAS COLUMNAS DE TEXTO LIBRE

   A partir de aqui el nombre vive en una sola tabla. Dejarlas
   habria mantenido dos fuentes de verdad para el mismo dato.
   ============================================================ */

IF COL_LENGTH('Usuarios', 'Especialidad') IS NOT NULL
    ALTER TABLE Usuarios DROP COLUMN Especialidad;
GO

IF COL_LENGTH('Remisiones', 'EspecialidadSolicitada') IS NOT NULL
    ALTER TABLE Remisiones DROP COLUMN EspecialidadSolicitada;
GO

IF COL_LENGTH('Remisiones', 'EnfermeroAsignado') IS NOT NULL
    ALTER TABLE Remisiones DROP COLUMN EnfermeroAsignado;
GO

/* ============================================================
   7. DPI DE PRUEBA PARA LOS REGISTROS EXISTENTES

   ===== DATOS FICTICIOS. BORRAR ANTES DE PRODUCCION. =====

   Los residentes y familiares que ya estaban cargados no tienen DPI,
   porque la columna no existia cuando se registraron. Les asigno uno
   aqui con el unico proposito de poder probar y mostrar el campo
   funcionando: sin ningun dato, la columna sale vacia en pantalla y
   no se puede verificar ni el formato ni la restriccion de unicidad.

   Como estan construidos: ocho digitos correlativos, un verificador,
   y '1001' al final, que es el codigo de Suchitepequez y Mazatenango.
   Respetan el formato de trece digitos pero NO son documentos reales
   ni pasan el algoritmo de verificacion del RENAP. Son correlativos a
   proposito, para que a simple vista se note que son inventados.

   Solo toca los registros que no tienen DPI: si ya se capturo uno de
   verdad, este bloque no lo pisa.
   ============================================================ */

;WITH ResidentesSinDpi AS (
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY Id) AS Fila
    FROM Residentes
    WHERE Dpi IS NULL
)
UPDATE r
SET Dpi = RIGHT('00000000' + CAST(10000000 + s.Fila AS VARCHAR(8)), 8)
        + CAST(s.Fila % 10 AS CHAR(1))
        + '1001'
FROM Residentes r
INNER JOIN ResidentesSinDpi s ON s.Id = r.Id;
GO

;WITH FamiliaresSinDpi AS (
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY Id) AS Fila
    FROM Familiares
    WHERE Dpi IS NULL
)
UPDATE f
SET Dpi = RIGHT('00000000' + CAST(20000000 + s.Fila AS VARCHAR(8)), 8)
        + CAST(s.Fila % 10 AS CHAR(1))
        + '1001'
FROM Familiares f
INNER JOIN FamiliaresSinDpi s ON s.Id = f.Id;
GO

PRINT 'DPI de prueba asignados. Son ficticios: revisar antes de cualquier despliegue real.';
GO

PRINT 'Catalogos de especialidades y enfermeros creados; DPI agregado.';
GO

/* ============================================================
   VERIFICACION
   ============================================================ */

SELECT 'Especialidades' AS Catalogo, COUNT(*) AS Registros FROM Especialidades
UNION ALL
SELECT 'Enfermeros', COUNT(*) FROM Enfermeros;
GO

/* DPI asignados. Los correlativos delatan que son de prueba. */
SELECT Id, Nombres + ' ' + Apellidos AS Residente, Dpi FROM Residentes ORDER BY Id;
GO

SELECT Id, Nombre AS Familiar, Dpi FROM Familiares ORDER BY Id;
GO

/* Especialidades y si tienen a alguien que las atienda. */
SELECT  e.Id,
        e.Nombre,
        COUNT(u.Id) AS EspecialistasActivos
FROM Especialidades e
LEFT JOIN Usuarios u
       ON u.EspecialidadId = e.Id
      AND u.Rol = 'Especialista'
      AND u.Activo = 1
WHERE e.Activa = 1
GROUP BY e.Id, e.Nombre
ORDER BY e.Nombre;
GO
GO

/* ---------- 10_Facturacion.sql ---------- */
/* ============================================================
   10_Facturacion.sql

   Prepara el modulo financiero (RF-06, RF-07 y RF-10):

   1. Desglose en Facturas. La tabla guardaba TotalBruto,
      DescuentoAplicado y TotalNeto, pero no separaba lo que es
      servicio clinico de lo que es cuota de estancia. Esa
      separacion es justamente la regla del convenio, asi que
      conviene que quede escrita en la factura y no solo en el
      calculo que la produjo.

   2. Enlace de la cita con la factura que la cobro. Sin el, saber
      que citas entraron en una liquidacion exigiria reconstruirlo
      por fechas, y no habria forma de anular una factura sin
      dejar las citas marcadas como cobradas.

   3. El caso de prueba base del plan de SQA: Rosa Elena Mendez,
      septiembre de 2026, con los montos que ese plan espera.
   ============================================================ */

/* ============================================================
   1. DESGLOSE EN LA FACTURA
   ============================================================ */

IF COL_LENGTH('Facturas', 'TotalServicios') IS NULL
    ALTER TABLE Facturas ADD TotalServicios DECIMAL(10,2) NOT NULL
        CONSTRAINT DF_Facturas_Servicios DEFAULT 0;
GO

IF COL_LENGTH('Facturas', 'CuotaMensual') IS NULL
    ALTER TABLE Facturas ADD CuotaMensual DECIMAL(10,2) NOT NULL
        CONSTRAINT DF_Facturas_Cuota DEFAULT 0;
GO

/* La coherencia del desglose vive en la base y no solo en el codigo:
   el bruto tiene que ser exactamente servicios mas cuota. Si algun
   dia un calculo se equivoca, la fila no entra. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Facturas_Desglose')
    ALTER TABLE Facturas ADD CONSTRAINT CK_Facturas_Desglose
        CHECK (TotalBruto = TotalServicios + CuotaMensual);
GO

/* El descuento no puede superar a los servicios: si lo hiciera,
   significaria que se aplico tambien sobre la cuota de estancia,
   que es exactamente el defecto que el plan de pruebas busca. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Facturas_DescuentoSoloServicios')
    ALTER TABLE Facturas ADD CONSTRAINT CK_Facturas_DescuentoSoloServicios
        CHECK (DescuentoAplicado <= TotalServicios);
GO

CREATE INDEX IX_Facturas_Periodo ON Facturas(ResidenteId, Periodo);
GO

/* ============================================================
   2. ENLACE DE LA CITA CON SU FACTURA
   ============================================================ */

IF COL_LENGTH('CitasFundacion', 'FacturaId') IS NULL
    ALTER TABLE CitasFundacion ADD FacturaId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Citas_Facturas')
    ALTER TABLE CitasFundacion ADD CONSTRAINT FK_Citas_Facturas
        FOREIGN KEY (FacturaId) REFERENCES Facturas(Id);
GO

/* Una cita marcada como liquidada tiene que decir en que factura se
   cobro, y una que no lo esta no puede apuntar a ninguna. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Citas_LiquidadaFactura')
    ALTER TABLE CitasFundacion ADD CONSTRAINT CK_Citas_LiquidadaFactura
        CHECK ((Liquidada = 1 AND FacturaId IS NOT NULL) OR
               (Liquidada = 0 AND FacturaId IS NULL));
GO

/* ============================================================
   3. CASO DE PRUEBA BASE DEL PLAN DE SQA

   ===== DATOS DE PRUEBA. BORRAR ANTES DE PRODUCCION. =====

   El plan de pruebas verifica el cobro de Rosa Elena Mendez de
   septiembre de 2026 y espera Q 1,820.00:

       Consulta de neurologia      350.00
       Hemograma completo          185.00
       Donepezilo 10 mg x 30       240.00
       --------------------------------
       Base descontable            775.00
       Descuento 20%              -155.00
       Cuota mensual            +1,200.00
       --------------------------------
       Total neto                1,820.00

   Si el calculo devuelve Q 1,580.00, el descuento se aplico
   tambien sobre la cuota de estancia.

   El bloque es idempotente: se ancla al bloque horario exacto, asi
   que ejecutarlo dos veces no duplica la cita.
   ============================================================ */

DECLARE @ResidenteId    INT = (SELECT Id FROM Residentes WHERE Nombres = 'Rosa Elena' AND Apellidos = 'Mendez');
DECLARE @MedicoId       INT = (SELECT TOP 1 Id FROM Usuarios WHERE Rol = 'MedicoGeneral' AND Activo = 1 ORDER BY Id);
DECLARE @EspecialidadId INT = (SELECT Id FROM Especialidades WHERE Nombre = 'Neurología' COLLATE Modern_Spanish_CI_AI);
DECLARE @EspecialistaId INT = (SELECT TOP 1 u.Id FROM Usuarios u
                               WHERE u.Rol = 'Especialista' AND u.Activo = 1
                                 AND u.EspecialidadId = @EspecialidadId ORDER BY u.Id);
DECLARE @Bloque DATETIME = '2026-09-20T10:00:00';

IF @ResidenteId IS NULL OR @MedicoId IS NULL OR @EspecialidadId IS NULL OR @EspecialistaId IS NULL
BEGIN
    RAISERROR('Faltan datos base: revisar que existan Rosa Elena, un medico general y un especialista de Neurologia.', 16, 1);
    RETURN;
END

/* La cuota mensual del residente es parte del caso: la fijo para que
   el total no dependa de lo que tenga cargado. */
UPDATE Residentes SET CuotaMensual = 1200.00 WHERE Id = @ResidenteId;

IF NOT EXISTS (SELECT 1 FROM CitasFundacion WHERE BloqueHorario = @Bloque)
BEGIN
    DECLARE @RemisionId INT, @CitaId INT;

    INSERT INTO Remisiones (ResidenteId, MedicoGeneralId, Sintomatologia,
                            EspecialidadId, FechaSolicitud, Estado)
    VALUES (@ResidenteId, @MedicoId,
            'Desorientacion nocturna progresiva. Caso base del plan de pruebas.',
            @EspecialidadId, '2026-09-15T08:00:00', 1);
    SET @RemisionId = SCOPE_IDENTITY();

    INSERT INTO CitasFundacion (RemisionId, EspecialistaId, BloqueHorario, Diagnostico,
                                CostoConsulta, Estado, Liquidada,
                                RequiereLaboratorio, RequiereFarmacia)
    VALUES (@RemisionId, @EspecialistaId, @Bloque,
            'Deterioro cognitivo leve. Se solicita hemograma y se mantiene tratamiento.',
            350.00, 3, 0, 1, 1);
    SET @CitaId = SCOPE_IDENTITY();

    INSERT INTO OrdenesLaboratorio (CitaId, TipoExamen, Resultado, CostoBruto,
                                    FechaSolicitud, FechaResultado, Estado)
    VALUES (@CitaId, 'Hemograma completo', 'Valores dentro de rango.', 185.00,
            '2026-09-20T11:00:00', '2026-09-21T09:00:00', 1);

    /* CostoTotal es columna calculada: 30 x 8.00 = 240.00 */
    INSERT INTO DespachosFarmacia (CitaId, Medicamento, Cantidad, TiempoAplicacion,
                                   CostoUnitario, FechaDespacho)
    VALUES (@CitaId, 'Donepezilo 10 mg', 30, '1 tableta cada 24 horas',
            8.00, '2026-09-21T10:00:00');

    PRINT 'Caso base creado. Cita de septiembre 2026 para Rosa Elena Mendez.';
END
ELSE
    PRINT 'El caso base ya existia. No se duplico.';
GO

PRINT 'Modulo de facturacion preparado.';
GO

/* ============================================================
   VERIFICACION

   Esta consulta reproduce a mano el calculo que hara la
   aplicacion. Tiene que devolver 775.00, 155.00, 1975.00 y
   1820.00. Si TotalNeto sale 1580.00, el descuento se aplico
   sobre la cuota.
   ============================================================ */

SELECT  r.Nombres + ' ' + r.Apellidos                  AS Residente,
        SUM(c.CostoConsulta)                           AS Consultas,
        SUM(ISNULL(lab.Total, 0))                      AS Laboratorio,
        SUM(ISNULL(far.Total, 0))                      AS Farmacia,
        SUM(c.CostoConsulta + ISNULL(lab.Total,0) + ISNULL(far.Total,0))          AS BaseDescontable,
        ROUND(SUM(c.CostoConsulta + ISNULL(lab.Total,0) + ISNULL(far.Total,0))
              * conv.PorcentajeDescuento / 100, 2)                               AS Descuento,
        r.CuotaMensual,
        SUM(c.CostoConsulta + ISNULL(lab.Total,0) + ISNULL(far.Total,0)) + r.CuotaMensual AS TotalBruto,
        SUM(c.CostoConsulta + ISNULL(lab.Total,0) + ISNULL(far.Total,0))
          - ROUND(SUM(c.CostoConsulta + ISNULL(lab.Total,0) + ISNULL(far.Total,0))
                  * conv.PorcentajeDescuento / 100, 2)
          + r.CuotaMensual                                                       AS TotalNeto
FROM CitasFundacion c
INNER JOIN Remisiones rem ON rem.Id = c.RemisionId
INNER JOIN Residentes r   ON r.Id   = rem.ResidenteId
CROSS JOIN (SELECT TOP 1 PorcentajeDescuento FROM ConveniosFundacion WHERE Vigente = 1) conv
OUTER APPLY (SELECT SUM(o.CostoBruto) AS Total FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) lab
OUTER APPLY (SELECT SUM(d.CostoTotal) AS Total FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) far
WHERE r.Nombres = 'Rosa Elena'
  AND YEAR(c.BloqueHorario) = 2026 AND MONTH(c.BloqueHorario) = 9
  AND c.Estado >= 3
  AND c.Liquidada = 0
GROUP BY r.Nombres, r.Apellidos, r.CuotaMensual, conv.PorcentajeDescuento;
GO
GO

/* ---------- 11_CorregirCuotaMensual.sql ---------- */
/* ============================================================
   11_CorregirCuotaMensual.sql

   Repara los residentes que quedaron con cuota mensual en cero.

   Que paso. La columna CuotaMensual existe desde el script 03 con
   DEFAULT 0, y el script de semilla le puso valor a los cuatro
   residentes iniciales. Pero la aplicacion nunca la capturaba: no
   estaba en el DTO, ni en el INSERT, ni en el formulario. Todo
   residente registrado desde la pantalla quedo en cero.

   Por que no se noto antes. El DEFAULT 0 hace que el INSERT no
   falle, asi que no habia ningun error visible. El problema solo
   aparece al facturar: esos residentes salen con el cobro de los
   servicios y sin la estancia. Un valor por omision silencioso es
   peor que una columna obligatoria, porque convierte un error de
   programacion en un dato incorrecto que nadie ve.

   Esto queda corregido en el codigo (la cuota es obligatoria en el
   DTO y viaja en el INSERT y en el UPDATE). Este script arregla las
   filas que ya se guardaron mal.
   ============================================================ */

/* Primero muestro a quienes afecta, antes de tocar nada. */
SELECT  Id,
        Nombres + ' ' + Apellidos AS Residente,
        FechaIngreso,
        CuotaMensual
FROM Residentes
WHERE CuotaMensual = 0 AND Activo = 1
ORDER BY Id;
GO

/* ===== REVISAR ANTES DE EJECUTAR =====

   Q 1,200.00 es la cuota que el asilo cobra de forma ordinaria, y es
   la que usan los residentes de la semilla. La aplico como valor de
   recuperacion, pero es una suposicion: si alguno de estos residentes
   tiene convenio distinto, hay que corregirlo a mano despues desde la
   pantalla de residentes. Lo dejo escrito para que quede claro que es
   dato reconstruido y no dato original. */

UPDATE Residentes
SET CuotaMensual = 1200.00
WHERE CuotaMensual = 0 AND Activo = 1;
GO

PRINT 'Cuotas mensuales en cero corregidas a Q1,200.00. Revisar caso por caso si alguno difiere.';
GO

/* ============================================================
   VERIFICACION
   No deberia quedar ningun residente activo con cuota en cero.
   ============================================================ */

SELECT  Id,
        Nombres + ' ' + Apellidos AS Residente,
        CuotaMensual,
        CASE WHEN CuotaMensual = 0 THEN 'REVISAR' ELSE 'Correcto' END AS Estado
FROM Residentes
WHERE Activo = 1
ORDER BY CuotaMensual, Id;
GO
GO

/* ---------- 12_FacturaComplementaria.sql ---------- */
/* ============================================================
   12_FacturaComplementaria.sql

   Habilita mas de una factura por residente y periodo.

   EL PROBLEMA. El modulo financiero permitia una sola factura por
   residente y mes. La idea era evitar cobrar la cuota de estancia dos
   veces, y para eso servia. Pero el cierre del mes no detiene la
   atencion medica: una cita que se agenda, se atiende y se cierra
   despues de haber facturado cae en un mes que ya no admite otra
   factura.

   Esa cita queda con Estado = 3 y Liquidada = 0 para siempre. Aparece
   en el desglose del periodo, suma al total que muestra la pantalla, y
   no hay ninguna operacion que la pueda cobrar. El dinero no se pierde
   por un error de calculo sino porque no existe el documento que lo
   cobre.

   Es la segunda vez que aparece el mismo patron que CF-01: un estado
   que el diagrama declara alcanzable y que ningun metodo ejecuta.

   LA SOLUCION. Varias facturas por periodo, pero la cuota de estancia
   se cobra en una sola de ellas. La primera factura del mes lleva los
   servicios mas la cuota; las complementarias llevan solo los
   servicios de las citas que se cerraron despues.

   POR QUE UN INDICE Y NO UNA VALIDACION EN EL CODIGO. La regla que de
   verdad importa no es "una factura por mes" sino "una cuota de
   estancia por mes". Escrita asi, se puede expresar como un indice
   unico filtrado, y entonces la base la garantiza aunque dos
   emisiones ocurran al mismo tiempo. Una comprobacion en C# que lee y
   despues inserta no cubre ese caso: entre la lectura y la insercion
   cabe otra peticion.
   ============================================================ */

/* ============================================================
   1. DIAGNOSTICO: QUE CARGOS QUEDARON EN EL AIRE

   Citas cerradas y sin liquidar que caen en un periodo que ya tiene
   factura. Son las que hoy no se pueden cobrar por ninguna via.
   Las listo antes de cambiar nada para saber de cuanto se habla.
   ============================================================ */

SELECT  r.Id                                   AS ResidenteId,
        r.Nombres + ' ' + r.Apellidos          AS Residente,
        FORMAT(c.BloqueHorario, 'MM/yyyy')     AS Periodo,
        c.Id                                   AS CitaId,
        c.BloqueHorario,
        c.CostoConsulta
          + ISNULL(lab.Total, 0)
          + ISNULL(far.Total, 0)               AS CargoSinCobrar,
        f.Id                                   AS FacturaDelPeriodo
FROM CitasFundacion c
INNER JOIN Remisiones rem ON rem.Id = c.RemisionId
INNER JOIN Residentes r   ON r.Id   = rem.ResidenteId
INNER JOIN Facturas   f   ON f.ResidenteId = r.Id
                         AND YEAR(f.Periodo)  = YEAR(c.BloqueHorario)
                         AND MONTH(f.Periodo) = MONTH(c.BloqueHorario)
OUTER APPLY (SELECT SUM(o.CostoBruto) AS Total
             FROM OrdenesLaboratorio o WHERE o.CitaId = c.Id) lab
OUTER APPLY (SELECT SUM(d.CostoTotal) AS Total
             FROM DespachosFarmacia  d WHERE d.CitaId = c.Id) far
WHERE c.Estado   >= 3
  AND c.Liquidada = 0
ORDER BY r.Id, c.BloqueHorario;
GO

/* ============================================================
   2. UNA SOLA CUOTA DE ESTANCIA POR PERIODO

   El indice es unico sobre (ResidenteId, Periodo) pero solo mira las
   filas donde CuotaMensual > 0. Las complementarias llevan cuota en
   cero, asi que caen fuera del filtro y pueden ser varias.

   Dicho de otra forma: el sistema admite cuantas facturas haga falta
   en un mes, y exactamente una de ellas cobra la estancia.
   ============================================================ */

/* Freno de seguridad. Si ya existieran dos facturas del mismo periodo
   con cuota, el indice no se podria crear y conviene saberlo antes de
   que falle la sentencia. No deberia ocurrir: hasta ahora el codigo
   solo permitia una factura por periodo. */
IF EXISTS (
    SELECT 1 FROM Facturas
    WHERE CuotaMensual > 0
    GROUP BY ResidenteId, Periodo
    HAVING COUNT(*) > 1)
BEGIN
    RAISERROR('Hay periodos con mas de una factura cobrando cuota de estancia. Reviso esos casos antes de crear el indice.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Facturas_CuotaUnicaPorPeriodo')
CREATE UNIQUE INDEX UX_Facturas_CuotaUnicaPorPeriodo
    ON Facturas(ResidenteId, Periodo)
    WHERE CuotaMensual > 0;
GO

PRINT 'Facturas complementarias habilitadas. La cuota de estancia sigue siendo una por periodo.';
GO

/* ============================================================
   VERIFICACION

   Despues de correr esto y emitir una complementaria desde la
   pantalla, esta consulta tiene que mostrar dos facturas del mismo
   periodo con una sola cuota entre las dos.
   ============================================================ */

SELECT  r.Nombres + ' ' + r.Apellidos      AS Residente,
        FORMAT(f.Periodo, 'MM/yyyy')       AS Periodo,
        f.Id                               AS FacturaId,
        f.TotalServicios,
        f.DescuentoAplicado,
        f.CuotaMensual,
        f.TotalNeto,
        CASE WHEN f.CuotaMensual > 0
             THEN 'Factura del periodo'
             ELSE 'Complementaria' END     AS Tipo
FROM Facturas f
INNER JOIN Residentes r ON r.Id = f.ResidenteId
ORDER BY r.Id, f.Periodo, f.Id;
GO

/* No debe quedar ninguna cita cerrada y sin liquidar en un periodo ya
   facturado: si queda alguna, falto emitir su complementaria. */
SELECT  COUNT(*) AS CargosEnElAire
FROM CitasFundacion c
INNER JOIN Remisiones rem ON rem.Id = c.RemisionId
INNER JOIN Facturas   f   ON f.ResidenteId = rem.ResidenteId
                         AND YEAR(f.Periodo)  = YEAR(c.BloqueHorario)
                         AND MONTH(f.Periodo) = MONTH(c.BloqueHorario)
WHERE c.Estado >= 3 AND c.Liquidada = 0;
GO
GO

/* ---------- 13_RecetaYDespacho.sql ---------- */
/* ============================================================
   13_RecetaYDespacho.sql

   Separa la receta del despacho en farmacia.

   EL PROBLEMA. La pantalla de farmacia le pedia al tecnico el
   medicamento, la cantidad y la dosis. Pero el tecnico no sabe que se
   le receto al residente: eso lo decide el especialista en la consulta.
   Tal como estaba, quien despacha estaba inventando la indicacion
   medica.

   Lo mismo pasaba en laboratorio, donde el tecnico escribia el tipo de
   examen. Ahi la base ya estaba bien: OrdenesLaboratorio tiene Estado,
   FechaSolicitud y FechaResultado separadas, de modo que uno solicita y
   otro ejecuta. Bastaba mover el formulario de pantalla.

   Farmacia no tenia esa division. DespachosFarmacia guardaba el
   medicamento, la cantidad, la dosis y el costo en una sola fila
   escrita de una sola vez. Este script le da la misma forma que ya
   tenia laboratorio.

   COMO QUEDA.
     Estado = 0  Recetado   el especialista indico el medicamento,
                            la cantidad y cada cuanto se aplica.
                            Sin costo todavia.
     Estado = 1  Despachado farmacia lo entrego y fijo el costo
                            unitario. Es el momento en que el cargo
                            existe para la factura.

   CostoTotal sigue siendo columna calculada. Mientras la receta no se
   despacha vale cero, que es lo correcto: lo recetado no se cobra, lo
   entregado si.
   ============================================================ */

/* ============================================================
   1. ESTADO DEL DESPACHO
   ============================================================ */

IF COL_LENGTH('DespachosFarmacia', 'Estado') IS NULL
    ALTER TABLE DespachosFarmacia ADD Estado TINYINT NOT NULL
        CONSTRAINT DF_Despachos_Estado DEFAULT 0;
GO

/* Todo lo que ya estaba guardado se registro con su costo y se entrego
   en el mismo acto, asi que es despacho cumplido. */
UPDATE DespachosFarmacia SET Estado = 1 WHERE Estado = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Despachos_Estado')
    ALTER TABLE DespachosFarmacia ADD CONSTRAINT CK_Despachos_Estado
        CHECK (Estado IN (0,1));
GO

/* ============================================================
   2. DOS FECHAS, UNA POR MOMENTO
   ============================================================ */

IF COL_LENGTH('DespachosFarmacia', 'FechaReceta') IS NULL
    ALTER TABLE DespachosFarmacia ADD FechaReceta DATETIME NULL;
GO

/* Para lo ya existente no se cuando se receto, solo cuando se
   despacho. Uso esa misma fecha: es una reconstruccion, no el dato
   original, y prefiero dejarlo escrito a inventar otra cosa. */
UPDATE DespachosFarmacia
SET FechaReceta = FechaDespacho
WHERE FechaReceta IS NULL;
GO

ALTER TABLE DespachosFarmacia ALTER COLUMN FechaReceta DATETIME NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Despachos_FReceta')
    ALTER TABLE DespachosFarmacia ADD CONSTRAINT DF_Despachos_FReceta
        DEFAULT GETDATE() FOR FechaReceta;
GO

/* FechaDespacho pasa a admitir NULL: una receta todavia no se entrego.
   Hay que quitarle antes el DEFAULT GETDATE(), porque si no toda receta
   nacería con fecha de entrega y la restriccion de abajo la rechazaria. */
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Despachos_Fecha')
    ALTER TABLE DespachosFarmacia DROP CONSTRAINT DF_Despachos_Fecha;
GO

ALTER TABLE DespachosFarmacia ALTER COLUMN FechaDespacho DATETIME NULL;
GO

/* El estado y la fecha no pueden contradecirse: lo despachado tiene
   fecha de entrega y lo recetado no. Es la misma idea que
   CK_Citas_LiquidadaFactura del script 10. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Despachos_EstadoFecha')
    ALTER TABLE DespachosFarmacia ADD CONSTRAINT CK_Despachos_EstadoFecha
        CHECK ((Estado = 1 AND FechaDespacho IS NOT NULL) OR
               (Estado = 0 AND FechaDespacho IS NULL));
GO

/* ============================================================
   3. COSTO AL MOMENTO DE ENTREGAR

   La receta se guarda sin costo. El precio lo pone farmacia cuando
   entrega, igual que el tecnico de laboratorio pone el costo del
   examen al cargar el resultado.
   ============================================================ */

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Despachos_CostoUni')
    ALTER TABLE DespachosFarmacia ADD CONSTRAINT DF_Despachos_CostoUni
        DEFAULT 0 FOR CostoUnitario;
GO

/* Lo entregado tiene que tener precio. Lo recetado todavia no.
   Sin esto, un despacho podria cerrarse en cero y la cita entraria
   a la factura cobrando de menos sin que nada lo advirtiera: el mismo
   defecto silencioso que tuvo la cuota mensual (CF-12). */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Despachos_CostoEntregado')
    ALTER TABLE DespachosFarmacia ADD CONSTRAINT CK_Despachos_CostoEntregado
        CHECK (Estado = 0 OR CostoUnitario > 0);
GO

CREATE INDEX IX_Despachos_Estado ON DespachosFarmacia(CitaId, Estado);
GO

PRINT 'Receta y despacho separados. La receta la escribe el especialista; el costo lo pone farmacia al entregar.';
GO

/* ============================================================
   VERIFICACION

   Las filas que ya existian tienen que haber quedado todas como
   despachadas, con su fecha y su costo.
   ============================================================ */

SELECT  CASE Estado WHEN 0 THEN 'Recetado' ELSE 'Despachado' END AS Situacion,
        COUNT(*)                                                 AS Filas,
        SUM(CASE WHEN FechaDespacho IS NULL THEN 1 ELSE 0 END)   AS SinFechaEntrega,
        SUM(CASE WHEN CostoUnitario = 0 THEN 1 ELSE 0 END)       AS SinCosto
FROM DespachosFarmacia
GROUP BY Estado;
GO

/* Ninguna cita deberia estar cerrada teniendo una receta sin entregar.
   Si alguna aparece, se cerro con la regla vieja y hay que revisarla
   a mano: su medicamento no se cobro. */
SELECT  c.Id                            AS CitaId,
        r.Nombres + ' ' + r.Apellidos   AS Residente,
        c.Estado,
        COUNT(d.Id)                     AS RecetasSinEntregar
FROM CitasFundacion c
INNER JOIN Remisiones     rem ON rem.Id = c.RemisionId
INNER JOIN Residentes       r ON r.Id   = rem.ResidenteId
INNER JOIN DespachosFarmacia d ON d.CitaId = c.Id AND d.Estado = 0
WHERE c.Estado >= 3
GROUP BY c.Id, r.Nombres, r.Apellidos, c.Estado;
GO
GO
