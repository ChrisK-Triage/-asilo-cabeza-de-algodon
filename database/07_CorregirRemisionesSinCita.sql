/* ============================================================
   07_CorregirRemisionesSinCita.sql

   Durante las pruebas quedaron remisiones en estado Programada
   sin cita asociada, porque el PATCH de estado permitia ese paso
   sin validar. Ya corregi el endpoint; este script repara los
   datos que quedaron inconsistentes.

   Ejecutar una sola vez.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

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
