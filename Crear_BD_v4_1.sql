/****** Object:  Database [greentransitdb]    Script Date: 31/03/2026 18:14:47 ******/
CREATE DATABASE [greentransitdb]  (EDITION = 'Basic', SERVICE_OBJECTIVE = 'Basic', MAXSIZE = 2 GB) WITH CATALOG_COLLATION = SQL_Latin1_General_CP1_CI_AS, LEDGER = OFF;
GO
ALTER DATABASE [greentransitdb] SET COMPATIBILITY_LEVEL = 170
GO
ALTER DATABASE [greentransitdb] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [greentransitdb] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [greentransitdb] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [greentransitdb] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [greentransitdb] SET ARITHABORT OFF 
GO
ALTER DATABASE [greentransitdb] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [greentransitdb] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [greentransitdb] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [greentransitdb] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [greentransitdb] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [greentransitdb] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [greentransitdb] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [greentransitdb] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [greentransitdb] SET ALLOW_SNAPSHOT_ISOLATION ON 
GO
ALTER DATABASE [greentransitdb] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [greentransitdb] SET READ_COMMITTED_SNAPSHOT ON 
GO
ALTER DATABASE [greentransitdb] SET  MULTI_USER 
GO
ALTER DATABASE [greentransitdb] SET ENCRYPTION ON
GO
ALTER DATABASE [greentransitdb] SET QUERY_STORE = ON
GO
ALTER DATABASE [greentransitdb] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 7), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 10, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
/*** Los scripts de las configuraciones con ámbito de base de datos en Azure deben ejecutarse dentro de la conexión de base de datos de destino. ***/
GO
-- ALTER DATABASE SCOPED CONFIGURATION SET MAXDOP = 8;
GO

-- ============================================================
-- NUEVA TABLA MAESTRA: Entities  (introducida en v2)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Entities](
	[Id]                        [uniqueidentifier] NOT NULL  CONSTRAINT [DF_Entities_Id] DEFAULT (NEWSEQUENTIALID()),
	[Name]                      [nvarchar](256) NOT NULL,
	[NationalId]                [nvarchar](64)  NULL,
	[CenterCode]                [nvarchar](256) NULL,
	-- Rol principal. Valores: 'Source','Destination','Carrier','OperatorTransfer','SCRAP','Producer','Plant','CAC','PublicEntity','Coordinator','Other'
	[EntityRole]                [nvarchar](64)  NOT NULL,
	[TypeThirdParty]            [nvarchar](256) NULL,
	[InscriptionType]           [nvarchar](64)  NULL,
	[InscriptionNumber]         [nvarchar](256) NULL,
	[CountryCode]               [nvarchar](64)  NULL,
	[StateCode]                 [nvarchar](64)  NULL,
	[ZipCode]                   [nvarchar](64)  NULL,
	[ProvinceCode]              [nvarchar](256) NULL,
	[MunicipalityCode]          [nvarchar](256) NULL,
	[Address]                   [nvarchar](512) NULL,
	[Latitude]                  [nvarchar](64)  NULL,
	[Longitude]                 [nvarchar](64)  NULL,
	[PhoneNumber]               [nvarchar](64)  NULL,
	[Email]                     [nvarchar](256) NULL,
	[ContactPerson]             [nvarchar](256) NULL,
	[EconomicActivity]          [nvarchar](256) NULL,
	[EntityType]                [nvarchar](256) NULL,
	[IsActive]                  [bit]           NOT NULL CONSTRAINT [DF_Entities_IsActive] DEFAULT (1),
	[SourceSystem]              [nvarchar](64)  NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_Entities_CreatedAt] DEFAULT (SYSUTCDATETIME()),
	[UpdatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_Entities_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_Entities] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Entities_NationalId]  ON [dbo].[Entities] ([NationalId] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Entities_EntityRole]  ON [dbo].[Entities] ([EntityRole] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Entities_CenterCode]  ON [dbo].[Entities] ([CenterCode] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

-- ============================================================
-- NUEVA TABLA MAESTRA: LERCodes  (introducida en v2)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LERCodes](
	[Id]                        [uniqueidentifier] NOT NULL  CONSTRAINT [DF_LERCodes_Id] DEFAULT (NEWSEQUENTIALID()),
	[Code]                      [nvarchar](32)  NOT NULL,
	[CodeExtended]              [nvarchar](64)  NULL,
	[Description]               [nvarchar](512) NOT NULL,
	[Chapter]                   [nvarchar](8)   NULL,
	[ChapterDescription]        [nvarchar](256) NULL,
	[SubChapter]                [nvarchar](8)   NULL,
	[SubChapterDescription]     [nvarchar](256) NULL,
	[IsDangerous]               [bit]           NOT NULL CONSTRAINT [DF_LERCodes_IsDangerous] DEFAULT (0),
	[IsRAEE]                    [bit]           NOT NULL CONSTRAINT [DF_LERCodes_IsRAEE]      DEFAULT (0),
	[DefaultProductCategory]    [nvarchar](256) NULL,
	[Notes]                     [nvarchar](512) NULL,
	[IsActive]                  [bit]           NOT NULL CONSTRAINT [DF_LERCodes_IsActive]    DEFAULT (1),
	[CreatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_LERCodes_CreatedAt]   DEFAULT (SYSUTCDATETIME()),
	[UpdatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_LERCodes_UpdatedAt]   DEFAULT (SYSUTCDATETIME()),
 CONSTRAINT [PK_LERCodes]      PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_LERCodes_Code] UNIQUE NONCLUSTERED  ([Code] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_LERCodes_Chapter]     ON [dbo].[LERCodes] ([Chapter]     ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_LERCodes_IsDangerous] ON [dbo].[LERCodes] ([IsDangerous] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

-- ============================================================
-- NUEVA TABLA MAESTRA: Residues  (introducida en v3)
--
-- Catálogo maestro de residuos y productos declarados.
-- Centraliza los campos descriptivos que aparecían repetidos
-- en WasteMoveResidues, EntryPlantResidues, EntryCACResidues,
-- TreatmentPlantResidues, Products y ProductSpecs.
--
-- El campo ResidueType discrimina el contexto del registro:
--   'Waste'       -> residuo genérico / operativo
--   'Product'     -> producto puesto en el mercado (declaración)
--   'ProductSpec' -> ficha técnica de producto (ecodiseño)
--
-- Las tablas operativas (WasteMoveResidues, EntryPlantResidues,
-- etc.) referencian a esta tabla para obtener la descripción
-- normalizada, manteniendo sus propios campos de cantidad,
-- peso, precio y tratamiento (datos de instancia, no de catálogo).
-- NOTA: NTNumber, DINumber y DIPhase NO están aquí porque son
-- datos del traslado concreto (dos traslados del mismo residuo
-- pueden tener BT/DI distintos). Esos campos permanecen en
-- WasteMoveResidues, que es donde se genera el documento.
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Residues](
	[Id]                        [uniqueidentifier] NOT NULL  CONSTRAINT [DF_Residues_Id] DEFAULT (NEWSEQUENTIALID()),

	-- ── Clasificación ─────────────────────────────────────────
	-- Tipo de registro. Valores: 'Waste' | 'Product' | 'ProductSpec'
	[ResidueType]               [nvarchar](32)  NOT NULL,
	-- Nombre / descripción del residuo o producto
	[Name]                      [nvarchar](512) NOT NULL,
	[Description]               [nvarchar](512) NULL,
	-- Referencia interna del producto/residuo (SKU, código interno, etc.)
	[Reference]                 [nvarchar](128) NULL,

	-- ── Código LER asociado ───────────────────────────────────
	-- Código LER principal del residuo o potencial al fin de vida
	[IdLERCode]                 [uniqueidentifier] NULL,   -- FK -> LERCodes

	-- ── Atributos de peligrosidad ─────────────────────────────
	[IsDangerous]               [bit]           NOT NULL CONSTRAINT [DF_Residues_IsDangerous] DEFAULT (0),
	[IsRAEE]                    [bit]           NOT NULL CONSTRAINT [DF_Residues_IsRAEE]      DEFAULT (0),
	-- Código de peligrosidad (H-codes, UN, ADR…)
	[DangerousCode]             [nvarchar](256) NULL,

	-- ── Clasificación de producto / residuo ───────────────────
	-- Uso del producto según nomenclatura normativa (int -> dicProductDeclarationUse)
	[ProductUse]                [nvarchar](64)  NULL,
	-- Categoría del producto / residuo (int -> dicProductDeclarationCategory)
	[ProductCategory]           [nvarchar](256) NULL,

	-- ── Características físicas ───────────────────────────────
	-- Peso por unidad en kg (para productos envasados/unitarios)
	[WeightPerUnitKg]           [decimal](18,3) NULL,
	-- Unidad de medida por defecto ('kg','t','ud','l'…)
	[DefaultMeasureUnit]        [nvarchar](64)  NULL,

	-- ── Atributos de ecodiseño / circularidad (ProductSpec) ───
	-- Índice de reparabilidad (0-10 o escala normativa aplicable)
	[ReparabilityIndex]         [int]           NULL,
	-- Facilidad de desmontaje: 'Easy' | 'Medium' | 'Hard'
	[DisassemblyEase]           [nvarchar](32)  NULL,
	-- ¿Contiene sustancias peligrosas declaradas?
	[ContainsHazardous]         [bit]           NULL,
	-- % de contenido reciclado sobre el total del producto
	[RecycledContentPercent]    [decimal](5,2)  NULL,
	-- Composición de materiales en JSON
	[CompositionJson]           [nvarchar](max) NULL,
	-- Códigos LER potenciales al fin de vida, en JSON
	[PotentialLERCodesJson]     [nvarchar](max) NULL,
	-- Otros materiales (para líneas de declaración de producto)
	[MaterialsJson]             [nvarchar](max) NULL,

	-- ── Origen del dato ───────────────────────────────────────
	-- Productor asociado (si aplica)
	[IdProducer]                [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Producer')
	-- Referencia del productor en su propio sistema
	[ProducerRef]               [nvarchar](64)  NULL,
	-- Sistema origen del registro
	[SourceSystem]              [nvarchar](64)  NULL,

	-- ── Auditoría ─────────────────────────────────────────────
	[IsActive]                  [bit]           NOT NULL CONSTRAINT [DF_Residues_IsActive]  DEFAULT (1),
	[Version]                   [int]           NOT NULL CONSTRAINT [DF_Residues_Version]   DEFAULT (1),
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_Residues_CreatedAt] DEFAULT (SYSUTCDATETIME()),
	[UpdatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_Residues_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
	[IdUser]                    [int]           NOT NULL,

 CONSTRAINT [PK_Residues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- Índices sobre Residues
CREATE NONCLUSTERED INDEX [IX_Residues_ResidueType]  ON [dbo].[Residues] ([ResidueType]  ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Residues_IdLERCode]    ON [dbo].[Residues] ([IdLERCode]    ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Residues_IdProducer]   ON [dbo].[Residues] ([IdProducer]   ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Residues_ProductCat]   ON [dbo].[Residues] ([ProductCategory] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Residues_IsDangerous]  ON [dbo].[Residues] ([IsDangerous]  ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: Agreements
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Agreements](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[AgreementNumber]           [nvarchar](64)  NOT NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[EffectiveFrom]             [datetime2](0)  NOT NULL,
	[EffectiveTo]               [datetime2](0)  NULL,
	[IdScrap]                   [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='SCRAP')
	[IdPublicEntity]            [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='PublicEntity')
	[IdCoordinator]             [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Coordinator')
	[WasteStream]               [nvarchar](32)  NULL,
	[SubStream]                 [nvarchar](32)  NULL,
	[AutonomousCommunity]       [nvarchar](64)  NULL,
	[ProvinceCode]              [nvarchar](16)  NULL,
	[MunicipalityCode]          [nvarchar](16)  NULL,
	[CoveredMethodsJson]        [nvarchar](max) NULL,
	[TariffModelType]           [nvarchar](64)  NULL,
	[Currency]                  [nvarchar](8)   NULL,
	[TariffRulesJson]           [nvarchar](max) NULL,
	[MinimumsJson]              [nvarchar](max) NULL,
	[ObligationsJson]           [nvarchar](max) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_Agreements] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[AgreementDocuments] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AgreementDocuments](
	[Id]                        [uniqueidentifier] NOT NULL,
	[AgreementId]               [uniqueidentifier] NOT NULL,
	[DocumentType]              [nvarchar](64)  NOT NULL,
	[DocumentId]                [nvarchar](128) NULL,
	[DocumentHash]              [nvarchar](128) NULL,
	[SignedAt]                  [datetime2](0)  NULL,
	[SignatureProvider]         [nvarchar](64)  NULL,
 CONSTRAINT [PK_AgreementDocuments] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: ServiceOrders
-- CAMBIOS v3: ProductUse/ProductCategory permanecen como
-- clasificadores de la orden (no son residuo en sí).
-- IdLERCode apunta al catálogo LERCodes (ya en v2).
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ServiceOrders](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[ServiceOrderNumber]        [nvarchar](64)  NOT NULL,
	[IssuedAt]                  [datetime2](0)  NOT NULL,
	[IdIssuedBy]                [uniqueidentifier] NULL,   -- FK -> Entities
	[IssuedByName]              [nvarchar](256) NULL,
	[IssuedByNationalId]        [nvarchar](32)  NULL,
	[IssuedByCenterCode]        [nvarchar](64)  NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[Priority]                  [nvarchar](16)  NOT NULL,
	[WasteStream]               [nvarchar](32)  NULL,
	[SubStream]                 [nvarchar](32)  NULL,
	[ProductUse]                [int]           NULL,
	[ProductCategory]           [int]           NULL,
	[IdLERCode]                 [uniqueidentifier] NULL,   -- FK -> LERCodes
	-- Punto de recogida como entidad registrada en el maestro
	-- (EntityRole='Source' / 'CAC' / 'PublicEntity' según tipo de punto)
	[IdPickupPoint]             [uniqueidentifier] NULL,   -- FK -> Entities
	[PlannedPickupStart]        [datetime2](0)  NULL,
	[PlannedPickupEnd]          [datetime2](0)  NULL,
	[PlannedDeliveryStart]      [datetime2](0)  NULL,
	[PlannedDeliveryEnd]        [datetime2](0)  NULL,
	[EstimatedWeight]           [decimal](18,3) NULL,
	[MeasureUnit]               [int]           NULL,
	[Units]                     [int]           NULL,
	[ContainersJson]            [nvarchar](max) NULL,
	[IdCarrier]                 [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Carrier')
	[IdPlannedPlant]            [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Plant')
	[WasteMoveReference]        [nvarchar](128) NULL,
	[TicketScalePlanned]        [nvarchar](128) NULL,
	[ActualPickupStart]         [datetime2](0)  NULL,
	[ActualPickupEnd]           [datetime2](0)  NULL,
	[ActualDeliveryStart]       [datetime2](0)  NULL,
	[ActualDeliveryEnd]         [datetime2](0)  NULL,
	[TransportDistanceKm]       [decimal](18,3) NULL,
	[TransportDurationMin]      [int]           NULL,
	[VehicleRegistration]       [nvarchar](32)  NULL,
	[VehicleType]               [nvarchar](32)  NULL,
	[FuelType]                  [nvarchar](32)  NULL,
	[EuroClass]                 [nvarchar](16)  NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_ServiceOrders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- ============================================================
-- Tabla: Settlements
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Settlements](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[SettlementNumber]          [nvarchar](64)  NOT NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[AgreementId]               [uniqueidentifier] NOT NULL,
	[Year]                      [int]           NOT NULL,
	[Month]                     [int]           NULL,
	[IdScrap]                   [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='SCRAP')
	[IdPublicEntity]            [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='PublicEntity')
	[Currency]                  [nvarchar](8)   NOT NULL,
	[BaseAmount]                [decimal](18,2) NOT NULL,
	[AdjustmentsAmount]         [decimal](18,2) NOT NULL,
	[TaxAmount]                 [decimal](18,2) NOT NULL,
	[TotalAmount]               [decimal](18,2) NOT NULL,
	[EvidenceRefsJson]          [nvarchar](max) NULL,
	[Validator]                 [nvarchar](64)  NULL,
	[ValidationStatus]          [nvarchar](32)  NULL,
	[ValidatedAt]               [datetime2](0)  NULL,
	[ValidationRef]             [nvarchar](128) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_Settlements] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- ============================================================
-- Tabla: SettlementLines
-- CAMBIOS v2: IdLERCode (FK LERCodes)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SettlementLines](
	[Id]                        [uniqueidentifier] NOT NULL,
	[SettlementId]              [uniqueidentifier] NOT NULL,
	[ProductCategory]           [int]           NULL,
	[IdLERCode]                 [uniqueidentifier] NULL,   -- FK -> LERCodes
	[WeightKg]                  [decimal](18,3) NOT NULL,
	[PricePerKg]                [decimal](18,6) NOT NULL,
	[Amount]                    [decimal](18,2) NOT NULL,
	[EvidenceType]              [nvarchar](64)  NULL,
	[SourceIdsJson]             [nvarchar](max) NULL,
 CONSTRAINT [PK_SettlementLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- ============================================================
-- Tabla: MarketShares
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MarketShares](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[IdScrap]                   [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='SCRAP')
	[Category]                  [nvarchar](256) NOT NULL,
	[AutonomousCommunity]       [nvarchar](256) NULL,
	[Year]                      [int]           NOT NULL,
	[Weight]                    [decimal](18,2) NOT NULL,
	[Period]                    [int]           NULL,
	[EffectiveFrom]             [date]          NULL,
	[EffectiveTo]               [date]          NULL,
	[FlowType]                  [nvarchar](32)  NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
 CONSTRAINT [PK_MarketShares] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: WasteMoves
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WasteMoves](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[GatheredDate]              [datetime]      NULL,
	[RequestDate]               [datetime]      NULL,
	[PlantEntryDate]            [datetime]      NULL,
	[IdScrap]                   [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='SCRAP')
	[IdScrap2]                  [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='SCRAP')
	[IdSource]                  [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Source')
	[IdDestination]             [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Destination'/'Plant')
	[IdOperatorTransfer]        [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='OperatorTransfer')
	[WasteMoveReference]        [nvarchar](256) NULL,
	[Lot]                       [nvarchar](256) NULL,
	[PlannedPickupStart]        [datetime2](0)  NULL,
	[PlannedPickupEnd]          [datetime2](0)  NULL,
	[PlannedDeliveryStart]      [datetime2](0)  NULL,
	[PlannedDeliveryEnd]        [datetime2](0)  NULL,
	[ActualPickupStart]         [datetime2](0)  NULL,
	[ActualPickupEnd]           [datetime2](0)  NULL,
	[ActualDeliveryStart]       [datetime2](0)  NULL,
	[ActualDeliveryEnd]         [datetime2](0)  NULL,
	[DocumentId]                [nvarchar](128) NULL,
	[DocumentHash]              [nvarchar](128) NULL,
	[SignatureStatus]           [nvarchar](32)  NULL,
	[DateCreateSys]             [datetime]      NULL,
	[DateModifiedSys]           [datetime]      NULL,
	[IdUser]                    [int]           NOT NULL,
	[ServiceOrderId]            [uniqueidentifier] NULL,
	[ServiceStatus]             [nvarchar](32)  NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
 CONSTRAINT [PK_WasteMoves] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: WasteMoveResidues
-- CAMBIOS v2: IdLERCode, IdCarrier (FK maestros)
-- CAMBIOS v3: IdResidue (FK -> Residues)
--   Eliminados: ResidueName, Description, dangerous, Raee,
--   ProductUse, ProductCategory, DangerousCode
-- CAMBIOS v4: NTNumber/DINumber/DIPhase restaurados aquí (son datos
--   de instancia del traslado, no del catálogo de residuos).
--   TreatmentOperationDestiny -> IdTreatmentOperationDestiny (FK -> TreatmentOperations)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WasteMoveResidues](
	[Id]                        [uniqueidentifier] NOT NULL,
	[IdWasteMove]               [uniqueidentifier] NOT NULL,
	-- Residuo del catálogo maestro
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues
	-- Datos de instancia operativa (cantidades reales del traslado)
	[Weight]                    [decimal](18,2) NULL,
	[MeasureUnit]               [nvarchar](64)  NULL,
	[Units]                     [int]           NULL,
	[unitPriceKg]               [decimal](18,2) NULL,
	[DateDelivery]              [datetime]      NULL,
	-- Documentación normativa de peligrosos: específica de este traslado,
	-- no del residuo en sí (mismo residuo puede tener BT/DI distintos)
	[NTNumber]                  [nvarchar](64)  NULL,   -- Número de Notificación de Traslado
	[DINumber]                  [nvarchar](64)  NULL,   -- Número de Documento de Identificación
	[DIPhase]                   [nvarchar](12)  NULL,   -- Fase del DI (E1/E2/E3/E4/E5…)
	-- Operación de tratamiento destino (FK -> TreatmentOperations)
	[IdTreatmentOperationDestiny] [uniqueidentifier] NULL,   -- FK -> TreatmentOperations
	-- Transportista del residuo
	[IdCarrier]                 [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Carrier')
	-- Información de transporte
	[TransportInfo_vehicleRegistration]         [nvarchar](256) NULL,
	[TransportInfo_vehicleRegistrationTrailer]  [nvarchar](256) NULL,
	[TransportInfo_TransportDuration]           [decimal](18,2) NULL,
	[TransportInfo_TransportDistance]           [decimal](18,2) NULL,
	[TransportInfo_TransportCarbonEmissions]    [decimal](18,2) NULL,
	[VehicleType]               [nvarchar](32)  NULL,
	[FuelType]                  [nvarchar](32)  NULL,
	[EuroClass]                 [nvarchar](16)  NULL,
	[EmissionFactorSetId]       [uniqueidentifier] NULL,
	[EmissionFactorVersion]     [nvarchar](32)  NULL,
 CONSTRAINT [PK_WasteMoveResidues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: EntryPlants
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EntryPlants](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[IdWasteMove]               [uniqueidentifier] NOT NULL,
	[WasteMoveReference]        [nvarchar](256) NULL,
	[TicketScale]               [nvarchar](256) NULL,
	[PlantEntryDate]            [datetime]      NULL,
	[TypeContainer]             [nvarchar](256) NULL,
	[PriceContainer]            [decimal](18,2) NULL,
	[DateCreateSys]             [datetime]      NULL,
	[DateModifiedSys]           [datetime]      NULL,
	[IdUser]                    [int]           NOT NULL,
	[GrossWeight]               [decimal](18,3) NULL,
	[TareWeight]                [decimal](18,3) NULL,
	[NetWeight]                 [decimal](18,3) NULL,
	[WeighbridgeId]             [nvarchar](64)  NULL,
	[ServiceOrderId]            [uniqueidentifier] NULL,
 CONSTRAINT [PK_EntryPlants] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: EntryPlantResidues
-- CAMBIOS v2: IdLERCode
-- CAMBIOS v3: IdResidue (FK -> Residues)
--   Eliminados: ResidueName, Dangerous, Raee, ProductCategory
--   Se mantienen: Weight, MeasureUnit, Units, PriceWeight, PriceUnit
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EntryPlantResidues](
	[Id]                        [uniqueidentifier] NOT NULL,
	[IdEntryPlant]              [uniqueidentifier] NOT NULL,
	-- Residuo del catálogo maestro
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues
	-- Datos de instancia (pesaje real en planta)
	[Weight]                    [decimal](18,2) NULL,
	[MeasureUnit]               [nvarchar](64)  NULL,
	[Units]                     [int]           NULL,
	[PriceWeight]               [decimal](18,2) NULL,
	[PriceUnit]                 [decimal](18,2) NULL,
 CONSTRAINT [PK_EntryPlantResidues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: TreatmentPlants
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TreatmentPlants](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[IdWasteMove]               [uniqueidentifier] NULL,
	[WasteMoveReference]        [nvarchar](256) NULL,
	[TicketScale]               [nvarchar](256) NULL,
	[PlantTreatmentDate]        [datetime]      NULL,
	[TypeContainer]             [nvarchar](256) NULL,
	[PriceContainer]            [decimal](18,2) NULL,
	[DateCreateSys]             [datetime]      NULL,
	[DateModifiedSys]           [datetime]      NULL,
	[IdUser]                    [int]           NOT NULL,
	[ServiceOrderId]            [uniqueidentifier] NULL,
	-- Operación de tratamiento aplicada (FK -> TreatmentOperations)
	[IdTreatmentOperation]      [uniqueidentifier] NULL,   -- FK -> TreatmentOperations
	[ImproperWeight]            [decimal](18,3) NULL,
	[QualityMetricsJson]        [nvarchar](max) NULL,
	[IncidentId]                [uniqueidentifier] NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
 CONSTRAINT [PK_TreatmentPlants] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- ============================================================
-- Tabla: TreatmentPlantResidues
-- CAMBIOS v2: 4 IdLERCode (entrada, reutilizado, valorizado, rechazo)
-- CAMBIOS v3: IdResidue y 3 IdResidue de salida (FK -> Residues)
--   Los 4 flujos (entrada, reused, valued, remove) apuntan
--   cada uno a su registro en Residues, que a su vez lleva
--   el IdLERCode correspondiente (eliminando los 4 IdLERCode
--   directos de esta tabla — ahora se obtienen via JOIN).
--   Se mantienen: pesos, unidades y precios de cada fracción.
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TreatmentPlantResidues](
	[Id]                        [uniqueidentifier] NOT NULL,
	[IdTreatmentPlant]          [uniqueidentifier] NOT NULL,

	-- ── Residuo de entrada ────────────────────────────────────
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues (residuo entrante)
	[Category]                  [nvarchar](256) NULL,
	[WeightTotal]               [decimal](18,2) NULL,
	[MeasureUnit]               [nvarchar](64)  NULL,
	[Units]                     [int]           NULL,
	[PriceWeight]               [decimal](18,2) NULL,
	[PriceUnit]                 [decimal](18,2) NULL,

	-- ── Fracción reutilizada ──────────────────────────────────
	[IdResidueReused]           [uniqueidentifier] NULL,   -- FK -> Residues (residuo reutilizado)
	[WeightReused]              [decimal](18,2) NULL,
	[MeasureUnitReused]         [nvarchar](64)  NULL,
	[UnitsReused]               [int]           NULL,

	-- ── Fracción valorizada ───────────────────────────────────
	[IdResidueValued]           [uniqueidentifier] NULL,   -- FK -> Residues (residuo valorizado)
	[WeightValued]              [decimal](18,2) NULL,
	[MeasureUnitValued]         [nvarchar](64)  NULL,
	[UnitsValued]               [int]           NULL,

	-- ── Fracción rechazo ──────────────────────────────────────
	[IdResidueRemove]           [uniqueidentifier] NULL,   -- FK -> Residues (fracción de rechazo)
	[WeightRemove]              [decimal](18,2) NULL,
	[MeasureUnitRemove]         [nvarchar](64)  NULL,
	[UnitsRemove]               [int]           NULL,

 CONSTRAINT [PK_TreatmentPlantResidues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: EntryCACs
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EntryCACs](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[IdWasteMove]               [uniqueidentifier] NOT NULL,
	[WasteMoveReference]        [nvarchar](256) NULL,
	[CACEntryDate]              [datetime]      NULL,
	[TypeContainer]             [nvarchar](256) NULL,
	[PriceContainer]            [decimal](18,2) NULL,
	[DateCreateSys]             [datetime]      NULL,
	[DateModifiedSys]           [datetime]      NULL,
	[IdUser]                    [int]           NOT NULL,
	[CollectionMethod]          [nvarchar](32)  NULL,
 CONSTRAINT [PK_EntryCACs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: EntryCACResidues
-- CAMBIOS v2: IdLERCode
-- CAMBIOS v3: IdResidue (FK -> Residues)
--   Eliminados: ResidueName, Dangerous, Raee, ProductCategory
--   Se mantienen: Weight, MeasureUnit, Units, PriceWeight, PriceUnit
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EntryCACResidues](
	[Id]                        [uniqueidentifier] NOT NULL,
	[IdEntryCAC]                [uniqueidentifier] NOT NULL,
	-- Residuo del catálogo maestro
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues
	-- Datos de instancia
	[Weight]                    [decimal](18,2) NULL,
	[MeasureUnit]               [nvarchar](64)  NULL,
	[Units]                     [int]           NULL,
	[PriceWeight]               [decimal](18,2) NULL,
	[PriceUnit]                 [decimal](18,2) NULL,
 CONSTRAINT [PK_EntryCACResidues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: ProductDeclaration
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductDeclaration](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[Period]                    [int]           NULL,
	[Year]                      [int]           NULL,
	[Month]                     [int]           NULL,
	[Currency]                  [nvarchar](256) NULL,
	[State]                     [nvarchar](64)  NULL,
	[DateCreate]                [datetime]      NULL,
	[DateEmit]                  [datetime]      NULL,
	[Reference]                 [nvarchar](256) NULL,
	[IdProducer]                [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Producer')
	[Amount]                    [decimal](18,2) NULL,
	[Type]                      [nvarchar](256) NULL,
	[DateCreateSys]             [datetime]      NULL,
	[DateModifiedSys]           [datetime]      NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_ProductDeclaration] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: Products  (líneas de declaración de producto)
-- CAMBIOS v3: IdResidue (FK -> Residues, ResidueType='Product')
--   Eliminados: Description, WeightPerUnitKg, ReparabilityIndex,
--   RecycledContentPercent, MaterialsJson  (todos en Residues)
--   Se mantienen: Quantity, MeasureUnit, Units, Price, Source,
--   Reference, ProductUse, ProductCategory
--   (datos de instancia de la línea de declaración).
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Products](
	[Id]                        [uniqueidentifier] NOT NULL,
	[IdProductDeclaration]      [uniqueidentifier] NOT NULL,
	-- Producto del catálogo maestro de residuos
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues (ResidueType='Product')
	-- Datos de instancia de la línea declarada
	[Reference]                 [nvarchar](512) NULL,
	[Source]                    [nvarchar](128) NULL,
	[ProductUse]                [nvarchar](128) NULL,
	[ProductCategory]           [nvarchar](256) NULL,
	[Quantity]                  [decimal](18,2) NULL,
	[MeasureUnit]               [nvarchar](64)  NULL,
	[Units]                     [int]           NULL,
	[Price]                     [decimal](18,0) NULL,
 CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- Tabla: ProductSpecs  (ficha técnica de producto)
-- CAMBIOS v3: IdResidue (FK -> Residues, ResidueType='ProductSpec')
--   Eliminados: ProducerName/NationalId (estaban en v1, en v2 ya
--   se consolidaron en IdProducer), ProductUse, ProductCategory,
--   CompositionJson, WeightPerUnitKg, ReparabilityIndex,
--   DisassemblyEase, ContainsHazardous, PotentialLERCodesJson
--   (todos pasan al catálogo Residues).
--   Se mantienen: ProductRef (clave de negocio), ProducerRef,
--   CategoryRef, Notes y campos de auditoría.
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductSpecs](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	-- Referencia única del producto en el sistema
	[ProductRef]                [nvarchar](128) NOT NULL,
	-- Ficha técnica completa en el catálogo maestro
	[IdResidue]                 [uniqueidentifier] NULL,   -- FK -> Residues (ResidueType='ProductSpec')
	-- Clasificadores propios de la especificación
	[ProductUse]                [int]           NULL,
	[ProductCategory]           [int]           NULL,
	[CategoryRef]               [nvarchar](64)  NULL,
	-- Productor vinculado
	[IdProducer]                [uniqueidentifier] NULL,   -- FK -> Entities (EntityRole='Producer')
	[ProducerRef]               [nvarchar](64)  NULL,
	[Notes]                     [nvarchar](512) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_ProductSpecs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- NUEVA TABLA MAESTRA: TreatmentOperations  (introducida en v4)
--
-- Catálogo de operaciones de tratamiento de residuos según
-- la nomenclatura establecida en la Directiva Marco de Residuos
-- (2008/98/CE) y su transposición nacional (Ley 7/2022):
--   Operaciones de valorización: R1–R13
--   Operaciones de eliminación:  D1–D15
-- Se usa en:
--   TreatmentPlants.IdTreatmentOperation   (operación realizada en planta)
--   WasteMoveResidues.IdTreatmentOperationDestiny (operación prevista en destino)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TreatmentOperations](
	[Id]                        [uniqueidentifier] NOT NULL  CONSTRAINT [DF_TreatmentOperations_Id] DEFAULT (NEWSEQUENTIALID()),
	-- Código oficial: R1-R13 (valorización) o D1-D15 (eliminación)
	[Code]                      [nvarchar](8)   NOT NULL,
	-- Tipo de operación: 'Recovery' (valorización) | 'Disposal' (eliminación)
	[OperationType]             [nvarchar](32)  NOT NULL,
	-- Descripción oficial según directiva
	[Description]               [nvarchar](512) NOT NULL,
	-- Descripción abreviada para interfaces
	[ShortDescription]          [nvarchar](128) NULL,
	-- ¿Considera reciclaje? (útil para cálculo de KPIs de reciclado)
	[IsRecycling]               [bit]           NOT NULL CONSTRAINT [DF_TreatmentOperations_IsRecycling] DEFAULT (0),
	-- ¿Considera valorización energética?
	[IsEnergyRecovery]          [bit]           NOT NULL CONSTRAINT [DF_TreatmentOperations_IsEnergyRecovery] DEFAULT (0),
	-- ¿Es operación de reutilización preparatoria?
	[IsPreparationForReuse]     [bit]           NOT NULL CONSTRAINT [DF_TreatmentOperations_IsPreparationForReuse] DEFAULT (0),
	-- Orden de visualización
	[SortOrder]                 [int]           NULL,
	-- Estado del código (permite deprecar si normativa cambia)
	[IsActive]                  [bit]           NOT NULL CONSTRAINT [DF_TreatmentOperations_IsActive] DEFAULT (1),
	[CreatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_TreatmentOperations_CreatedAt] DEFAULT (SYSUTCDATETIME()),
	[UpdatedAt]                 [datetime2](0)  NOT NULL CONSTRAINT [DF_TreatmentOperations_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
 CONSTRAINT [PK_TreatmentOperations]      PRIMARY KEY CLUSTERED  ([Id] ASC)   WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_TreatmentOperations_Code] UNIQUE NONCLUSTERED    ([Code] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TreatmentOperations_OperationType] ON [dbo].[TreatmentOperations] ([OperationType] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

-- ============================================================
-- Resto de tablas sin cambios en residuos
-- ============================================================

/****** Object:  Table [dbo].[DUMZones] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DUMZones](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[ZoneCode]                  [nvarchar](64)  NOT NULL,
	[Name]                      [nvarchar](256) NOT NULL,
	[Description]               [nvarchar](512) NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[GeometryJson]              [nvarchar](max) NOT NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_DUMZones] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[DUMRestrictionRules] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DUMRestrictionRules](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[RuleCode]                  [nvarchar](64)  NOT NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[ZoneId]                    [uniqueidentifier] NOT NULL,
	[ValidFrom]                 [datetime2](0)  NOT NULL,
	[ValidTo]                   [datetime2](0)  NULL,
	[ConditionsJson]            [nvarchar](max) NOT NULL,
	[ActionType]                [nvarchar](32)  NOT NULL,
	[ActionReason]              [nvarchar](256) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
 CONSTRAINT [PK_DUMRestrictionRules] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- ============================================================
-- Tabla: EmissionFactorSets
-- Conjuntos versionados de factores de emisión. Define el
-- publisher, metodología y ventana de validez del conjunto.
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmissionFactorSets](
	[Id]                [uniqueidentifier] NOT NULL  CONSTRAINT [DF_EmissionFactorSets_Id] DEFAULT (NEWSEQUENTIALID()),
	[OwnerId]           [uniqueidentifier] NULL,
	[FactorSetName]     [nvarchar](128)    NOT NULL,
	[Version]           [nvarchar](32)     NOT NULL,
	[Status]            [nvarchar](32)     NOT NULL  CONSTRAINT [DF_EmissionFactorSets_Status]    DEFAULT ('Active'),
	[ValidFrom]         [datetime2](0)     NOT NULL,
	[ValidTo]           [datetime2](0)     NULL,
	[Publisher]         [nvarchar](256)    NULL,
	[Reference]         [nvarchar](128)    NULL,
	[Methodology]       [nvarchar](256)    NULL,
	[SourceSystem]      [nvarchar](64)     NULL,
	[Hash]              [nvarchar](128)    NULL,
	[CreatedAt]         [datetime2](0)     NOT NULL  CONSTRAINT [DF_EmissionFactorSets_CreatedAt]  DEFAULT (SYSUTCDATETIME()),
	[UpdatedAt]         [datetime2](0)     NOT NULL  CONSTRAINT [DF_EmissionFactorSets_UpdatedAt]  DEFAULT (SYSUTCDATETIME()),
	[IdUser]            [int]              NOT NULL,
 CONSTRAINT [PK_EmissionFactorSets] PRIMARY KEY CLUSTERED ([Id] ASC)
 WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_EmissionFactorSets_NameVersion] ON [dbo].[EmissionFactorSets]
(
	[FactorSetName] ASC,
	[Version] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[EmissionFactors] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmissionFactors](
	[Id]                        [uniqueidentifier] NOT NULL,
	[FactorSetId]               [uniqueidentifier] NOT NULL,
	[VehicleType]               [nvarchar](32)  NOT NULL,
	[FuelType]                  [nvarchar](32)  NOT NULL,
	[EuroClass]                 [nvarchar](16)  NULL,
	[Unit]                      [nvarchar](32)  NOT NULL,
	[Value]                     [decimal](18,6) NOT NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
 CONSTRAINT [PK_EmissionFactors] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[EcoModulationRuleSets] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcoModulationRuleSets](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[RuleSetName]               [nvarchar](128) NOT NULL,
	[Version]                   [nvarchar](32)  NOT NULL,
	[Status]                    [nvarchar](32)  NOT NULL,
	[ValidFrom]                 [datetime2](0)  NOT NULL,
	[ValidTo]                   [datetime2](0)  NULL,
	[PublisherName]             [nvarchar](256) NULL,
	[PublisherNationalId]       [nvarchar](32)  NULL,
	[PublisherCenterCode]       [nvarchar](64)  NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_EcoModulationRuleSets] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[EcoModulationRules] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EcoModulationRules](
	[Id]                        [uniqueidentifier] NOT NULL,
	[RuleSetId]                 [uniqueidentifier] NOT NULL,
	[RuleCode]                  [nvarchar](64)  NOT NULL,
	[ProductCategory]           [int]           NULL,
	[CriteriaJson]              [nvarchar](max) NOT NULL,
	[FeeImpactType]             [nvarchar](32)  NOT NULL,
	[FeeImpactValue]            [decimal](18,6) NOT NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
 CONSTRAINT [PK_EcoModulationRules] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[PlantEnergies] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PlantEnergies](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[PlantName]                 [nvarchar](256) NOT NULL,
	[PlantCenterCode]           [nvarchar](64)  NULL,
	[Year]                      [int]           NOT NULL,
	[Month]                     [int]           NULL,
	[KwhTotal]                  [decimal](18,3) NOT NULL,
	[Source]                    [nvarchar](64)  NULL,
	[GridMixRef]                [nvarchar](64)  NULL,
	[AllocationMethod]          [nvarchar](64)  NULL,
	[Notes]                     [nvarchar](512) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_PlantEnergies] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Incidents] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Incidents](
	[Id]                        [uniqueidentifier] NOT NULL,
	[OwnerId]                   [uniqueidentifier] NULL,
	[Type]                      [nvarchar](64)  NOT NULL,
	[Severity]                  [nvarchar](16)  NOT NULL,
	[OpenedAt]                  [datetime2](0)  NOT NULL,
	[ClosedAt]                  [datetime2](0)  NULL,
	[ServiceOrderId]            [uniqueidentifier] NULL,
	[WasteMoveReference]        [nvarchar](128) NULL,
	[TicketScale]               [nvarchar](128) NULL,
	[ReportedByName]            [nvarchar](256) NULL,
	[ReportedByNationalId]      [nvarchar](32)  NULL,
	[ReportedByCenterCode]      [nvarchar](64)  NULL,
	[Description]               [nvarchar](max) NULL,
	[ResolutionJson]            [nvarchar](max) NULL,
	[SourceSystem]              [nvarchar](64)  NULL,
	[Version]                   [int]           NOT NULL,
	[Hash]                      [nvarchar](128) NULL,
	[CreatedAt]                 [datetime2](0)  NOT NULL,
	[UpdatedAt]                 [datetime2](0)  NOT NULL,
	[IdUser]                    [int]           NOT NULL,
 CONSTRAINT [PK_Incidents] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Tablas geográficas y de diccionario (sin cambios) ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Country](
	[id]   [int] IDENTITY(1,1) NOT NULL,
	[Ref]  [varchar](64) NOT NULL,
	[Code] [varchar](2)  NOT NULL,
	[ISONUM]   [int]    NULL,
	[CODE_ISO3] [char](3) NULL,
	[MunicipalityDataLinkedRequired] [bit] NOT NULL,
	[MunicipalityDataRequired]       [bit] NOT NULL,
	[UE]   [bit] NOT NULL,
 CONSTRAINT [PK_Country] PRIMARY KEY CLUSTERED ([id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_Country]  UNIQUE NONCLUSTERED  ([Code] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[TerritoryState](
	[id]        [int] IDENTITY(1,1) NOT NULL,
	[idCountry] [int]          NOT NULL,
	[Ref]       [varchar](64)  NOT NULL,
	[Code]      [varchar](2)   NOT NULL,
	[Name]      [varchar](128) NULL,
 CONSTRAINT [PK_TerritoryState] PRIMARY KEY CLUSTERED ([id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_TerritoryState] UNIQUE NONCLUSTERED ([idCountry] ASC, [Code] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[Province](
	[id]      [int] IDENTITY(1,1) NOT NULL,
	[idState] [int]         NOT NULL,
	[Ref]     [varchar](64) NOT NULL,
	[Code]    [varchar](2)  NOT NULL,
	[Name]    [varchar](64) NULL,
 CONSTRAINT [PK_Province] PRIMARY KEY CLUSTERED ([id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[Municipality](
	[Id]                [int] IDENTITY(1,1) NOT NULL,
	[Id_Province]       [int]          NOT NULL,
	[Code]              [varchar](6)   NOT NULL,
	[Name]              [varchar](256) NOT NULL,
	[CodeControlNumber] [varchar](1)   NULL,
 CONSTRAINT [PK_Municipality] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[MunicipalityPopulation](
	[Id]               [int] IDENTITY(1,1) NOT NULL,
	[IdMunicipality]   [int] NOT NULL,
	[TotalPopulation]  [int] NULL,
	[MalePopulation]   [int] NULL,
	[FemalePopulation] [int] NULL,
 CONSTRAINT [PK_MunicipalityPopulation] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[MunicipalityZipCode](
	[Id]             [int] IDENTITY(1,1) NOT NULL,
	[IdMunicipality] [int]         NOT NULL,
	[ZipCode]        [varchar](5)  NOT NULL,
	[ription]        [varchar](256) NULL,
 CONSTRAINT [PK_MunicipalityZipCode] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationCategory](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_dicProductDeclarationCategory] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationPeriods](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_dicProductDeclarationPeriods] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationProducts](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL, [CategoryId] [int] NULL,
 CONSTRAINT [PK_dicProductDeclarationProducts] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationSource](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_dicProductDeclarationSource] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationType](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_dicProductDeclarationType] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[dicProductDeclarationUse](
	[Id] [int] NOT NULL, [Ref] [varchar](128) NOT NULL, [description] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_dicProductDeclarationUse] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[DocStates](
	[id] [int] NOT NULL, [id_ref] [varchar](64) NOT NULL, [name] [varchar](128) NULL,
 CONSTRAINT [PK_DocStates]  PRIMARY KEY CLUSTERED  ([id] ASC)     WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX_DocStates]  UNIQUE NONCLUSTERED    ([id_ref] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[Profiles](
	[ID] [int] IDENTITY(1,1) NOT NULL, [Reference] [nvarchar](256) NOT NULL, [Description] [varchar](255) NULL, [CreateDate] [datetime] NULL,
 CONSTRAINT [PK__Profiles] PRIMARY KEY NONCLUSTERED ([ID] ASC)        WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX__Profiles] UNIQUE NONCLUSTERED      ([Reference] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[Users](
	[ID]            [int] IDENTITY(1,1) NOT NULL,
	[Login]         [nvarchar](256) NOT NULL,
	[CompleteName]  [varchar](255)  NULL,
	[Email]         [nvarchar](256) NULL,
	[CreateDate]    [datetime]      NULL,
	[IdProfile]     [int]           NOT NULL,
	[NationalId]    [int]           NULL,
	[GeographicalId][int]           NULL,
	[ZipCode]       [varchar](64)   NULL,
	[MunicipalityId][int]           NULL,
	[Address]       [nvarchar](max) NULL,
	[OwnerId]       [uniqueidentifier] NULL,
	[PortalEDCProvider] [nvarchar](max) NULL,
	[PortalEDCConsumer] [nvarchar](max) NULL,
 CONSTRAINT [PK__Users] PRIMARY KEY NONCLUSTERED ([ID] ASC)      WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [IX__Users] UNIQUE NONCLUSTERED      ([Login] ASC)   WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
CREATE TABLE [dbo].[UserSharePointCredentials](
	[ID]           [int] IDENTITY(1,1) NOT NULL,
	[UserID]       [int]           NOT NULL,
	[TenantId]     [nvarchar](256) NOT NULL,
	[ClientId]     [nvarchar](256) NOT NULL,
	[ClientSecret] [nvarchar](512) NOT NULL,
	[IsActive]     [bit]           NOT NULL,
 CONSTRAINT [PK_UserSharePointCredentials]        PRIMARY KEY CLUSTERED ([ID]     ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_UserSharePointCredentials_UserID] UNIQUE NONCLUSTERED   ([UserID] ASC) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

-- ============================================================
-- ÍNDICES
-- ============================================================
CREATE NONCLUSTERED INDEX [IX_AgreementDocuments_AgreementId]    ON [dbo].[AgreementDocuments] ([AgreementId] ASC)    WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Agreements_Number]          ON [dbo].[Agreements]         ([AgreementNumber] ASC) WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Agreements_IdScrap]                ON [dbo].[Agreements]         ([IdScrap] ASC)        WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_DUMRestrictionRules_ZoneId]        ON [dbo].[DUMRestrictionRules]([ZoneId] ASC)         WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DUMRestrictionRules_Code]   ON [dbo].[DUMRestrictionRules]([RuleCode] ASC)       WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DUMZones_Code]              ON [dbo].[DUMZones]           ([ZoneCode] ASC)       WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_EcoModulationRules_Code]    ON [dbo].[EcoModulationRules] ([RuleSetId] ASC, [RuleCode] ASC) WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_EcoModulationRuleSets_NameVersion] ON [dbo].[EcoModulationRuleSets] ([RuleSetName] ASC, [Version] ASC) WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EmissionFactors_FactorSetId]       ON [dbo].[EmissionFactors]    ([FactorSetId] ASC)    WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EmissionFactors_Lookup]            ON [dbo].[EmissionFactors]    ([FactorSetId] ASC, [VehicleType] ASC, [FuelType] ASC, [EuroClass] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EntryPlants_ServiceOrderId]        ON [dbo].[EntryPlants]        ([ServiceOrderId] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Incidents_ServiceOrderId]          ON [dbo].[Incidents]          ([ServiceOrderId] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Incidents_WasteMoveReference]      ON [dbo].[Incidents]          ([WasteMoveReference] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductSpecs_ProductRef]    ON [dbo].[ProductSpecs]       ([ProductRef] ASC)     WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ProductSpecs_IdResidue]            ON [dbo].[ProductSpecs]       ([IdResidue] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ServiceOrders_WasteMoveReference]  ON [dbo].[ServiceOrders]      ([WasteMoveReference] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ServiceOrders_Number]       ON [dbo].[ServiceOrders]      ([ServiceOrderNumber] ASC) WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ServiceOrders_IdCarrier]           ON [dbo].[ServiceOrders]      ([IdCarrier] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ServiceOrders_IdPlannedPlant]      ON [dbo].[ServiceOrders]      ([IdPlannedPlant] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_ServiceOrders_IdPickupPoint]       ON [dbo].[ServiceOrders]      ([IdPickupPoint] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_SettlementLines_SettlementId]      ON [dbo].[SettlementLines]    ([SettlementId] ASC)   WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_SettlementLines_IdLERCode]         ON [dbo].[SettlementLines]    ([IdLERCode] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Settlements_Agreement_Period]      ON [dbo].[Settlements]        ([AgreementId] ASC, [Year] ASC, [Month] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Settlements_Number]         ON [dbo].[Settlements]        ([SettlementNumber] ASC) WITH (IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TreatmentPlants_ServiceOrderId]    ON [dbo].[TreatmentPlants]    ([ServiceOrderId] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TreatmentPlants_IdTreatmentOp]    ON [dbo].[TreatmentPlants]    ([IdTreatmentOperation] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_UserSharePointCredentials_UserID]  ON [dbo].[UserSharePointCredentials] ([UserID] ASC)
WHERE ([IsActive]=(1)) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_ServiceOrderId]         ON [dbo].[WasteMoves]         ([ServiceOrderId] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_WasteMoveReference]     ON [dbo].[WasteMoves]         ([WasteMoveReference] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_IdSource]               ON [dbo].[WasteMoves]         ([IdSource] ASC)       WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_IdDestination]          ON [dbo].[WasteMoves]         ([IdDestination] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_IdScrap]                ON [dbo].[WasteMoves]         ([IdScrap] ASC)        WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoves_IdOperatorTransfer]     ON [dbo].[WasteMoves]         ([IdOperatorTransfer] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
-- Índices sobre IdResidue en tablas operativas
CREATE NONCLUSTERED INDEX [IX_WasteMoveResidues_IdResidue]       ON [dbo].[WasteMoveResidues]  ([IdResidue] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_WasteMoveResidues_IdTreatmentOp]  ON [dbo].[WasteMoveResidues]  ([IdTreatmentOperationDestiny] ASC) WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EntryPlantResidues_IdResidue]      ON [dbo].[EntryPlantResidues] ([IdResidue] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_EntryCACResidues_IdResidue]        ON [dbo].[EntryCACResidues]   ([IdResidue] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TreatmentPlantResidues_IdResidue]  ON [dbo].[TreatmentPlantResidues] ([IdResidue] ASC)  WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_Products_IdResidue]                ON [dbo].[Products]           ([IdResidue] ASC)      WITH (DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO

-- ============================================================
-- DEFAULTS
-- ============================================================
ALTER TABLE [dbo].[Agreements]    ADD CONSTRAINT [DF_Agreements_Version]   DEFAULT ((1))              FOR [Version]
GO
ALTER TABLE [dbo].[Agreements]    ADD CONSTRAINT [DF_Agreements_CreatedAt] DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Agreements]    ADD CONSTRAINT [DF_Agreements_UpdatedAt] DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Country]       ADD DEFAULT ((0)) FOR [MunicipalityDataLinkedRequired]
GO
ALTER TABLE [dbo].[Country]       ADD DEFAULT ((0)) FOR [MunicipalityDataRequired]
GO
ALTER TABLE [dbo].[Country]       ADD CONSTRAINT [DF_Country_UE]                    DEFAULT ((0))              FOR [UE]
GO
ALTER TABLE [dbo].[DUMRestrictionRules] ADD CONSTRAINT [DF_DUMRestrictionRules_Status]    DEFAULT ('Active')           FOR [Status]
GO
ALTER TABLE [dbo].[DUMRestrictionRules] ADD CONSTRAINT [DF_DUMRestrictionRules_Version]   DEFAULT ((1))               FOR [Version]
GO
ALTER TABLE [dbo].[DUMRestrictionRules] ADD CONSTRAINT [DF_DUMRestrictionRules_CreatedAt] DEFAULT (SYSUTCDATETIME())  FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DUMRestrictionRules] ADD CONSTRAINT [DF_DUMRestrictionRules_UpdatedAt] DEFAULT (SYSUTCDATETIME())  FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[DUMZones]      ADD CONSTRAINT [DF_DUMZones_Status]    DEFAULT ('Active')          FOR [Status]
GO
ALTER TABLE [dbo].[DUMZones]      ADD CONSTRAINT [DF_DUMZones_Version]   DEFAULT ((1))               FOR [Version]
GO
ALTER TABLE [dbo].[DUMZones]      ADD CONSTRAINT [DF_DUMZones_CreatedAt] DEFAULT (SYSUTCDATETIME())  FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DUMZones]      ADD CONSTRAINT [DF_DUMZones_UpdatedAt] DEFAULT (SYSUTCDATETIME())  FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[EcoModulationRules]    ADD CONSTRAINT [DF_EcoModulationRules_CreatedAt]    DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EcoModulationRuleSets] ADD CONSTRAINT [DF_EcoModulationRuleSets_Status]    DEFAULT ('Active')          FOR [Status]
GO
ALTER TABLE [dbo].[EcoModulationRuleSets] ADD CONSTRAINT [DF_EcoModulationRuleSets_CreatedAt] DEFAULT (SYSUTCDATETIME())  FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EcoModulationRuleSets] ADD CONSTRAINT [DF_EcoModulationRuleSets_UpdatedAt] DEFAULT (SYSUTCDATETIME())  FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[EmissionFactors]       ADD CONSTRAINT [DF_EmissionFactors_CreatedAt]       DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Incidents]     ADD CONSTRAINT [DF_Incidents_Version]   DEFAULT ((1))              FOR [Version]
GO
ALTER TABLE [dbo].[Incidents]     ADD CONSTRAINT [DF_Incidents_CreatedAt] DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Incidents]     ADD CONSTRAINT [DF_Incidents_UpdatedAt] DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[MarketShares]  ADD CONSTRAINT [DF_MarketShares_Version] DEFAULT ((1)) FOR [Version]
GO
ALTER TABLE [dbo].[PlantEnergies] ADD CONSTRAINT [DF_PlantEnergies_Kwh]       DEFAULT ((0))              FOR [KwhTotal]
GO
ALTER TABLE [dbo].[PlantEnergies] ADD CONSTRAINT [DF_PlantEnergies_Version]   DEFAULT ((1))              FOR [Version]
GO
ALTER TABLE [dbo].[PlantEnergies] ADD CONSTRAINT [DF_PlantEnergies_CreatedAt] DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PlantEnergies] ADD CONSTRAINT [DF_PlantEnergies_UpdatedAt] DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[ProductSpecs]  ADD CONSTRAINT [DF_ProductSpecs_Version]   DEFAULT ((1))              FOR [Version]
GO
ALTER TABLE [dbo].[ProductSpecs]  ADD CONSTRAINT [DF_ProductSpecs_CreatedAt] DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ProductSpecs]  ADD CONSTRAINT [DF_ProductSpecs_UpdatedAt] DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[ServiceOrders] ADD CONSTRAINT [DF_ServiceOrders_Priority]  DEFAULT ('Normal')         FOR [Priority]
GO
ALTER TABLE [dbo].[ServiceOrders] ADD CONSTRAINT [DF_ServiceOrders_Version]   DEFAULT ((1))              FOR [Version]
GO
ALTER TABLE [dbo].[ServiceOrders] ADD CONSTRAINT [DF_ServiceOrders_CreatedAt] DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ServiceOrders] ADD CONSTRAINT [DF_ServiceOrders_UpdatedAt] DEFAULT (SYSUTCDATETIME()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[SettlementLines] ADD CONSTRAINT [DF_SettlementLines_Weight]  DEFAULT ((0)) FOR [WeightKg]
GO
ALTER TABLE [dbo].[SettlementLines] ADD CONSTRAINT [DF_SettlementLines_Price]   DEFAULT ((0)) FOR [PricePerKg]
GO
ALTER TABLE [dbo].[SettlementLines] ADD CONSTRAINT [DF_SettlementLines_Amount]  DEFAULT ((0)) FOR [Amount]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_Currency]      DEFAULT ('EUR') FOR [Currency]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_BaseAmount]    DEFAULT ((0))   FOR [BaseAmount]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_Adjustments]   DEFAULT ((0))   FOR [AdjustmentsAmount]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_Tax]           DEFAULT ((0))   FOR [TaxAmount]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_Total]         DEFAULT ((0))   FOR [TotalAmount]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_Version]       DEFAULT ((1))               FOR [Version]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_CreatedAt]     DEFAULT (SYSUTCDATETIME())  FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Settlements]   ADD CONSTRAINT [DF_Settlements_UpdatedAt]     DEFAULT (SYSUTCDATETIME())  FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[UserSharePointCredentials] ADD CONSTRAINT [DF_UserSharePointCredentials_IsActive] DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[WasteMoves]    ADD CONSTRAINT [DF_WasteMoves_Version] DEFAULT ((1)) FOR [Version]
GO

-- ============================================================
-- FOREIGN KEYS
-- ============================================================

-- Residues -> LERCodes
ALTER TABLE [dbo].[Residues] WITH CHECK
  ADD CONSTRAINT [FK_Residues_LERCodes] FOREIGN KEY([IdLERCode]) REFERENCES [dbo].[LERCodes] ([Id])
GO
ALTER TABLE [dbo].[Residues] CHECK CONSTRAINT [FK_Residues_LERCodes]
GO

-- Residues -> Entities (Producer)
ALTER TABLE [dbo].[Residues] WITH CHECK
  ADD CONSTRAINT [FK_Residues_Entities_Producer] FOREIGN KEY([IdProducer]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Residues] CHECK CONSTRAINT [FK_Residues_Entities_Producer]
GO

-- AgreementDocuments -> Agreements
ALTER TABLE [dbo].[AgreementDocuments] WITH CHECK
  ADD CONSTRAINT [FK_AgreementDocuments_Agreements] FOREIGN KEY([AgreementId]) REFERENCES [dbo].[Agreements] ([Id])
GO
ALTER TABLE [dbo].[AgreementDocuments] CHECK CONSTRAINT [FK_AgreementDocuments_Agreements]
GO

-- Agreements -> Entities
ALTER TABLE [dbo].[Agreements] WITH CHECK
  ADD CONSTRAINT [FK_Agreements_Entities_Scrap]        FOREIGN KEY([IdScrap])        REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Agreements] CHECK CONSTRAINT [FK_Agreements_Entities_Scrap]
GO
ALTER TABLE [dbo].[Agreements] WITH CHECK
  ADD CONSTRAINT [FK_Agreements_Entities_PublicEntity] FOREIGN KEY([IdPublicEntity]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Agreements] CHECK CONSTRAINT [FK_Agreements_Entities_PublicEntity]
GO
ALTER TABLE [dbo].[Agreements] WITH CHECK
  ADD CONSTRAINT [FK_Agreements_Entities_Coordinator]  FOREIGN KEY([IdCoordinator])  REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Agreements] CHECK CONSTRAINT [FK_Agreements_Entities_Coordinator]
GO

-- ServiceOrders -> Entities (Carrier, PlannedPlant, IssuedBy, PickupPoint)
ALTER TABLE [dbo].[ServiceOrders] WITH CHECK
  ADD CONSTRAINT [FK_ServiceOrders_Entities_Carrier]      FOREIGN KEY([IdCarrier])      REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ServiceOrders] CHECK CONSTRAINT [FK_ServiceOrders_Entities_Carrier]
GO
ALTER TABLE [dbo].[ServiceOrders] WITH CHECK
  ADD CONSTRAINT [FK_ServiceOrders_Entities_PlannedPlant] FOREIGN KEY([IdPlannedPlant]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ServiceOrders] CHECK CONSTRAINT [FK_ServiceOrders_Entities_PlannedPlant]
GO
ALTER TABLE [dbo].[ServiceOrders] WITH CHECK
  ADD CONSTRAINT [FK_ServiceOrders_Entities_IssuedBy]     FOREIGN KEY([IdIssuedBy])     REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ServiceOrders] CHECK CONSTRAINT [FK_ServiceOrders_Entities_IssuedBy]
GO
ALTER TABLE [dbo].[ServiceOrders] WITH CHECK
  ADD CONSTRAINT [FK_ServiceOrders_Entities_PickupPoint]  FOREIGN KEY([IdPickupPoint])  REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ServiceOrders] CHECK CONSTRAINT [FK_ServiceOrders_Entities_PickupPoint]
GO

-- ServiceOrders -> LERCodes
ALTER TABLE [dbo].[ServiceOrders] WITH CHECK
  ADD CONSTRAINT [FK_ServiceOrders_LERCodes] FOREIGN KEY([IdLERCode]) REFERENCES [dbo].[LERCodes] ([Id])
GO
ALTER TABLE [dbo].[ServiceOrders] CHECK CONSTRAINT [FK_ServiceOrders_LERCodes]
GO

-- Settlements -> Agreements
ALTER TABLE [dbo].[Settlements] WITH CHECK
  ADD CONSTRAINT [FK_Settlements_Agreements] FOREIGN KEY([AgreementId]) REFERENCES [dbo].[Agreements] ([Id])
GO
ALTER TABLE [dbo].[Settlements] CHECK CONSTRAINT [FK_Settlements_Agreements]
GO

-- Settlements -> Entities
ALTER TABLE [dbo].[Settlements] WITH CHECK
  ADD CONSTRAINT [FK_Settlements_Entities_Scrap]        FOREIGN KEY([IdScrap])        REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Settlements] CHECK CONSTRAINT [FK_Settlements_Entities_Scrap]
GO
ALTER TABLE [dbo].[Settlements] WITH CHECK
  ADD CONSTRAINT [FK_Settlements_Entities_PublicEntity] FOREIGN KEY([IdPublicEntity]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[Settlements] CHECK CONSTRAINT [FK_Settlements_Entities_PublicEntity]
GO

-- SettlementLines -> Settlements
ALTER TABLE [dbo].[SettlementLines] WITH CHECK
  ADD CONSTRAINT [FK_SettlementLines_Settlements] FOREIGN KEY([SettlementId]) REFERENCES [dbo].[Settlements] ([Id])
GO
ALTER TABLE [dbo].[SettlementLines] CHECK CONSTRAINT [FK_SettlementLines_Settlements]
GO

-- SettlementLines -> LERCodes
ALTER TABLE [dbo].[SettlementLines] WITH CHECK
  ADD CONSTRAINT [FK_SettlementLines_LERCodes] FOREIGN KEY([IdLERCode]) REFERENCES [dbo].[LERCodes] ([Id])
GO
ALTER TABLE [dbo].[SettlementLines] CHECK CONSTRAINT [FK_SettlementLines_LERCodes]
GO

-- MarketShares -> Entities
ALTER TABLE [dbo].[MarketShares] WITH CHECK
  ADD CONSTRAINT [FK_MarketShares_Entities_Scrap] FOREIGN KEY([IdScrap]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[MarketShares] CHECK CONSTRAINT [FK_MarketShares_Entities_Scrap]
GO

-- WasteMoves -> ServiceOrders
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_ServiceOrders] FOREIGN KEY([ServiceOrderId]) REFERENCES [dbo].[ServiceOrders] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_ServiceOrders]
GO

-- WasteMoves -> Entities
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_Entities_Source]           FOREIGN KEY([IdSource])           REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_Entities_Source]
GO
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_Entities_Destination]      FOREIGN KEY([IdDestination])      REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_Entities_Destination]
GO
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_Entities_Scrap]            FOREIGN KEY([IdScrap])            REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_Entities_Scrap]
GO
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_Entities_Scrap2]           FOREIGN KEY([IdScrap2])           REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_Entities_Scrap2]
GO
ALTER TABLE [dbo].[WasteMoves] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoves_Entities_OperatorTransfer] FOREIGN KEY([IdOperatorTransfer]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoves] CHECK CONSTRAINT [FK_WasteMoves_Entities_OperatorTransfer]
GO

-- WasteMoveResidues -> WasteMoves
ALTER TABLE [dbo].[WasteMoveResidues] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoveResidues_WasteMoves] FOREIGN KEY([IdWasteMove]) REFERENCES [dbo].[WasteMoves] ([Id])
GO
ALTER TABLE [dbo].[WasteMoveResidues] CHECK CONSTRAINT [FK_WasteMoveResidues_WasteMoves]
GO

-- WasteMoveResidues -> Residues
ALTER TABLE [dbo].[WasteMoveResidues] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoveResidues_Residues] FOREIGN KEY([IdResidue]) REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[WasteMoveResidues] CHECK CONSTRAINT [FK_WasteMoveResidues_Residues]
GO

-- WasteMoveResidues -> TreatmentOperations (operación prevista en destino)
ALTER TABLE [dbo].[WasteMoveResidues] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoveResidues_TreatmentOperations] FOREIGN KEY([IdTreatmentOperationDestiny]) REFERENCES [dbo].[TreatmentOperations] ([Id])
GO
ALTER TABLE [dbo].[WasteMoveResidues] CHECK CONSTRAINT [FK_WasteMoveResidues_TreatmentOperations]
GO

-- WasteMoveResidues -> Entities (Carrier)
ALTER TABLE [dbo].[WasteMoveResidues] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoveResidues_Entities_Carrier] FOREIGN KEY([IdCarrier]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[WasteMoveResidues] CHECK CONSTRAINT [FK_WasteMoveResidues_Entities_Carrier]
GO

-- WasteMoveResidues -> EmissionFactorSets
ALTER TABLE [dbo].[WasteMoveResidues] WITH CHECK
  ADD CONSTRAINT [FK_WasteMoveResidues_EmissionFactorSets] FOREIGN KEY([EmissionFactorSetId]) REFERENCES [dbo].[EmissionFactorSets] ([Id])
GO
ALTER TABLE [dbo].[WasteMoveResidues] CHECK CONSTRAINT [FK_WasteMoveResidues_EmissionFactorSets]
GO

-- EntryPlants -> ServiceOrders
ALTER TABLE [dbo].[EntryPlants] WITH CHECK
  ADD CONSTRAINT [FK_EntryPlants_ServiceOrders] FOREIGN KEY([ServiceOrderId]) REFERENCES [dbo].[ServiceOrders] ([Id])
GO
ALTER TABLE [dbo].[EntryPlants] CHECK CONSTRAINT [FK_EntryPlants_ServiceOrders]
GO

-- EntryPlantResidues -> EntryPlants
ALTER TABLE [dbo].[EntryPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_EntryPlantResidues_EntryPlants] FOREIGN KEY([IdEntryPlant]) REFERENCES [dbo].[EntryPlants] ([Id])
GO
ALTER TABLE [dbo].[EntryPlantResidues] CHECK CONSTRAINT [FK_EntryPlantResidues_EntryPlants]
GO

-- EntryPlantResidues -> Residues
ALTER TABLE [dbo].[EntryPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_EntryPlantResidues_Residues] FOREIGN KEY([IdResidue]) REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[EntryPlantResidues] CHECK CONSTRAINT [FK_EntryPlantResidues_Residues]
GO

-- TreatmentPlants -> ServiceOrders
ALTER TABLE [dbo].[TreatmentPlants] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlants_ServiceOrders] FOREIGN KEY([ServiceOrderId]) REFERENCES [dbo].[ServiceOrders] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlants] CHECK CONSTRAINT [FK_TreatmentPlants_ServiceOrders]
GO

-- TreatmentPlants -> TreatmentOperations (operación realizada en planta)
ALTER TABLE [dbo].[TreatmentPlants] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlants_TreatmentOperations] FOREIGN KEY([IdTreatmentOperation]) REFERENCES [dbo].[TreatmentOperations] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlants] CHECK CONSTRAINT [FK_TreatmentPlants_TreatmentOperations]
GO

-- TreatmentPlants -> Incidents
ALTER TABLE [dbo].[TreatmentPlants] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlants_Incidents] FOREIGN KEY([IncidentId]) REFERENCES [dbo].[Incidents] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlants] CHECK CONSTRAINT [FK_TreatmentPlants_Incidents]
GO

-- TreatmentPlantResidues -> TreatmentPlants
ALTER TABLE [dbo].[TreatmentPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlantResidues_TreatmentPlants] FOREIGN KEY([IdTreatmentPlant]) REFERENCES [dbo].[TreatmentPlants] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] CHECK CONSTRAINT [FK_TreatmentPlantResidues_TreatmentPlants]
GO

-- TreatmentPlantResidues -> Residues (4 flujos)
ALTER TABLE [dbo].[TreatmentPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlantResidues_Residues]        FOREIGN KEY([IdResidue])        REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] CHECK CONSTRAINT [FK_TreatmentPlantResidues_Residues]
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlantResidues_ResiduesReused]  FOREIGN KEY([IdResidueReused])  REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] CHECK CONSTRAINT [FK_TreatmentPlantResidues_ResiduesReused]
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlantResidues_ResiduesValued]  FOREIGN KEY([IdResidueValued])  REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] CHECK CONSTRAINT [FK_TreatmentPlantResidues_ResiduesValued]
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] WITH CHECK
  ADD CONSTRAINT [FK_TreatmentPlantResidues_ResiduesRemove]  FOREIGN KEY([IdResidueRemove])  REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[TreatmentPlantResidues] CHECK CONSTRAINT [FK_TreatmentPlantResidues_ResiduesRemove]
GO

-- EntryCACResidues -> EntryCACs
ALTER TABLE [dbo].[EntryCACResidues] WITH CHECK
  ADD CONSTRAINT [FK_EntryCACResidues_EntryCACs] FOREIGN KEY([IdEntryCAC]) REFERENCES [dbo].[EntryCACs] ([Id])
GO
ALTER TABLE [dbo].[EntryCACResidues] CHECK CONSTRAINT [FK_EntryCACResidues_EntryCACs]
GO

-- EntryCACResidues -> Residues
ALTER TABLE [dbo].[EntryCACResidues] WITH CHECK
  ADD CONSTRAINT [FK_EntryCACResidues_Residues] FOREIGN KEY([IdResidue]) REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[EntryCACResidues] CHECK CONSTRAINT [FK_EntryCACResidues_Residues]
GO

-- ProductDeclaration -> Entities (Producer)
ALTER TABLE [dbo].[ProductDeclaration] WITH CHECK
  ADD CONSTRAINT [FK_ProductDeclaration_Entities_Producer] FOREIGN KEY([IdProducer]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ProductDeclaration] CHECK CONSTRAINT [FK_ProductDeclaration_Entities_Producer]
GO

-- Products -> ProductDeclaration
ALTER TABLE [dbo].[Products] WITH CHECK
  ADD CONSTRAINT [FK_Products_ProductDeclaration] FOREIGN KEY([IdProductDeclaration]) REFERENCES [dbo].[ProductDeclaration] ([Id])
GO
ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [FK_Products_ProductDeclaration]
GO

-- Products -> Residues
ALTER TABLE [dbo].[Products] WITH CHECK
  ADD CONSTRAINT [FK_Products_Residues] FOREIGN KEY([IdResidue]) REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[Products] CHECK CONSTRAINT [FK_Products_Residues]
GO

-- ProductSpecs -> Residues
ALTER TABLE [dbo].[ProductSpecs] WITH CHECK
  ADD CONSTRAINT [FK_ProductSpecs_Residues] FOREIGN KEY([IdResidue]) REFERENCES [dbo].[Residues] ([Id])
GO
ALTER TABLE [dbo].[ProductSpecs] CHECK CONSTRAINT [FK_ProductSpecs_Residues]
GO

-- ProductSpecs -> Entities (Producer)
ALTER TABLE [dbo].[ProductSpecs] WITH CHECK
  ADD CONSTRAINT [FK_ProductSpecs_Entities_Producer] FOREIGN KEY([IdProducer]) REFERENCES [dbo].[Entities] ([Id])
GO
ALTER TABLE [dbo].[ProductSpecs] CHECK CONSTRAINT [FK_ProductSpecs_Entities_Producer]
GO

-- dicProductDeclarationProducts -> dicProductDeclarationCategory
ALTER TABLE [dbo].[dicProductDeclarationProducts] WITH CHECK
  ADD CONSTRAINT [FK_dicProductDeclarationProducts_Category] FOREIGN KEY([CategoryId]) REFERENCES [dbo].[dicProductDeclarationCategory] ([Id])
GO
ALTER TABLE [dbo].[dicProductDeclarationProducts] CHECK CONSTRAINT [FK_dicProductDeclarationProducts_Category]
GO

-- DUMRestrictionRules -> DUMZones
ALTER TABLE [dbo].[DUMRestrictionRules] WITH CHECK
  ADD CONSTRAINT [FK_DUMRestrictionRules_DUMZones] FOREIGN KEY([ZoneId]) REFERENCES [dbo].[DUMZones] ([Id])
GO
ALTER TABLE [dbo].[DUMRestrictionRules] CHECK CONSTRAINT [FK_DUMRestrictionRules_DUMZones]
GO

-- EcoModulationRules -> EcoModulationRuleSets
ALTER TABLE [dbo].[EcoModulationRules] WITH CHECK
  ADD CONSTRAINT [FK_EcoModulationRules_EcoModulationRuleSets] FOREIGN KEY([RuleSetId]) REFERENCES [dbo].[EcoModulationRuleSets] ([Id])
GO
ALTER TABLE [dbo].[EcoModulationRules] CHECK CONSTRAINT [FK_EcoModulationRules_EcoModulationRuleSets]
GO

-- EmissionFactors -> EmissionFactorSets
ALTER TABLE [dbo].[EmissionFactors] WITH CHECK
  ADD CONSTRAINT [FK_EmissionFactors_EmissionFactorSets] FOREIGN KEY([FactorSetId]) REFERENCES [dbo].[EmissionFactorSets] ([Id])
GO
ALTER TABLE [dbo].[EmissionFactors] CHECK CONSTRAINT [FK_EmissionFactors_EmissionFactorSets]
GO

-- Geografía
ALTER TABLE [dbo].[Municipality]         WITH CHECK ADD CONSTRAINT [FK_Municipality_Province]              FOREIGN KEY([Id_Province])    REFERENCES [dbo].[Province]        ([id])
GO
ALTER TABLE [dbo].[Municipality]         CHECK CONSTRAINT [FK_Municipality_Province]
GO
ALTER TABLE [dbo].[MunicipalityPopulation] WITH CHECK ADD CONSTRAINT [FK_MunicipalityPopulation_Municipality] FOREIGN KEY([IdMunicipality]) REFERENCES [dbo].[Municipality]     ([Id])
GO
ALTER TABLE [dbo].[MunicipalityPopulation] CHECK CONSTRAINT [FK_MunicipalityPopulation_Municipality]
GO
ALTER TABLE [dbo].[MunicipalityZipCode]  WITH CHECK ADD CONSTRAINT [FK_MunicipalityZipCode_Municipality]  FOREIGN KEY([IdMunicipality]) REFERENCES [dbo].[Municipality]     ([Id])
GO
ALTER TABLE [dbo].[MunicipalityZipCode]  CHECK CONSTRAINT [FK_MunicipalityZipCode_Municipality]
GO
ALTER TABLE [dbo].[Province]             WITH CHECK ADD CONSTRAINT [FK_Province_TerritoryState]           FOREIGN KEY([idState])        REFERENCES [dbo].[TerritoryState]   ([id])
GO
ALTER TABLE [dbo].[Province]             CHECK CONSTRAINT [FK_Province_TerritoryState]
GO
ALTER TABLE [dbo].[TerritoryState]       WITH NOCHECK ADD CONSTRAINT [FK_TerritoryState_Country]          FOREIGN KEY([idCountry])      REFERENCES [dbo].[Country]          ([id])
GO
ALTER TABLE [dbo].[TerritoryState]       CHECK CONSTRAINT [FK_TerritoryState_Country]
GO

-- Users
ALTER TABLE [dbo].[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Country]        FOREIGN KEY([NationalId])     REFERENCES [dbo].[Country]        ([id])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_Country]
GO
ALTER TABLE [dbo].[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Municipality]   FOREIGN KEY([MunicipalityId]) REFERENCES [dbo].[Municipality]   ([Id])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_Municipality]
GO
ALTER TABLE [dbo].[Users] WITH CHECK ADD CONSTRAINT [FK_Users_Profiles]       FOREIGN KEY([IdProfile])      REFERENCES [dbo].[Profiles]       ([ID])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_Profiles]
GO
ALTER TABLE [dbo].[Users] WITH CHECK ADD CONSTRAINT [FK_Users_TerritoryState] FOREIGN KEY([GeographicalId]) REFERENCES [dbo].[TerritoryState] ([id])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_TerritoryState]
GO

ALTER TABLE [dbo].[UserSharePointCredentials] WITH CHECK
  ADD CONSTRAINT [FK_UserSharePointCredentials_Users] FOREIGN KEY([UserID]) REFERENCES [dbo].[Users] ([ID])
  ON UPDATE CASCADE ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserSharePointCredentials] CHECK CONSTRAINT [FK_UserSharePointCredentials_Users]
GO

ALTER DATABASE [greentransitdb] SET READ_WRITE 
GO
