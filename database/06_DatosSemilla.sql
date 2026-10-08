/* ============================================================
   06_DatosSemilla.sql
   Datos minimos para que el sistema arranque y pueda mostrarse
   en Swagger sin pantallas vacias.

   Los hashes de contrasena son marcadores. Antes de desplegar
   genero los reales desde el endpoint de registro con BCrypt.
   ============================================================ */

USE AsiloCabezaAlgodonDB;
GO

/* ---------------- Usuarios ---------------- */
IF NOT EXISTS (SELECT 1 FROM Usuarios)
INSERT INTO Usuarios (NombreCompleto, Email, PasswordHash, Rol, Especialidad) VALUES
 ('Licda. Monica Herrera', 'admin@asilo.org',             'PENDIENTE_HASH_BCRYPT', 'Administrador',      NULL),
 ('Dr. Luis Arevalo',      'medico.general@asilo.org',    'PENDIENTE_HASH_BCRYPT', 'MedicoGeneral',      NULL),
 ('Lic. Andrea Solis',     'coordinacion@fundacion.org',  'PENDIENTE_HASH_BCRYPT', 'Fundacion',          NULL),
 ('Dra. Silvia Paz',       'silvia.paz@fundacion.org',    'PENDIENTE_HASH_BCRYPT', 'Especialista',       'Neurologia'),
 ('Dr. Mario Coronado',    'mario.coronado@fundacion.org','PENDIENTE_HASH_BCRYPT', 'Especialista',       'Cardiologia'),
 ('Tec. Byron Calderon',   'laboratorio@fundacion.org',   'PENDIENTE_HASH_BCRYPT', 'TecnicoLabFarmacia', NULL);
GO

/* ---------------- Convenio vigente ----------------
   20% sobre consulta, laboratorio y farmacia. No aplica sobre
   la cuota mensual de estancia, que es ingreso del asilo. */
IF NOT EXISTS (SELECT 1 FROM ConveniosFundacion)
INSERT INTO ConveniosFundacion (Descripcion, PorcentajeDescuento, FechaInicio, Vigente) VALUES
 ('Convenio general 2026 - 20% sobre servicios clinicos', 20.00, '2026-01-01', 1);
GO

/* ---------------- Residentes ---------------- */
IF NOT EXISTS (SELECT 1 FROM Residentes)
INSERT INTO Residentes (Nombres, Apellidos, FechaNacimiento, Psicopatologias, MedicamentosCajon, CuotaMensual, FechaIngreso) VALUES
 ('Rosa Elena',  'Mendez',  '1945-04-12', 'Demencia senil leve',   'Donepezilo 10 mg cada 24 h; Losartan 50 mg cada 12 h', 1200.00, '2023-03-12'),
 ('Julio Cesar', 'Ramirez', '1949-08-30', 'Hipertension arterial', 'Enalapril 10 mg cada 12 h',                            1200.00, '2024-07-05'),
 ('Marta Lucia', 'Ortiz',   '1942-11-03', 'Diabetes tipo 2',       'Metformina 850 mg cada 12 h',                          1350.00, '2022-11-19'),
 ('Fernando',    'Aguilar', '1953-02-18', NULL,                    NULL,                                                   1200.00, '2026-02-02');
GO

/* ---------------- Familiares ---------------- */
IF NOT EXISTS (SELECT 1 FROM Familiares)
INSERT INTO Familiares (ResidenteId, Nombre, Email, Telefono) VALUES
 (1, 'Ana Mendez',      'ana.mendez@correo.com',      '5555-4444'),
 (2, 'Pedro Ramirez',   'pedro.ramirez@correo.com',   '5555-5555'),
 (3, 'Claudia Ortiz',   'claudia.ortiz@correo.com',   '5555-6666'),
 (4, 'Rodrigo Aguilar', 'rodrigo.aguilar@correo.com', '5555-7777');
GO

/* ---------------- Remisiones de ejemplo ---------------- */
IF NOT EXISTS (SELECT 1 FROM Remisiones)
INSERT INTO Remisiones (ResidenteId, MedicoGeneralId, Sintomatologia, EspecialidadSolicitada, EnfermeroAsignado, FechaSolicitud, Estado) VALUES
 (1, 2, 'Desorientacion nocturna progresiva durante las ultimas tres semanas.', 'Neurologia',    'Karla Jimenez',  '2026-09-22', 0),
 (2, 2, 'Control de presion arterial no estabilizado pese al tratamiento.',     'Cardiologia',   'Oscar Baldizon', '2026-09-24', 0),
 (4, 2, 'Dolor lumbar persistente tras caida en el pabellon.',                  'Traumatologia', 'Karla Jimenez',  '2026-09-17', 0);
GO

PRINT 'Datos semilla insertados.';
GO
