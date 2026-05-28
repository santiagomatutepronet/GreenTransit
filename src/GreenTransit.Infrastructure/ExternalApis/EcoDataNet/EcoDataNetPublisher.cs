using System.Diagnostics;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet;

/// <summary>
/// Orquestador que publica los 16 endpoints de GreenTransit a la API EcoDataNet Waste.
/// </summary>
public class EcoDataNetPublisher : IEcoDataNetPublisher
{
    private readonly IApplicationDbContext         _db;
    private readonly EcoDataNetHttpClient          _httpClient;
    private readonly IOptions<EcoDataNetOptions>   _options;
    private readonly ILogger<EcoDataNetPublisher>  _logger;

    // ── OwnerIds fijos / cíclicos por endpoint ────────────────────────────────
    private static readonly Guid OwnerPlant       = Guid.Parse("B5BF81E1-92B9-4D5D-A873-B23F187D8088");
    private static readonly Guid OwnerCac         = Guid.Parse("ACB7BFE6-AAE2-4A9B-BC23-A1D7EAA7DEEF");
    private static readonly Guid OwnerProducer    = Guid.Parse("D5FD04C2-752B-4277-6EBB-08DE64D3ACCE");
    private static readonly Guid OwnerDispatch    = Guid.Parse("0168F029-6DD2-4EBF-9CF1-FC8DCC0819AB");
    private static readonly Guid OwnerProductSpec = Guid.Parse("5ACB6BA6-8EF7-42EC-8DA7-B47DEBC7D160");
    private static readonly Guid OwnerDum         = Guid.Parse("64ED5419-D01C-4009-AFE7-173F1857C84F");

    private static readonly Guid[] OwnerWasteMove = [
        Guid.Parse("F49F3B63-120B-49B2-9B27-F42D2E80153C"),
        Guid.Parse("4E3C335B-A84A-4E3E-B960-71F7086C6489"),
        Guid.Parse("7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00"),
    ];
    private static readonly Guid[] OwnerMarketShare = [
        Guid.Parse("4E3C335B-A84A-4E3E-B960-71F7086C6489"),
        Guid.Parse("7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00"),
    ];
    private static readonly Guid[] OwnerEcoMod = [
        Guid.Parse("4E3C335B-A84A-4E3E-B960-71F7086C6489"),
        Guid.Parse("7E1AA26A-D006-42DF-BCA2-8FF1F56FCD00"),
    ];

    public EcoDataNetPublisher(
        IApplicationDbContext        db,
        EcoDataNetHttpClient         httpClient,
        IOptions<EcoDataNetOptions>  options,
        ILogger<EcoDataNetPublisher> logger)
    {
        _db         = db;
        _httpClient = httpClient;
        _options    = options;
        _logger     = logger;
    }

    // ── Punto de entrada ──────────────────────────────────────────────────────
    public async Task<PublishSummary> PublishAllAsync(
        Action<string, int, int>? onProgress, CancellationToken ct)
    {
        var sw      = Stopwatch.StartNew();
        var summary = new PublishSummary();
        var batch   = _options.Value.BatchSize;

        // Publicar WasteMoves primero para obtener el mapa ref→owner con los índices exactos.
        onProgress?.Invoke("WasteMoves", 1, 16);
        var (wmResult, refToOwner) = await PublishWasteMovesAsync(batch, ct);
        summary.Results.Add(wmResult);
        _logger.LogInformation(
            "EcoDataNet [{Endpoint}]: enviados={Sent} ok={Ok} errores={Err}",
            wmResult.Endpoint, wmResult.TotalSent, wmResult.SuccessCount, wmResult.ErrorCount);

        var endpoints = new (string Name, Func<Task<EndpointResult>> Action)[]
        {
            ("EntryPlants",           () => PublishEntryPlantsAsync(batch, refToOwner, ct)),
            ("EntryCACs",             () => PublishEntryCACSAsync(batch, refToOwner, ct)),
            ("TreatmentPlants",       () => PublishTreatmentPlantsAsync(batch, refToOwner, ct)),
            ("ProductDeclarations",   () => PublishProductDeclarationsAsync(batch, ct)),
            ("ServiceOrders",         () => PublishServiceOrdersAsync(batch, ct)),
            ("Agreements",            () => PublishAgreementsAsync(batch, ct)),
            ("Settlements",           () => PublishSettlementsAsync(batch, ct)),
            ("AgreementDocuments",    () => PublishAgreementDocumentsAsync(batch, ct)),
            ("MarketShares",          () => PublishMarketSharesAsync(batch, ct)),
            ("ProductSpecs",          () => PublishProductSpecsAsync(batch, ct)),
            ("PlantEnergies",         () => PublishPlantEnergiesAsync(batch, ct)),
            ("Incidents",             () => PublishIncidentsAsync(batch, ct)),
            ("EmissionFactorSets",    () => PublishEmissionFactorSetsAsync(batch, ct)),
            ("EcoModulationRuleSets", () => PublishEcoModulationRuleSetsAsync(batch, ct)),
            ("DUMZones",              () => PublishDUMZonesAsync(batch, ct)),
        };

        for (int i = 0; i < endpoints.Length; i++)
        {
            onProgress?.Invoke(endpoints[i].Name, i + 2, 16);
            try
            {
                var result = await endpoints[i].Action();
                summary.Results.Add(result);
                _logger.LogInformation(
                    "EcoDataNet [{Endpoint}]: enviados={Sent} ok={Ok} errores={Err}",
                    result.Endpoint, result.TotalSent, result.SuccessCount, result.ErrorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EcoDataNet [{Endpoint}]: error inesperado.", endpoints[i].Name);
                summary.Results.Add(new EndpointResult
                {
                    Endpoint     = endpoints[i].Name,
                    ErrorMessage = ex.Message
                });
            }
        }

        sw.Stop();
        summary.Duration = sw.Elapsed;
        return summary;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ThirdPartyRef? MapEntity(BusinessEntity? e)
    {
        if (e is null) return null;
        return new ThirdPartyRef
        {
            TypeThirdParty    = EcoDataNetEnumMapper.ToTypeThirdParty(e.TypeThirdParty),
            Name              = e.Name,
            NationalId        = e.NationalId,
            CenterCode        = e.CenterCode,
            EntityType        = e.EntityType,
            InscriptionType   = e.InscriptionType,
            InscriptionNumber = e.InscriptionNumber,
            CountryCode       = e.CountryCode,
            StateCode         = e.StateCode,
            ZipCode           = e.ZipCode,
            Latitude          = e.Latitude,
            Longitude         = e.Longitude,
        };
    }

    private static async Task SendInBatchesAsync<T>(
        EcoDataNetHttpClient client, string endpoint, List<T> items,
        int batchSize, EndpointResult result, CancellationToken ct)
    {
        for (int offset = 0; offset < items.Count; offset += batchSize)
        {
            var batch = items.Skip(offset).Take(batchSize).ToList();
            var r     = await client.PostBatchAsync(endpoint, batch, ct);
            result.TotalSent    += r.TotalSent;
            result.SuccessCount += r.SuccessCount;
            result.ErrorCount   += r.ErrorCount;
            if (r.ErrorMessage is not null) result.ErrorMessage = r.ErrorMessage;
            if (r.ErrorDetail  is not null) result.ErrorDetail  = r.ErrorDetail;
        }
    }

    // ── Endpoint 1: WasteMoves ────────────────────────────────────────────────
    private async Task<(EndpointResult Result, Dictionary<string, Guid> RefToOwner)> PublishWasteMovesAsync(
        int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "WasteMoves" };
        var moves  = await _db.WasteMoves
            .AsNoTracking()
            .OrderBy(wm => wm.DateCreateSys)
            .Include(wm => wm.Scrap)
            .Include(wm => wm.Scrap2)
            .Include(wm => wm.Source)
            .Include(wm => wm.Destination)
            .Include(wm => wm.OperatorTransfer)
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(r => r.Residue)
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(r => r.LerCode)
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(r => r.Carrier)
            .ToListAsync(ct);

        var items = moves.Select(wm => new WasteMoveItem
        {
            RemoteId             = wm.Id,
            OwnerId              = OwnerWasteMove[Math.Abs(wm.Id.GetHashCode()) % OwnerWasteMove.Length],
            GatheredDate         = wm.GatheredDate,
            RequestDate          = wm.RequestDate,
            PlantEntryDate       = wm.PlantEntryDate,
            WasteMoveReference   = wm.WasteMoveReference,
            Lot                  = wm.Lot,
            ServiceOrderId       = wm.ServiceOrderId,
            ServiceStatus        = wm.ServiceStatus,
            PlannedPickupStart   = wm.PlannedPickupStart,
            PlannedPickupEnd     = wm.PlannedPickupEnd,
            PlannedDeliveryStart = wm.PlannedDeliveryStart,
            PlannedDeliveryEnd   = wm.PlannedDeliveryEnd,
            ActualPickupStart    = wm.ActualPickupStart,
            ActualPickupEnd      = wm.ActualPickupEnd,
            ActualDeliveryStart  = wm.ActualDeliveryStart,
            ActualDeliveryEnd    = wm.ActualDeliveryEnd,
            DocumentId           = wm.DocumentId,
            DocumentHash         = wm.DocumentHash,
            SignatureStatus      = wm.SignatureStatus,
            SourceSystem         = wm.SourceSystem ?? "GreenTransit",
            Version              = wm.Version,
            Scrap                = MapEntity(wm.Scrap),
            Scrap2               = MapEntity(wm.Scrap2),
            Source               = MapEntity(wm.Source),
            Destination          = MapEntity(wm.Destination),
            OperatorTransfer     = MapEntity(wm.OperatorTransfer),
            Residues             = wm.WasteMoveResidues.Select(r => new WasteMoveResidueItem
            {
                IdWasteMove     = r.IdWasteMove,
                LerCode         = r.LerCode?.Code,
                LerCodeExtended = r.LerCode?.CodeExtended,
                Dangerous       = r.Residue?.IsDangerous,
                Raee            = r.Residue?.IsRAEE,
                ProductUse      = EcoDataNetEnumMapper.ToUseProduct(r.Residue?.ProductUse),
                ProductCategory = EcoDataNetEnumMapper.ToCategoryProduct(r.Residue?.ProductCategory),
                Description     = r.Residue?.Description,
                ResidueName     = r.Residue?.Name,
                Weight          = r.Weight,
                MeasureUnit     = EcoDataNetEnumMapper.ToMeasureUnit(r.MeasureUnit),
                Units           = r.Units,
                UnitPriceKg     = r.UnitPriceKg,
                NtNumber        = r.NTNumber,
                DiNumber        = r.DINumber,
                DiPhase         = r.DIPhase,
                DangerousCode   = r.Residue?.DangerousCode,
                Carrier         = MapEntity(r.Carrier),
                TransportInfo   = new TransportInfoItem
                {
                    VehicleRegistration        = r.TransportInfo_VehicleRegistration,
                    VehicleRegistrationTrailer = r.TransportInfo_VehicleRegistrationTrailer,
                    TransportDuration          = r.TransportInfo_TransportDuration,
                    TransportDistance          = r.TransportInfo_TransportDistance,
                    TransportCarbonEmissions   = r.TransportInfo_TransportCarbonEmissions,
                },
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/WasteMoves/Register", items, batchSize, result, ct);

        // Construir mapa ref→owner con la misma lógica determinista usada arriba
        var refToOwner = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var wm in moves)
        {
            if (wm.WasteMoveReference is not null)
                refToOwner.TryAdd(wm.WasteMoveReference, OwnerWasteMove[Math.Abs(wm.Id.GetHashCode()) % OwnerWasteMove.Length]);
        }

        return (result, refToOwner);
    }

    // ── Endpoint 2: EntryPlants ───────────────────────────────────────────────
    private async Task<EndpointResult> PublishEntryPlantsAsync(
        int batchSize, Dictionary<string, Guid> refToOwner, CancellationToken ct)
    {
        var result  = new EndpointResult { Endpoint = "EntryPlants" };

        var entries = await _db.EntryPlants
            .AsNoTracking()
            .Include(ep => ep.EntryPlantResidues)
                .ThenInclude(r => r.Residue)
                    .ThenInclude(res => res!.LerCode)
            .Where(ep => ep.WasteMoveReference != null)
            .ToListAsync(ct);

        entries = entries.Where(ep => refToOwner.ContainsKey(ep.WasteMoveReference!)).ToList();

        var items = entries.Select(ep => new EntryPlantItem
        {
            RemoteId           = ep.Id,
            OwnerId            = refToOwner.GetValueOrDefault(ep.WasteMoveReference!, OwnerPlant),
            WasteMoveReference = ep.WasteMoveReference,
            TicketScale        = ep.TicketScale,
            PlantEntryDate     = ep.PlantEntryDate,
            TypeContainer      = EcoDataNetEnumMapper.ToTypeContainer(ep.TypeContainer) ?? 8,
            PriceContainer     = ep.PriceContainer,
            Residues           = ep.EntryPlantResidues.Select(r => new EntryPlantResidueItem
            {
                IdEntryPlant    = r.IdEntryPlant,
                ResidueName     = r.Residue?.Name,
                LerCode         = r.Residue?.LerCode?.Code,
                LerCodeExtended = r.Residue?.LerCode?.CodeExtended,
                Weight          = r.Weight,
                MeasureUnit     = EcoDataNetEnumMapper.ToMeasureUnit(r.MeasureUnit),
                Units           = r.Units,
                PriceWeight     = r.PriceWeight,
                PriceUnit       = r.PriceUnit,
                Dangerous       = r.Residue?.IsDangerous,
                Raee            = r.Residue?.IsRAEE,
                ProductCategory = EcoDataNetEnumMapper.ToCategoryProduct(r.Residue?.ProductCategory),
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/EntryPlants/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 3: EntryCACs ─────────────────────────────────────────────────
    private async Task<EndpointResult> PublishEntryCACSAsync(
        int batchSize, Dictionary<string, Guid> refToOwner, CancellationToken ct)
    {
        var result  = new EndpointResult { Endpoint = "EntryCACs" };

        var entries = await _db.EntryCACs
            .AsNoTracking()
            .Include(ec => ec.EntryCACResidues)
                .ThenInclude(r => r.Residue)
                    .ThenInclude(res => res!.LerCode)
            .Where(ec => ec.WasteMoveReference != null)
            .ToListAsync(ct);

        entries = entries.Where(ec => refToOwner.ContainsKey(ec.WasteMoveReference!)).ToList();

        var items = entries.Select(ec => new EntryCACItem
        {
            RemoteId           = ec.Id,
            OwnerId            = refToOwner.GetValueOrDefault(ec.WasteMoveReference!, OwnerCac),
            WasteMoveReference = ec.WasteMoveReference,
            CacEntryDate       = ec.CACEntryDate,
            TypeContainer      = EcoDataNetEnumMapper.ToTypeContainer(ec.TypeContainer) ?? 8,
            PriceContainer     = ec.PriceContainer,
            CollectionMethod   = ec.CollectionMethod,
            Residues           = ec.EntryCACResidues.Select(r => new EntryCACResidueItem
            {
                IdEntryCAC      = r.IdEntryCAC,
                ResidueName     = r.Residue?.Name,
                LerCode         = r.Residue?.LerCode?.Code,
                LerCodeExtended = r.Residue?.LerCode?.CodeExtended,
                Weight          = r.Weight,
                MeasureUnit     = EcoDataNetEnumMapper.ToMeasureUnit(r.MeasureUnit),
                Units           = r.Units,
                PriceWeight     = r.PriceWeight,
                PriceUnit       = r.PriceUnit,
                Dangerous       = r.Residue?.IsDangerous,
                Raee            = r.Residue?.IsRAEE,
                ProductCategory = EcoDataNetEnumMapper.ToCategoryProduct(r.Residue?.ProductCategory),
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/EntryCACs/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 4: TreatmentPlants ───────────────────────────────────────────
    private async Task<EndpointResult> PublishTreatmentPlantsAsync(
        int batchSize, Dictionary<string, Guid> refToOwner, CancellationToken ct)
    {
        var result     = new EndpointResult { Endpoint = "TreatmentPlants" };

        var treatments = await _db.TreatmentPlants
            .AsNoTracking()
            .Include(tp => tp.TreatmentPlantResidues)
                .ThenInclude(r => r.Residue)
                    .ThenInclude(res => res!.LerCode)
            .Where(tp => tp.WasteMoveReference != null)
            .ToListAsync(ct);

        treatments = treatments.Where(tp => refToOwner.ContainsKey(tp.WasteMoveReference!)).ToList();

        var items = treatments.Select(tp => new TreatmentPlantItem
        {
            RemoteId            = tp.Id,
            OwnerId             = refToOwner.GetValueOrDefault(tp.WasteMoveReference!, OwnerPlant),
            WasteMoveReference  = tp.WasteMoveReference,
            TicketScale         = tp.TicketScale,
            PlantTreatmentDate  = tp.PlantTreatmentDate,
            TypeContainer       = EcoDataNetEnumMapper.ToTypeContainer(tp.TypeContainer) ?? 8,
            PriceContainer      = tp.PriceContainer,
            Residues            = tp.TreatmentPlantResidues.Select(r => new TreatmentPlantResidueItem
            {
                IdTreatmentPlant  = r.IdTreatmentPlant,
                ResidueName       = r.Residue?.Name,
                LerCode           = r.Residue?.LerCode?.Code,
                LerCodeExtended   = r.Residue?.LerCode?.CodeExtended,
                Category          = EcoDataNetEnumMapper.ToCategoryProduct(r.Category),
                WeightTotal       = r.WeightTotal,
                MeasureUnit       = EcoDataNetEnumMapper.ToMeasureUnit(r.MeasureUnit),
                Units             = r.Units,
                PriceWeight       = r.PriceWeight,
                PriceUnit         = r.PriceUnit,
                WeightReused      = r.WeightReused,
                MeasureUnitReused = EcoDataNetEnumMapper.ToMeasureUnit(r.MeasureUnitReused),
                UnitsReused       = r.UnitsReused,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/TreatmentPlants/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 5: ProductDeclarations ──────────────────────────────────────
    private async Task<EndpointResult> PublishProductDeclarationsAsync(int batchSize, CancellationToken ct)
    {
        var result       = new EndpointResult { Endpoint = "ProductDeclarations" };
        var declarations = await _db.ProductDeclarations
            .AsNoTracking()
            .Include(pd => pd.Producer)
            .Include(pd => pd.Products)
                .ThenInclude(p => p.Residue)
            .ToListAsync(ct);

        var items = declarations.Select(pd => new ProductDeclarationItem
        {
            RemoteId   = pd.Id,
            OwnerId    = OwnerProducer,
            Period     = pd.Period?.ToString(),
            Year       = pd.Year,
            Month      = pd.Month,
            Reference  = pd.Reference,
            Currency   = pd.Currency,
            State      = pd.State,
            DateCreate = pd.DateCreate,
            DateEmit   = pd.DateEmit,
            Amount     = pd.Amount,
            Type       = pd.Type,
            Producer   = pd.Producer is null ? null : new ProducerRef
            {
                Name             = pd.Producer.Name,
                NationalId       = pd.Producer.NationalId,
                CountryCode      = pd.Producer.CountryCode,
                StateCode        = pd.Producer.StateCode,
                ZipCode          = pd.Producer.ZipCode,
                MunicipalityCode = pd.Producer.MunicipalityCode,
                Address          = pd.Producer.Address,
            },
            Products   = pd.Products.Select(p => new ProductItem
            {
                Id                   = p.Id,
                IdProductDeclaration = p.IdProductDeclaration,
                Description          = p.Residue?.Description ?? p.ProductName,
                Reference            = p.Reference,
                Source               = p.Source,
                Quantity             = p.Quantity,
                Price                = p.Price,
                MeasureUnit          = p.MeasureUnit,
                Units                = p.Units,
                ProductUse           = p.ProductUse,
                ProductCategory      = p.ProductCategory,
                WeightPerUnitKg      = p.Residue?.WeightPerUnitKg,
                ReparabilityIndex    = p.Residue?.ReparabilityIndex,
                RecycledContentPercent = p.Residue?.RecycledContentPercent,
                MaterialsJson        = p.Residue?.MaterialsJson,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/ProductDeclarations/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 6: ServiceOrders ─────────────────────────────────────────────
    private async Task<EndpointResult> PublishServiceOrdersAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "ServiceOrders" };
        var orders = await _db.ServiceOrders
            .AsNoTracking()
            .Include(so => so.IssuedBy)
            .Include(so => so.Carrier)
            .Include(so => so.PlannedPlant)
            .Include(so => so.PickupPoint)
            .Include(so => so.LerCode)
            .ToListAsync(ct);

        var items = orders.Select(so => new ServiceOrderItem
        {
            RemoteId                       = so.Id,
            OwnerId                        = OwnerDispatch,
            ServiceOrderNumber             = so.ServiceOrderNumber,
            IssuedAt                       = so.IssuedAt,
            IssuedByName                   = so.IssuedByName ?? so.IssuedBy?.Name,
            IssuedByNationalId             = so.IssuedByNationalId ?? so.IssuedBy?.NationalId,
            IssuedByCenterCode             = so.IssuedByCenterCode ?? so.IssuedBy?.CenterCode,
            Status                         = so.Status,
            Priority                       = so.Priority,
            WasteStream                    = so.WasteStream,
            SubStream                      = so.SubStream,
            ProductUse                     = so.ProductUse,
            ProductCategory                = so.ProductCategory,
            LerCode                        = so.LerCode?.Code,
            LerCodeExtended                = so.LerCode?.CodeExtended,
            PointName                      = so.PickupPoint?.Name,
            PointType                      = so.PickupPoint?.EntityType,
            PointAddress                   = so.PickupPoint?.Address,
            MunicipalityCode               = so.PickupPoint?.MunicipalityCode,
            Latitude                       = so.PickupPoint?.Latitude,
            Longitude                      = so.PickupPoint?.Longitude,
            PlannedPickupStart             = so.PlannedPickupStart,
            PlannedPickupEnd               = so.PlannedPickupEnd,
            PlannedDeliveryStart           = so.PlannedDeliveryStart,
            PlannedDeliveryEnd             = so.PlannedDeliveryEnd,
            EstimatedWeight                = so.EstimatedWeight,
            MeasureUnit                    = so.MeasureUnit,
            Units                          = so.Units,
            ContainersJson                 = so.ContainersJson,
            AssignedCarrierName            = so.Carrier?.Name,
            AssignedCarrierNationalId      = so.Carrier?.NationalId,
            AssignedCarrierCenterCode      = so.Carrier?.CenterCode,
            AssignedCarrierInscriptionType   = so.Carrier?.InscriptionType,
            AssignedCarrierInscriptionNumber = so.Carrier?.InscriptionNumber,
            PlannedPlantName               = so.PlannedPlant?.Name,
            PlannedPlantCenterCode         = so.PlannedPlant?.CenterCode,
            WasteMoveReference             = so.WasteMoveReference,
            TicketScalePlanned             = so.TicketScalePlanned,
            ActualPickupStart              = so.ActualPickupStart,
            ActualPickupEnd                = so.ActualPickupEnd,
            ActualDeliveryStart            = so.ActualDeliveryStart,
            ActualDeliveryEnd              = so.ActualDeliveryEnd,
            TransportDistanceKm            = so.TransportDistanceKm,
            TransportDurationMin           = so.TransportDurationMin,
            VehicleRegistration            = so.VehicleRegistration,
            VehicleType                    = so.VehicleType,
            FuelType                       = so.FuelType,
            EuroClass                      = so.EuroClass,
            SourceSystem                   = so.SourceSystem ?? "GreenTransit",
            Version                        = so.Version,
            Hash                           = so.Hash,
            CreatedAt                      = so.CreatedAt,
            UpdatedAt                      = so.UpdatedAt,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/ServiceOrders/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 7: Agreements ────────────────────────────────────────────────
    private async Task<EndpointResult> PublishAgreementsAsync(int batchSize, CancellationToken ct)
    {
        var result     = new EndpointResult { Endpoint = "Agreements" };
        var agreements = await _db.Agreements
            .AsNoTracking()
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .Include(a => a.Coordinator)
            .Include(a => a.AgreementDocuments)
            .ToListAsync(ct);

        var items = agreements.Select(a => new AgreementItem
        {
            RemoteId               = a.Id,
            OwnerId                = OwnerDispatch,
            AgreementNumber        = a.AgreementNumber,
            Status                 = a.Status,
            EffectiveFrom          = a.EffectiveFrom,
            EffectiveTo            = a.EffectiveTo,
            ScrapName              = a.Scrap?.Name,
            ScrapNationalId        = a.Scrap?.NationalId,
            ScrapCenterCode        = a.Scrap?.CenterCode,
            PublicEntityName       = a.PublicEntity?.Name,
            PublicEntityNationalId = a.PublicEntity?.NationalId,
            PublicEntityCenterCode = a.PublicEntity?.CenterCode,
            CoordinatorName        = a.Coordinator?.Name,
            CoordinatorNationalId  = a.Coordinator?.NationalId,
            CoordinatorCenterCode  = a.Coordinator?.CenterCode,
            WasteStream            = a.WasteStream,
            SubStream              = a.SubStream,
            AutonomousCommunity    = a.AutonomousCommunity,
            ProvinceCode           = a.ProvinceCode,
            MunicipalityCode       = a.MunicipalityCode,
            CoveredMethodsJson     = a.CoveredMethodsJson,
            TariffModelType        = a.TariffModelType,
            Currency               = a.Currency,
            TariffRulesJson        = a.TariffRulesJson,
            MinimumsJson           = a.MinimumsJson,
            ObligationsJson        = a.ObligationsJson,
            SourceSystem           = a.SourceSystem ?? "GreenTransit",
            Version                = a.Version,
            Hash                   = a.Hash,
            CreatedAt              = a.CreatedAt,
            UpdatedAt              = a.UpdatedAt,
            Documents              = a.AgreementDocuments.Select(d => new AgreementDocumentItem
            {
                RemoteId          = d.Id,
                AgreementId       = d.AgreementId,
                DocumentType      = d.DocumentType,
                DocumentId        = d.DocumentId,
                DocumentHash      = d.DocumentHash,
                SignedAt          = d.SignedAt,
                SignatureProvider = d.SignatureProvider,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/Agreements/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 8: Settlements ───────────────────────────────────────────────
    private async Task<EndpointResult> PublishSettlementsAsync(int batchSize, CancellationToken ct)
    {
        var result      = new EndpointResult { Endpoint = "Settlements" };
        var settlements = await _db.Settlements
            .AsNoTracking()
            .Include(s => s.Scrap)
            .Include(s => s.PublicEntity)
            .Include(s => s.SettlementLines)
                .ThenInclude(l => l.LerCode)
            .ToListAsync(ct);

        var items = settlements.Select(s => new SettlementItem
        {
            RemoteId               = s.Id,
            OwnerId                = OwnerDispatch,
            SettlementNumber       = s.SettlementNumber,
            Status                 = s.Status,
            AgreementId            = s.AgreementId,
            Year                   = s.Year,
            Month                  = s.Month,
            ScrapName              = s.Scrap?.Name,
            ScrapNationalId        = s.Scrap?.NationalId,
            PublicEntityName       = s.PublicEntity?.Name,
            PublicEntityNationalId = s.PublicEntity?.NationalId,
            Currency               = s.Currency,
            BaseAmount             = s.BaseAmount,
            AdjustmentsAmount      = s.AdjustmentsAmount,
            TaxAmount              = s.TaxAmount,
            TotalAmount            = s.TotalAmount,
            EvidenceRefsJson       = s.EvidenceRefsJson,
            Validator              = s.Validator,
            ValidationStatus       = s.ValidationStatus,
            ValidatedAt            = s.ValidatedAt,
            ValidationRef          = s.ValidationRef,
            SourceSystem           = s.SourceSystem ?? "GreenTransit",
            Version                = s.Version,
            Hash                   = s.Hash,
            CreatedAt              = s.CreatedAt,
            UpdatedAt              = s.UpdatedAt,
            Lines                  = s.SettlementLines.Select(l => new SettlementLineItem
            {
                RemoteId        = l.Id,
                SettlementId    = l.SettlementId,
                ProductCategory = l.ProductCategory,
                LerCode         = l.LerCode?.Code,
                WeightKg        = l.WeightKg,
                PricePerKg      = l.PricePerKg,
                Amount          = l.Amount,
                EvidenceType    = l.EvidenceType,
                SourceIdsJson   = l.SourceIdsJson,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/Settlements/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 9: AgreementDocuments (huérfanos) ────────────────────────────
    private async Task<EndpointResult> PublishAgreementDocumentsAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "AgreementDocuments" };
        var docs   = await _db.AgreementDocuments
            .AsNoTracking()
            .ToListAsync(ct);

        var items = docs.Select(d => new AgreementDocumentItem
        {
            RemoteId          = d.Id,
            AgreementId       = d.AgreementId,
            DocumentType      = d.DocumentType,
            DocumentId        = d.DocumentId,
            DocumentHash      = d.DocumentHash,
            SignedAt          = d.SignedAt,
            SignatureProvider = d.SignatureProvider,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/AgreementDocuments/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 10: MarketShares ─────────────────────────────────────────────
    private async Task<EndpointResult> PublishMarketSharesAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "MarketShares" };
        var shares = await _db.MarketShares
            .AsNoTracking()
            .Include(ms => ms.Scrap)
            .ToListAsync(ct);

        var items = shares.Select((ms, idx) => new MarketShareItem
        {
            RemoteId            = ms.Id,
            OwnerId             = OwnerMarketShare[idx % OwnerMarketShare.Length],
            Scrap               = ms.Scrap?.Name,
            Category            = EcoDataNetEnumMapper.ToCategoryProduct(ms.Category),
            AutonomousCommunity = ms.AutonomousCommunity,
            Year                = ms.Year,
            Weight              = ms.Weight,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/MarketShares/Register", items, batchSize, result, ct);

        // Segunda publicación con OwnerId fijo (OwnerDispatch)
        var itemsDispatch = shares.Select(ms => new MarketShareItem
        {
            RemoteId            = ms.Id,
            OwnerId             = OwnerDispatch,
            Scrap               = ms.Scrap?.Name,
            Category            = EcoDataNetEnumMapper.ToCategoryProduct(ms.Category),
            AutonomousCommunity = ms.AutonomousCommunity,
            Year                = ms.Year,
            Weight              = ms.Weight,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/MarketShares/Register", itemsDispatch, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 11: ProductSpecs ─────────────────────────────────────────────
    private async Task<EndpointResult> PublishProductSpecsAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "ProductSpecs" };
        var specs  = await _db.ProductSpecs
            .AsNoTracking()
            .Include(ps => ps.Producer)
            .Include(ps => ps.Residue)
            .ToListAsync(ct);

        var items = specs.Select(ps => new ProductSpecItem
        {
            RemoteId              = ps.Id,
            OwnerId               = OwnerProductSpec,
            ProductRef            = ps.ProductRef,
            ProductUse            = ps.ProductUse,
            ProductCategory       = ps.ProductCategory,
            CategoryRef           = ps.CategoryRef,
            ProducerName          = ps.Producer?.Name,
            ProducerNationalId    = ps.Producer?.NationalId,
            ProducerRef           = ps.ProducerRef,
            CompositionJson       = ps.Residue?.CompositionJson,
            WeightPerUnitKg       = ps.Residue?.WeightPerUnitKg,
            ReparabilityIndex     = ps.Residue?.ReparabilityIndex,
            DisassemblyEase       = ps.Residue?.DisassemblyEase is not null
                                    ? (decimal?)null : null,  // string→decimal no aplica
            ContainsHazardous     = ps.Residue?.ContainsHazardous,
            PotentialLERCodesJson = ps.Residue?.PotentialLERCodesJson,
            Notes                 = ps.Notes,
            SourceSystem          = ps.SourceSystem ?? "GreenTransit",
            Version               = ps.Version,
            Hash                  = ps.Hash,
            CreatedAt             = ps.CreatedAt,
            UpdatedAt             = ps.UpdatedAt,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/ProductSpecs/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 12: PlantEnergies ────────────────────────────────────────────
    private async Task<EndpointResult> PublishPlantEnergiesAsync(int batchSize, CancellationToken ct)
    {
        var result   = new EndpointResult { Endpoint = "PlantEnergies" };
        var energies = await _db.PlantEnergies
            .AsNoTracking()
            .ToListAsync(ct);

        var items = energies.Select(pe => new PlantEnergyItem
        {
            RemoteId         = pe.Id,
            OwnerId          = OwnerPlant,
            PlantName        = pe.PlantName,
            PlantCenterCode  = pe.PlantCenterCode,
            Year             = pe.Year,
            Month            = pe.Month,
            KwhTotal         = pe.KwhTotal,
            Source           = pe.Source,
            GridMixRef       = pe.GridMixRef,
            AllocationMethod = pe.AllocationMethod,
            Notes            = pe.Notes,
            SourceSystem     = pe.SourceSystem ?? "GreenTransit",
            Version          = pe.Version,
            Hash             = pe.Hash,
            CreatedAt        = pe.CreatedAt,
            UpdatedAt        = pe.UpdatedAt,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/PlantEnergies/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 13: Incidents ────────────────────────────────────────────────
    private async Task<EndpointResult> PublishIncidentsAsync(int batchSize, CancellationToken ct)
    {
        var result    = new EndpointResult { Endpoint = "Incidents" };
        var incidents = await _db.Incidents
            .AsNoTracking()
            .ToListAsync(ct);

        var items = incidents.Select(i => new IncidentItem
        {
            RemoteId             = i.Id,
            OwnerId              = OwnerDispatch,
            Type                 = i.Type,
            Severity             = i.Severity,
            OpenedAt             = i.OpenedAt,
            ClosedAt             = i.ClosedAt,
            ServiceOrderId       = i.ServiceOrderId,
            WasteMoveReference   = i.WasteMoveReference,
            TicketScale          = i.TicketScale,
            ReportedByName       = i.ReportedByName,
            ReportedByNationalId = i.ReportedByNationalId,
            ReportedByCenterCode = i.ReportedByCenterCode,
            Description          = i.Description,
            ResolutionJson       = i.ResolutionJson,
            SourceSystem         = i.SourceSystem ?? "GreenTransit",
            Version              = i.Version,
            Hash                 = i.Hash,
            CreatedAt            = i.CreatedAt,
            UpdatedAt            = i.UpdatedAt,
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/Incidents/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 14: EmissionFactorSets ──────────────────────────────────────
    private async Task<EndpointResult> PublishEmissionFactorSetsAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "EmissionFactorSets" };
        var sets   = await _db.EmissionFactorSets
            .AsNoTracking()
            .Include(s => s.EmissionFactors)
            .ToListAsync(ct);

        var items = sets.Select(s => new EmissionFactorSetItem
        {
            RemoteId      = s.Id,
            OwnerId       = OwnerDispatch,
            FactorSetName = s.FactorSetName,
            Version       = s.Version,
            Status        = s.Status,
            ValidFrom     = s.ValidFrom,
            ValidTo       = s.ValidTo,
            Publisher     = s.Publisher,
            Reference     = s.Reference,
            Methodology   = s.Methodology,
            SourceSystem  = s.SourceSystem ?? "GreenTransit",
            Hash          = s.Hash,
            CreatedAt     = s.CreatedAt,
            UpdatedAt     = s.UpdatedAt,
            Factors       = s.EmissionFactors.Select(f => new EmissionFactorItem
            {
                RemoteId    = f.Id,
                FactorSetId = f.FactorSetId,
                VehicleType = f.VehicleType,
                FuelType    = f.FuelType,
                EuroClass   = f.EuroClass,
                Unit        = f.Unit,
                Value       = f.Value,
                CreatedAt   = f.CreatedAt,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/EmissionFactorSets/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 15: EcoModulationRuleSets ───────────────────────────────────
    private async Task<EndpointResult> PublishEcoModulationRuleSetsAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "EcoModulationRuleSets" };
        var sets   = await _db.EcoModulationRuleSets
            .AsNoTracking()
            .Include(s => s.EcoModulationRules)
            .ToListAsync(ct);

        var items = sets.Select((s, idx) => new EcoModulationRuleSetItem
        {
            RemoteId            = s.Id,
            OwnerId             = OwnerEcoMod[idx % OwnerEcoMod.Length],
            RuleSetName         = s.RuleSetName,
            Version             = s.Version,
            Status              = s.Status,
            ValidFrom           = s.ValidFrom,
            ValidTo             = s.ValidTo,
            PublisherName       = s.PublisherName,
            PublisherNationalId = s.PublisherNationalId,
            PublisherCenterCode = s.PublisherCenterCode,
            SourceSystem        = s.SourceSystem ?? "GreenTransit",
            Hash                = s.Hash,
            CreatedAt           = s.CreatedAt,
            UpdatedAt           = s.UpdatedAt,
            Rules               = s.EcoModulationRules.Select(r => new EcoModulationRuleItem
            {
                RemoteId        = r.Id,
                RuleSetId       = r.RuleSetId,
                RuleCode        = r.RuleCode,
                ProductCategory = r.ProductCategory,
                CriteriaJson    = r.CriteriaJson,
                FeeImpactType   = r.FeeImpactType,
                FeeImpactValue  = r.FeeImpactValue,
                CreatedAt       = r.CreatedAt,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/EcoModulationRuleSets/Register", items, batchSize, result, ct);
        return result;
    }

    // ── Endpoint 16: DUMZones ─────────────────────────────────────────────────
    private async Task<EndpointResult> PublishDUMZonesAsync(int batchSize, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = "DUMZones" };
        var zones  = await _db.DumZones
            .AsNoTracking()
            .Include(z => z.DumRestrictionRules)
            .ToListAsync(ct);

        var items = zones.Select(z => new DumZoneItem
        {
            RemoteId     = z.Id,
            OwnerId      = OwnerDum,
            ZoneCode     = z.ZoneCode,
            Name         = z.Name,
            Description  = z.Description,
            Status       = z.Status,
            GeometryJson = z.GeometryJson,
            SourceSystem = z.SourceSystem ?? "GreenTransit",
            Version      = z.Version,
            Hash         = z.Hash,
            CreatedAt    = z.CreatedAt,
            UpdatedAt    = z.UpdatedAt,
            Rules        = z.DumRestrictionRules.Select(r => new DumRestrictionRuleItem
            {
                RemoteId       = r.Id,
                OwnerId        = OwnerDum,
                RuleCode       = r.RuleCode,
                Status         = r.Status,
                ZoneId         = r.ZoneId,
                ValidFrom      = r.ValidFrom,
                ValidTo        = r.ValidTo,
                ConditionsJson = r.ConditionsJson,
                ActionType     = r.ActionType,
                ActionReason   = r.ActionReason,
                SourceSystem   = r.SourceSystem,
                Version        = r.Version,
                Hash           = r.Hash,
                CreatedAt      = r.CreatedAt,
                UpdatedAt      = r.UpdatedAt,
            }).ToList(),
        }).ToList();

        await SendInBatchesAsync(_httpClient, "/api/DUMZones/Register", items, batchSize, result, ct);
        return result;
    }
}
