CREATE TABLE dbo.UserEDCConnector (
    ID              INT             NOT NULL IDENTITY(1,1),
    UserId          INT             NOT NULL,
    EDCServerName   NVARCHAR(255)   NOT NULL,
    EDCConnectorId  NVARCHAR(255)   NOT NULL,
    ApiKey NVARCHAR(255)   NULL,
    CONSTRAINT PK_UserEDCConnector PRIMARY KEY (ID),
    CONSTRAINT FK_UserEDCConnector_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users (ID)
        ON DELETE CASCADE
);

CREATE TABLE dbo.ProfileEDCConsumer (
    ID                  INT NOT NULL IDENTITY(1,1),
    ProfileId           INT NOT NULL,
    ConsumedProfileId   INT NOT NULL,
    CONSTRAINT PK_ProfileEDCConsumer PRIMARY KEY (ID),
    CONSTRAINT FK_ProfileEDCConsumer_Profile FOREIGN KEY (ProfileId)
        REFERENCES dbo.Profiles (ID)
        ON DELETE CASCADE,
    CONSTRAINT FK_ProfileEDCConsumer_ConsumedProfile FOREIGN KEY (ConsumedProfileId)
        REFERENCES dbo.Profiles (ID)
);


-- =============================================================================
-- Seed: ProfileEDCConsumer
-- Descripción: Relaciones de consumo de datasets EDC entre perfiles,
--              basadas en EcoDataNetDatasetStore.cs (ConsumeGrouped).
-- Cada fila indica que el perfil ProfileId consume datos del perfil ConsumedProfileId.
-- =============================================================================

-- Usamos variables para referenciar los IDs por Reference, evitando hardcodear IDs.
DECLARE @dispatch_office  INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'DISPATCH_OFFICE');
DECLARE @scrap            INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'SCRAP');
DECLARE @public_entity    INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'PUBLIC_ENT');
DECLARE @carrier          INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'CARRIER');
DECLARE @cac              INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'CAC_OP');
DECLARE @plant            INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'PLANT_OP');
DECLARE @producer         INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'PRODUCER');
DECLARE @coordinator      INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'COORDINATOR');

-- =============================================================================
-- OFICINA DE ASIGNACIÓN (dispatch-office)
-- Consume de: scrap, carrier, plant, cac, public-entity, producer
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @scrap)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @scrap);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @carrier)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @carrier);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @plant)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @plant);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @cac)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @cac);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @public_entity)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @public_entity);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @dispatch_office AND ConsumedProfileId = @producer)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@dispatch_office, @producer);

-- =============================================================================
-- SCRAP
-- Consume de: dispatch-office, public-entity, plant
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @scrap AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@scrap, @dispatch_office);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @scrap AND ConsumedProfileId = @public_entity)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@scrap, @public_entity);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @scrap AND ConsumedProfileId = @plant)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@scrap, @plant);

-- =============================================================================
-- ENTIDAD PÚBLICA / AYUNTAMIENTO (public-entity)
-- Consume de: dispatch-office, carrier, scrap
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @public_entity AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@public_entity, @dispatch_office);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @public_entity AND ConsumedProfileId = @carrier)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@public_entity, @carrier);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @public_entity AND ConsumedProfileId = @scrap)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@public_entity, @scrap);

-- =============================================================================
-- TRANSPORTISTA (carrier)
-- Consume de: dispatch-office, scrap
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @carrier AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@carrier, @dispatch_office);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @carrier AND ConsumedProfileId = @scrap)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@carrier, @scrap);

-- =============================================================================
-- OPERADOR DE CENTRO DE ACOPIO (cac)
-- Consume de: dispatch-office
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @cac AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@cac, @dispatch_office);

-- =============================================================================
-- PLANTA DE TRATAMIENTO (plant)
-- Consume de: carrier, dispatch-office
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @plant AND ConsumedProfileId = @carrier)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@plant, @carrier);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @plant AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@plant, @dispatch_office);

-- =============================================================================
-- PRODUCTOR (producer)
-- Consume de: scrap, dispatch-office, plant
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @producer AND ConsumedProfileId = @scrap)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@producer, @scrap);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @producer AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@producer, @dispatch_office);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @producer AND ConsumedProfileId = @plant)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@producer, @plant);

-- =============================================================================
-- COORDINADOR (coordinator)
-- Consume de: dispatch-office
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileEDCConsumer WHERE ProfileId = @coordinator AND ConsumedProfileId = @dispatch_office)
    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId) VALUES (@coordinator, @dispatch_office);


    IF NOT EXISTS (SELECT 1 FROM dbo.Profiles WHERE Reference = 'REGULATOR')
    INSERT INTO dbo.Profiles (Reference, Description, CreateDate)
    VALUES ('REGULATOR', 'Regulador, Autoridad de supervisión normativa', GETDATE());

IF NOT EXISTS (SELECT 1 FROM dbo.Profiles WHERE Reference = 'CERTIFIER')
    INSERT INTO dbo.Profiles (Reference, Description, CreateDate)
    VALUES ('CERTIFIER', 'Certificador / Auditor, Validación y coherencia', GETDATE());




    GO

    DECLARE @scrap  INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'SCRAP');
    DECLARE @producer  INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'PRODUCER');
    DECLARE @carrier  INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'CARRIER');
    DECLARE @plant  INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'PLANT_OP');
    DECLARE @office INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'DISPATCH_OFFICE');
    DECLARE @certifier INT = (SELECT ID FROM dbo.Profiles WHERE Reference = 'CERTIFIER');

    INSERT INTO dbo.ProfileEDCConsumer (ProfileId, ConsumedProfileId)
    VALUES 
        (@scrap, @producer),
        (@certifier, @office),
        (@certifier, @carrier),
        (@certifier, @plant);

    DELETE FROM dbo.ProfileEDCConsumer
    WHERE ProfileId = @carrier AND ConsumedProfileId = @scrap;

 GO

 CREATE TABLE [dbo].[ExplorerLayoutConfigs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OwnerId] [uniqueidentifier] NOT NULL,
	[UserId] [int] NOT NULL,
	[AssetId] [nvarchar](512) NOT NULL,
	[ProviderParticipantId] [nvarchar](512) NOT NULL,
	[DatasetName] [nvarchar](256) NULL,
	[LayoutConfigJson] [nvarchar](max) NOT NULL,
	[SchemaHash] [nvarchar](64) NULL,
	[CreatedAt] [datetime2](0) NOT NULL,
	[UpdatedAt] [datetime2](0) NOT NULL,
 CONSTRAINT [PK_ExplorerLayoutConfigs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[ExplorerLayoutConfigs] ADD  DEFAULT (N'[]') FOR [LayoutConfigJson]
GO
