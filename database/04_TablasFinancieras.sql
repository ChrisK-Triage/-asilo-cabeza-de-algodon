/* ============================================================
   04_TablasFinancieras.sql
   Modulo de caja: facturacion, pagos, donaciones y gastos.
   Incluye la tabla Outbox del microservicio de notificaciones.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

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
