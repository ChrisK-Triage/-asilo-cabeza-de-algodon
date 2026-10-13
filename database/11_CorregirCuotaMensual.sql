/* ============================================================
   11_CorregirCuotaMensual.sql

   Repara los residentes que quedaron con cuota mensual en cero.

   Que paso. La columna CuotaMensual existe desde el script 03 con
   DEFAULT 0, y el script de semilla le puso valor a los cuatro
   residentes iniciales. Pero la aplicacion nunca la capturaba: no
   estaba en el DTO, ni en el INSERT, ni en el formulario. Todo
   residente registrado desde la pantalla quedo en cero.

   Por que no se noto antes. El DEFAULT 0 hace que el INSERT no
   falle, asi que no habia ningun error visible. El problema solo
   aparece al facturar: esos residentes salen con el cobro de los
   servicios y sin la estancia. Un valor por omision silencioso es
   peor que una columna obligatoria, porque convierte un error de
   programacion en un dato incorrecto que nadie ve.

   Esto queda corregido en el codigo (la cuota es obligatoria en el
   DTO y viaja en el INSERT y en el UPDATE). Este script arregla las
   filas que ya se guardaron mal.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

/* Primero muestro a quienes afecta, antes de tocar nada. */
SELECT  Id,
        Nombres + ' ' + Apellidos AS Residente,
        FechaIngreso,
        CuotaMensual
FROM Residentes
WHERE CuotaMensual = 0 AND Activo = 1
ORDER BY Id;
GO

/* ===== REVISAR ANTES DE EJECUTAR =====

   Q 1,200.00 es la cuota que el asilo cobra de forma ordinaria, y es
   la que usan los residentes de la semilla. La aplico como valor de
   recuperacion, pero es una suposicion: si alguno de estos residentes
   tiene convenio distinto, hay que corregirlo a mano despues desde la
   pantalla de residentes. Lo dejo escrito para que quede claro que es
   dato reconstruido y no dato original. */

UPDATE Residentes
SET CuotaMensual = 1200.00
WHERE CuotaMensual = 0 AND Activo = 1;
GO

PRINT 'Cuotas mensuales en cero corregidas a Q1,200.00. Revisar caso por caso si alguno difiere.';
GO

/* ============================================================
   VERIFICACION
   No deberia quedar ningun residente activo con cuota en cero.
   ============================================================ */

SELECT  Id,
        Nombres + ' ' + Apellidos AS Residente,
        CuotaMensual,
        CASE WHEN CuotaMensual = 0 THEN 'REVISAR' ELSE 'Correcto' END AS Estado
FROM Residentes
WHERE Activo = 1
ORDER BY CuotaMensual, Id;
GO
