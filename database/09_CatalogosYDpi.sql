/* ============================================================
   09_CatalogosYDpi.sql

   Tres cambios estructurales que corrigen omisiones del analisis:

   1. Tabla Especialidades. Hasta ahora la especialidad era texto
      libre en dos tablas distintas (Usuarios.Especialidad y
      Remisiones.EspecialidadSolicitada) y la consola de la fundacion
      cruzaba las dos cadenas para ofrecer especialistas. Una tilde de
      diferencia rompia el filtro y nada en la base lo impedia.

   2. Tabla Enfermeros. Remisiones.EnfermeroAsignado era un VARCHAR
      suelto, a diferencia del medico y del especialista, que si son
      llaves foraneas a Usuarios. Dos escrituras distintas del mismo
      nombre eran dos enfermeros para el sistema.

   3. Columna DPI en Residentes y Familiares. Sin ella no hay forma de
      identificar sin ambiguedad a la persona a la que se le factura.

   El script retira las columnas de texto despues de migrar su
   contenido. Prefiero el corte limpio a dejar el dato duplicado en
   dos lugares, que es justo el problema que vengo a resolver.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

/* ============================================================
   1. CATALOGO DE ESPECIALIDADES
   ============================================================ */

IF OBJECT_ID('Especialidades', 'U') IS NULL
CREATE TABLE Especialidades (
    Id            INT IDENTITY(1,1) NOT NULL,
    Nombre        VARCHAR(100)      NOT NULL,
    Activa        BIT               NOT NULL CONSTRAINT DF_Especialidades_Activa DEFAULT 1,
    FechaRegistro DATETIME          NOT NULL CONSTRAINT DF_Especialidades_Fecha  DEFAULT GETDATE(),

    CONSTRAINT PK_Especialidades     PRIMARY KEY (Id),
    CONSTRAINT UQ_Especialidades_Nom UNIQUE (Nombre)
);
GO

/* Semilla. Las escribo acentuadas porque es como se muestran en
   pantalla; el emparejamiento de abajo ignora tildes. */
IF NOT EXISTS (SELECT 1 FROM Especialidades)
INSERT INTO Especialidades (Nombre) VALUES
 ('Neurología'), ('Cardiología'), ('Traumatología'),
 ('Oftalmología'), ('Medicina interna'), ('Geriatría');
GO

/* Cualquier especialidad que ya estuviera escrita en los datos y no
   figure en la semilla tambien entra: no quiero perder informacion
   existente por no haberla previsto. */
INSERT INTO Especialidades (Nombre)
SELECT DISTINCT LTRIM(RTRIM(u.Especialidad))
FROM Usuarios u
WHERE u.Especialidad IS NOT NULL
  AND LTRIM(RTRIM(u.Especialidad)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Especialidades e
        WHERE e.Nombre = u.Especialidad COLLATE Modern_Spanish_CI_AI);
GO

INSERT INTO Especialidades (Nombre)
SELECT DISTINCT LTRIM(RTRIM(r.EspecialidadSolicitada))
FROM Remisiones r
WHERE r.EspecialidadSolicitada IS NOT NULL
  AND LTRIM(RTRIM(r.EspecialidadSolicitada)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Especialidades e
        WHERE e.Nombre = r.EspecialidadSolicitada COLLATE Modern_Spanish_CI_AI);
GO

/* ============================================================
   2. CATALOGO DE ENFERMEROS
   ============================================================ */

IF OBJECT_ID('Enfermeros', 'U') IS NULL
CREATE TABLE Enfermeros (
    Id            INT IDENTITY(1,1) NOT NULL,
    Nombre        VARCHAR(150)      NOT NULL,
    Telefono      VARCHAR(30)       NULL,
    Activo        BIT               NOT NULL CONSTRAINT DF_Enfermeros_Activo DEFAULT 1,
    FechaRegistro DATETIME          NOT NULL CONSTRAINT DF_Enfermeros_Fecha  DEFAULT GETDATE(),

    CONSTRAINT PK_Enfermeros     PRIMARY KEY (Id),
    CONSTRAINT UQ_Enfermeros_Nom UNIQUE (Nombre)
);
GO

/* Los enfermeros que ya aparecian escritos en las remisiones. */
INSERT INTO Enfermeros (Nombre)
SELECT DISTINCT LTRIM(RTRIM(r.EnfermeroAsignado))
FROM Remisiones r
WHERE r.EnfermeroAsignado IS NOT NULL
  AND LTRIM(RTRIM(r.EnfermeroAsignado)) <> ''
  AND NOT EXISTS (
        SELECT 1 FROM Enfermeros e
        WHERE e.Nombre = r.EnfermeroAsignado COLLATE Modern_Spanish_CI_AI);
GO

/* ============================================================
   3. LLAVES FORANEAS EN USUARIOS
   ============================================================ */

IF COL_LENGTH('Usuarios', 'EspecialidadId') IS NULL
    ALTER TABLE Usuarios ADD EspecialidadId INT NULL;
GO

UPDATE u
SET EspecialidadId = e.Id
FROM Usuarios u
INNER JOIN Especialidades e
        ON e.Nombre = u.Especialidad COLLATE Modern_Spanish_CI_AI
WHERE u.Especialidad IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_Especialidades')
    ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_Especialidades
        FOREIGN KEY (EspecialidadId) REFERENCES Especialidades(Id);
GO

/* Un especialista sin especialidad no tiene sentido, y una
   especialidad en alguien que no es especialista tampoco. La regla
   vivia solo en la cabeza del programador; aqui queda escrita. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Usuarios_EspecialidadRol')
    ALTER TABLE Usuarios ADD CONSTRAINT CK_Usuarios_EspecialidadRol CHECK (
        (Rol = 'Especialista' AND EspecialidadId IS NOT NULL) OR
        (Rol <> 'Especialista' AND EspecialidadId IS NULL));
GO

/* ============================================================
   4. LLAVES FORANEAS EN REMISIONES
   ============================================================ */

IF COL_LENGTH('Remisiones', 'EspecialidadId') IS NULL
    ALTER TABLE Remisiones ADD EspecialidadId INT NULL;
GO

IF COL_LENGTH('Remisiones', 'EnfermeroId') IS NULL
    ALTER TABLE Remisiones ADD EnfermeroId INT NULL;
GO

UPDATE r
SET EspecialidadId = e.Id
FROM Remisiones r
INNER JOIN Especialidades e
        ON e.Nombre = r.EspecialidadSolicitada COLLATE Modern_Spanish_CI_AI;
GO

UPDATE r
SET EnfermeroId = n.Id
FROM Remisiones r
INNER JOIN Enfermeros n
        ON n.Nombre = r.EnfermeroAsignado COLLATE Modern_Spanish_CI_AI
WHERE r.EnfermeroAsignado IS NOT NULL;
GO

/* Freno de seguridad: si alguna remision quedo sin especialidad, el
   script se detiene antes de borrar la columna de texto. Mejor
   abortar que perder el dato. */
IF EXISTS (SELECT 1 FROM Remisiones WHERE EspecialidadId IS NULL)
BEGIN
    RAISERROR('Hay remisiones sin EspecialidadId. Reviso el emparejamiento antes de continuar.', 16, 1);
    RETURN;
END
GO

ALTER TABLE Remisiones ALTER COLUMN EspecialidadId INT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Remisiones_Especialidades')
    ALTER TABLE Remisiones ADD CONSTRAINT FK_Remisiones_Especialidades
        FOREIGN KEY (EspecialidadId) REFERENCES Especialidades(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Remisiones_Enfermeros')
    ALTER TABLE Remisiones ADD CONSTRAINT FK_Remisiones_Enfermeros
        FOREIGN KEY (EnfermeroId) REFERENCES Enfermeros(Id);
GO

CREATE INDEX IX_Remisiones_EspecialidadId ON Remisiones(EspecialidadId);
GO

/* ============================================================
   5. DPI EN RESIDENTES Y FAMILIARES

   CHAR(13) y no VARCHAR: el DPI guatemalteco siempre tiene trece
   digitos, asi que la longitud fija documenta la regla.

   Admite NULL porque los registros que ya existen no lo tienen, y
   rellenarlos con un valor inventado seria peor que dejarlo vacio.
   El indice unico filtrado impide repetirlo sin obligar a tenerlo.
   ============================================================ */

IF COL_LENGTH('Residentes', 'Dpi') IS NULL
    ALTER TABLE Residentes ADD Dpi CHAR(13) NULL;
GO

IF COL_LENGTH('Familiares', 'Dpi') IS NULL
    ALTER TABLE Familiares ADD Dpi CHAR(13) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Residentes_Dpi')
CREATE UNIQUE INDEX UX_Residentes_Dpi ON Residentes(Dpi) WHERE Dpi IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Familiares_Dpi')
CREATE UNIQUE INDEX UX_Familiares_Dpi ON Familiares(Dpi) WHERE Dpi IS NOT NULL;
GO

/* Trece digitos exactos. La validacion tambien esta en los DTO, pero
   la quiero aqui para que ningun INSERT directo la esquive. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Residentes_Dpi')
    ALTER TABLE Residentes ADD CONSTRAINT CK_Residentes_Dpi
        CHECK (Dpi IS NULL OR Dpi LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Familiares_Dpi')
    ALTER TABLE Familiares ADD CONSTRAINT CK_Familiares_Dpi
        CHECK (Dpi IS NULL OR Dpi LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
GO

/* ============================================================
   6. RETIRO DE LAS COLUMNAS DE TEXTO LIBRE

   A partir de aqui el nombre vive en una sola tabla. Dejarlas
   habria mantenido dos fuentes de verdad para el mismo dato.
   ============================================================ */

IF COL_LENGTH('Usuarios', 'Especialidad') IS NOT NULL
    ALTER TABLE Usuarios DROP COLUMN Especialidad;
GO

IF COL_LENGTH('Remisiones', 'EspecialidadSolicitada') IS NOT NULL
    ALTER TABLE Remisiones DROP COLUMN EspecialidadSolicitada;
GO

IF COL_LENGTH('Remisiones', 'EnfermeroAsignado') IS NOT NULL
    ALTER TABLE Remisiones DROP COLUMN EnfermeroAsignado;
GO

/* ============================================================
   7. DPI DE PRUEBA PARA LOS REGISTROS EXISTENTES

   ===== DATOS FICTICIOS. BORRAR ANTES DE PRODUCCION. =====

   Los residentes y familiares que ya estaban cargados no tienen DPI,
   porque la columna no existia cuando se registraron. Les asigno uno
   aqui con el unico proposito de poder probar y mostrar el campo
   funcionando: sin ningun dato, la columna sale vacia en pantalla y
   no se puede verificar ni el formato ni la restriccion de unicidad.

   Como estan construidos: ocho digitos correlativos, un verificador,
   y '1001' al final, que es el codigo de Suchitepequez y Mazatenango.
   Respetan el formato de trece digitos pero NO son documentos reales
   ni pasan el algoritmo de verificacion del RENAP. Son correlativos a
   proposito, para que a simple vista se note que son inventados.

   Solo toca los registros que no tienen DPI: si ya se capturo uno de
   verdad, este bloque no lo pisa.
   ============================================================ */

;WITH ResidentesSinDpi AS (
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY Id) AS Fila
    FROM Residentes
    WHERE Dpi IS NULL
)
UPDATE r
SET Dpi = RIGHT('00000000' + CAST(10000000 + s.Fila AS VARCHAR(8)), 8)
        + CAST(s.Fila % 10 AS CHAR(1))
        + '1001'
FROM Residentes r
INNER JOIN ResidentesSinDpi s ON s.Id = r.Id;
GO

;WITH FamiliaresSinDpi AS (
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY Id) AS Fila
    FROM Familiares
    WHERE Dpi IS NULL
)
UPDATE f
SET Dpi = RIGHT('00000000' + CAST(20000000 + s.Fila AS VARCHAR(8)), 8)
        + CAST(s.Fila % 10 AS CHAR(1))
        + '1001'
FROM Familiares f
INNER JOIN FamiliaresSinDpi s ON s.Id = f.Id;
GO

PRINT 'DPI de prueba asignados. Son ficticios: revisar antes de cualquier despliegue real.';
GO

PRINT 'Catalogos de especialidades y enfermeros creados; DPI agregado.';
GO

/* ============================================================
   VERIFICACION
   ============================================================ */

SELECT 'Especialidades' AS Catalogo, COUNT(*) AS Registros FROM Especialidades
UNION ALL
SELECT 'Enfermeros', COUNT(*) FROM Enfermeros;
GO

/* Especialidades y si tienen a alguien que las atienda. */
SELECT  e.Id,
        e.Nombre,
        COUNT(u.Id) AS EspecialistasActivos
FROM Especialidades e
LEFT JOIN Usuarios u
       ON u.EspecialidadId = e.Id
      AND u.Rol = 'Especialista'
      AND u.Activo = 1
WHERE e.Activa = 1
GROUP BY e.Id, e.Nombre
ORDER BY e.Nombre;
GO
