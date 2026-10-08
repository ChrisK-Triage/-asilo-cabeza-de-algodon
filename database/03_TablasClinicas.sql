/* ============================================================
   03_TablasClinicas.sql
   Expediente del residente y flujo remision -> cita -> anexos.
   Requiere 02_TablasCatalogo.sql ejecutado antes.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

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
