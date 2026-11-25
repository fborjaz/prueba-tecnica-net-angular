/* =============================================================================
   00 - Creación de la base de datos
   Ejecutar solo en instalaciones manuales. En Docker / ejecución normal la API
   crea la base de datos y ejecuta los scripts 01..03 automáticamente al iniciar.
   ============================================================================= */
IF DB_ID('CashManagementDb') IS NULL
BEGIN
    CREATE DATABASE CashManagementDb;
END
GO
