/* ============================================================
   00_VerificarInstalacion.sql
   No crea nada. Lo ejecuto al final para comprobar que todo
   quedo bien y para sacar las capturas de la entrega.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

PRINT '--- 1. Tablas creadas ---';
SELECT TABLE_NAME AS Tabla
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;
GO

PRINT '--- 2. Llaves foraneas y su destino ---';
SELECT
    fk.name                                                      AS LlaveForanea,
    OBJECT_NAME(fk.parent_object_id)                             AS TablaOrigen,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id)         AS ColumnaOrigen,
    OBJECT_NAME(fk.referenced_object_id)                         AS TablaReferenciada,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ColumnaReferenciada
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY TablaOrigen, LlaveForanea;
GO

PRINT '--- 3. Restricciones CHECK ---';
SELECT OBJECT_NAME(parent_object_id) AS Tabla, name AS Restriccion, definition AS Regla
FROM sys.check_constraints
ORDER BY Tabla;
GO

PRINT '--- 4. Triggers de auditoria ---';
SELECT name AS NombreTrigger, OBJECT_NAME(parent_id) AS Tabla, is_disabled AS Deshabilitado
FROM sys.triggers WHERE parent_id > 0;
GO

PRINT '--- 5. Conteo de registros semilla ---';
SELECT 'Usuarios' AS Tabla, COUNT(*) AS Registros FROM Usuarios
UNION ALL SELECT 'Residentes', COUNT(*) FROM Residentes
UNION ALL SELECT 'Familiares', COUNT(*) FROM Familiares
UNION ALL SELECT 'Remisiones', COUNT(*) FROM Remisiones;
GO

PRINT '--- 6. Consulta que cruza tres tablas (demuestra las relaciones) ---';
SELECT
    r.Nombres + ' ' + r.Apellidos AS Residente,
    rem.EspecialidadSolicitada    AS Especialidad,
    rem.FechaSolicitud,
    CASE rem.Estado WHEN 0 THEN 'Pendiente' WHEN 1 THEN 'Programada' ELSE 'Cancelada' END AS Estado,
    rem.EnfermeroAsignado,
    u.NombreCompleto              AS MedicoRemitente
FROM Remisiones rem
INNER JOIN Residentes r ON r.Id = rem.ResidenteId
INNER JOIN Usuarios   u ON u.Id = rem.MedicoGeneralId
ORDER BY rem.FechaSolicitud;
GO
