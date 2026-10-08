/* ============================================================
   05_Auditoria_Triggers.sql
   Cumple el RNF-01.

   Lo implemento con triggers y no con un interceptor en la
   aplicacion porque asi tambien quedan registrados los cambios
   hechos desde SSMS o cualquier via externa al sistema.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

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
