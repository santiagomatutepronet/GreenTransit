-- Script: Crear tabla ServiceOrderResidues
-- Ejecutar contra: greentransitdb

IF OBJECT_ID('dbo.ServiceOrderResidues', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ServiceOrderResidues] (
        [Id]              uniqueidentifier NOT NULL,
        [IdServiceOrder]  uniqueidentifier NOT NULL,
        [SortOrder]       int              NOT NULL,
        [IdLERCode]       uniqueidentifier NULL,
        [ProductUse]      int              NULL,
        [ProductCategory] int              NULL,
        [EstimatedWeight] decimal(18,3)    NULL,
        [MeasureUnit]     int              NULL,
        [Units]           int              NULL,
        CONSTRAINT PK_ServiceOrderResidues PRIMARY KEY CLUSTERED (Id ASC)
    );

    ALTER TABLE [dbo].[ServiceOrderResidues]
        ADD CONSTRAINT FK_ServiceOrderResidues_ServiceOrders
        FOREIGN KEY (IdServiceOrder)
        REFERENCES [dbo].[ServiceOrders] (Id)
        ON DELETE CASCADE;

    ALTER TABLE [dbo].[ServiceOrderResidues]
        ADD CONSTRAINT FK_ServiceOrderResidues_LERCodes
        FOREIGN KEY (IdLERCode)
        REFERENCES [dbo].[LERCodes] (Id);

    CREATE NONCLUSTERED INDEX IX_ServiceOrderResidues_IdServiceOrder
        ON [dbo].[ServiceOrderResidues] (IdServiceOrder ASC);

    PRINT 'Tabla ServiceOrderResidues creada correctamente.';
END
ELSE
BEGIN
    PRINT 'La tabla ServiceOrderResidues ya existe.';
END
