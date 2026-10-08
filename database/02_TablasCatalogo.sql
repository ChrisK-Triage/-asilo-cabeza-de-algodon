/* ============================================================
   02_TablasCatalogo.sql
   Tablas que no dependen de ninguna otra. Van primero porque
   Remisiones y Facturas las referencian con llaves foraneas.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

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
