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

USE AsiloCabezaAlgodonDB;
GO

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
