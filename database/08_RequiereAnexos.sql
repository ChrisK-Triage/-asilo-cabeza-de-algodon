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

USE AsiloCabezaAlgodonDB;
GO

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
