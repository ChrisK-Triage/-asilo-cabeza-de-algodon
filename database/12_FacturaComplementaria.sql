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

USE AsiloCabezaAlgodonDB;
GO

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
