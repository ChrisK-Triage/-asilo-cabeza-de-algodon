IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AsiloCabezaAlgodonDB')
BEGIN
    CREATE DATABASE AsiloCabezaAlgodonDB;
END
GO

USE AsiloCabezaAlgodonDB;
GO
