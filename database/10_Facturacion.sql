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

USE AsiloCabezaAlgodonDB;
GO

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
