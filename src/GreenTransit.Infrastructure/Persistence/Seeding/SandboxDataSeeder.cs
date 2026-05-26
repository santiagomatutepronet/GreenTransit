using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Persistence.Seeding;

/// <summary>
/// Servicio de seed de datos sandbox para GreenTransit.
/// Inserta ~1.400+ registros coherentes respetando FKs, multi-tenant y auditoría.
/// Idempotente: usa SourceSystem='SEED' como discriminador.
/// El OwnerId se resuelve desde el usuario autenticado; si está vacío usa el OwnerId demo.
/// </summary>
public sealed class SandboxDataSeeder : ISandboxDataSeeder
{
    // ── Constantes ────────────────────────────────────────────────────────────
    private const string Seed = "SEED";
    /// <summary>OwnerId de fallback cuando el admin no tiene tenant asignado.</summary>
    private static readonly Guid DemoOwnerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const int SeedUser = 1;

    // Calculado en tiempo de ejecución para que las fechas sean siempre relativas al momento actual
    private DateTime _now;

    // Horas variadas para distribuir las fechas con realismo en heatmaps
    private static readonly int[] SeedHours = { 7, 8, 8, 9, 10, 10, 11, 12, 13, 14, 15, 16, 17, 17, 18, 19, 20 };

    // Resuelto en tiempo de ejecución según el usuario autenticado
    private Guid _ownerId;

    private readonly AppDbContext               _db;
    private readonly ICurrentUserService        _currentUser;
    private readonly ILogger<SandboxDataSeeder> _log;

    public SandboxDataSeeder(
        AppDbContext db,
        ICurrentUserService currentUser,
        ILogger<SandboxDataSeeder> log)
    {
        _db          = db;
        _currentUser = currentUser;
        _log         = log;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SeedAsync
    // ─────────────────────────────────────────────────────────────────────────
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Usar el OwnerId del usuario actual; si es vacío (admin sin tenant) usar el demo
        _ownerId = _currentUser.OwnerId != Guid.Empty ? _currentUser.OwnerId : DemoOwnerId;
        _now = DateTime.UtcNow;
        _log.LogInformation("🌱 SandboxDataSeeder — inicio (OwnerId={OwnerId})", _ownerId);
        _db.IgnoreTenantFilter();

        try
        {
            await Phase0_CataloguesAsync(ct);
            await Phase1_EntitiesAsync(ct);
            await Phase2_ResiduesAsync(ct);
            await Phase3_AgreementsAsync(ct);
            await Phase4_OperationsAsync(ct);
            await Phase5_EntriesAsync(ct);
            await Phase6_EconomicsAsync(ct);
            await Phase7_DeclarationsAsync(ct);
            await Phase8_UsersAsync(ct);
            await Phase8b_SandboxUsersUcAsync(ct);
            await Phase9_DumAndEcoAsync(ct);
            await Phase10_RegulatoryTargetsAsync(ct);
            await Phase11_PlantEnergiesAsync(ct);
        }
        finally
        {
            _db.RestoreTenantFilter();
        }

        _log.LogInformation("✅ SandboxDataSeeder — completado");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CleanAsync
    // ─────────────────────────────────────────────────────────────────────────
    public async Task CleanAsync(CancellationToken ct = default)
    {
        _ownerId = _currentUser.OwnerId != Guid.Empty ? _currentUser.OwnerId : DemoOwnerId;
        _now = DateTime.UtcNow;
        _log.LogInformation("🧹 SandboxDataSeeder — limpieza (OwnerId={OwnerId})", _ownerId);
        _db.IgnoreTenantFilter();
        try
        {
            // ── Orden estricto inverso de FK ──────────────────────────────────
            // Las tablas HIJO se filtran siempre por OwnerId para garantizar que
            // no quede ninguna fila colgante independientemente de su SourceSystem.
            // Las tablas RAÍZ (sin OwnerId o sin FK entrante) usan SourceSystem==Seed.

            // Nivel 5 – hojas de TreatmentPlants
            await _db.TreatmentPlantResidues
                .Where(x => x.TreatmentPlant.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.PlantEnergies
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.TreatmentPlants
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 4 – hojas de EntryPlants / EntryCACs
            await _db.EntryPlantResidues
                .Where(x => x.EntryPlant.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryPlants
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACResidues
                .Where(x => x.EntryCAC.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACs
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 3 – hojas de WasteMoves / ServiceOrders / Incidents
            await _db.WasteMoveResidues
                .Where(x => x.WasteMove.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.WasteMoves
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Incidents
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ServiceOrders
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 2 – Settlements / MarketShares / Declarations / Agreements
            await _db.SettlementLines
                .Where(x => x.Settlement.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Settlements
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.MarketShares
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Products
                .Where(x => x.ProductDeclaration.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ProductDeclarations
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.AgreementDocuments
                .Where(x => x.Agreement.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Agreements
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 1 – DUM, Ecomodulación, Regulatory, Residues
            await _db.DumRestrictionRules
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.DumZones
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRules
                .Where(x => x.RuleSet.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRuleSets
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.RegulatoryTargets
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ProductSpecs
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Residues
                .Where(x => x.SourceSystem == Seed).ExecuteDeleteAsync(ct);

            // Nivel 0 – usuarios seed y entidades raíz
            var adminProfileId = await _db.UserProfiles
                .IgnoreQueryFilters()
                .Where(p => p.Reference == "ADMIN")
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);
            await _db.AppUsers
                .IgnoreQueryFilters()
                .Where(u => u.OwnerId == _ownerId
                         && u.IdProfile != adminProfileId
                         && u.Login != null
                         && u.Login.EndsWith("@greentransit.dev"))
                .ExecuteDeleteAsync(ct);
            await _db.BusinessEntities
                .Where(x => x.SourceSystem == Seed).ExecuteDeleteAsync(ct);

            // Catálogos seed
            await _db.TreatmentOperations
                .Where(x => x.CreatedAt >= _now.Date).ExecuteDeleteAsync(ct);
            await _db.LerCodes
                .Where(x => !x.IsActive).ExecuteDeleteAsync(ct);

            _log.LogInformation("✅ SandboxDataSeeder — limpieza completada");
        }
        finally
        {
            _db.RestoreTenantFilter();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CleanAllAsync — igual que CleanAsync pero elimina TODOS los datos del tenant,
    // no solo los marcados con SourceSystem='SEED'. No toca catálogos globales.
    // ─────────────────────────────────────────────────────────────────────────
    public async Task CleanAllAsync(CancellationToken ct = default)
    {
        _ownerId = _currentUser.OwnerId != Guid.Empty ? _currentUser.OwnerId : DemoOwnerId;
        _now = DateTime.UtcNow;
        _log.LogInformation("🧹 SandboxDataSeeder — limpieza total (OwnerId={OwnerId})", _ownerId);
        _db.IgnoreTenantFilter();
        try
        {
            // Nivel 5 – hojas de TreatmentPlants
            await _db.TreatmentPlantResidues
                .Where(x => x.TreatmentPlant.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.PlantEnergies
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.TreatmentPlants
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 4 – hojas de EntryPlants / EntryCACs
            await _db.EntryPlantResidues
                .Where(x => x.EntryPlant.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryPlants
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACResidues
                .Where(x => x.EntryCAC.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACs
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 3 – hojas de WasteMoves / ServiceOrders / Incidents
            await _db.WasteMoveResidues
                .Where(x => x.WasteMove.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.WasteMoves
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Incidents
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ServiceOrders
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 2 – Settlements / MarketShares / Declarations / Agreements
            await _db.SettlementLines
                .Where(x => x.Settlement.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Settlements
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.MarketShares
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Products
                .Where(x => x.ProductDeclaration.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ProductDeclarations
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.AgreementDocuments
                .Where(x => x.Agreement.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Agreements
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            // Nivel 1 – DUM, Ecomodulación, Regulatory, Residues del tenant
            await _db.DumRestrictionRules
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.DumZones
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRules
                .Where(x => x.RuleSet.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRuleSets
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.RegulatoryTargets
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ProductSpecs
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            // Residues: se filtran por IdProducer vinculado a entidades del tenant
            var tenantEntityIds = await _db.BusinessEntities
                .Where(e => _db.AppUsers.IgnoreQueryFilters()
                    .Any(u => u.OwnerId == _ownerId))
                .Select(e => e.Id)
                .ToListAsync(ct);
            await _db.Residues
                .Where(x => x.IdProducer != null && tenantEntityIds.Contains(x.IdProducer.Value))
                .ExecuteDeleteAsync(ct);

            // Nivel 0 – usuarios no-admin y entidades del tenant
            var adminProfileId = await _db.UserProfiles
                .IgnoreQueryFilters()
                .Where(p => p.Reference == "ADMIN")
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);
            await _db.AppUsers
                .IgnoreQueryFilters()
                .Where(u => u.OwnerId == _ownerId && u.IdProfile != adminProfileId)
                .ExecuteDeleteAsync(ct);
            // BusinessEntities no tienen OwnerId; se eliminan las referenciadas por el tenant
            // a través de Agreements, ServiceOrders, etc. que ya fueron borrados en pasos anteriores.
            // Solo se borran las que tienen SourceSystem del tenant mediante AppUsers existentes.
            await _db.BusinessEntities
                .Where(x => x.IdUser != 0 &&
                    _db.AppUsers.IgnoreQueryFilters().Any(u => u.OwnerId == _ownerId && u.Id == x.IdUser))
                .ExecuteDeleteAsync(ct);

            _log.LogInformation("✅ SandboxDataSeeder — limpieza total completada");
        }
        finally
        {
            _db.RestoreTenantFilter();
        }
    }

    // =========================================================================
    // FASE 0 — Catálogos
    // =========================================================================
    private async Task Phase0_CataloguesAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await SeedGeographyAsync(ct);
        await SeedTreatmentOperationsAsync(ct);
        await SeedLerCodesAsync(ct);
        await SeedEmissionFactorSetAsync(ct);
        _log.LogInformation("  Fase 0 completada en {Ms}ms", sw.ElapsedMilliseconds);
    }

    private async Task SeedGeographyAsync(CancellationToken ct)
    {
        // Solo insertar si no existe ningún municipio de los usados en el sandbox
        if (await _db.Municipalities.AnyAsync(m => m.Code == "28079", ct)) return;

        // ── País ─────────────────────────────────────────────────────────────
        var country = await _db.Countries.FirstOrDefaultAsync(c => c.Code == "ES", ct);
        if (country is null)
        {
            country = new Country
            {
                Ref  = "España",
                Code = "ES",
                MunicipalityDataLinkedRequired = false,
                MunicipalityDataRequired       = false,
                UE   = true
            };
            _db.Countries.Add(country);
            await _db.SaveChangesAsync(ct);
        }

        // ── Comunidades Autónomas (TerritoryState) ────────────────────────────
        // Code = código autonómico INE de 2 dígitos
        var stateDefs = new (string Ref, string Code)[]
        {
            ("Aragón",                    "AR"),
            ("Comunidad de Madrid",       "MD"),
            ("Cataluña",                  "CT"),
            ("Andalucía",                 "AN"),
            ("Comunitat Valenciana",      "VC"),
            ("País Vasco",                "PV"),
            ("Región de Murcia",          "MC"),
            ("Castilla y León",           "CL"),
            ("Illes Balears",             "IB"),
            ("Cantabria",                 "CB"),
            ("Comunidad Foral de Navarra","NC"),
            ("La Rioja",                  "RI"),
            ("Principado de Asturias",    "AS"),
            ("Galicia",                   "GA"),
            ("Castilla-La Mancha",        "CM"),
            ("Extremadura",               "EX"),
        };

        var existingStateCodes = await _db.TerritoryStates
            .Where(s => s.IdCountry == country.Id)
            .Select(s => s.Code)
            .ToHashSetAsync(ct);

        var states = stateDefs
            .Where(d => !existingStateCodes.Contains(d.Code))
            .Select(d => new TerritoryState { IdCountry = country.Id, Ref = d.Ref, Code = d.Code, Name = d.Ref })
            .ToList();
        _db.TerritoryStates.AddRange(states);
        await _db.SaveChangesAsync(ct);

        var stateByCode = await _db.TerritoryStates
            .Where(s => s.IdCountry == country.Id)
            .ToDictionaryAsync(s => s.Code, ct);

        // ── Provincias ────────────────────────────────────────────────────────
        // (stateCode, provinceCode, provinceName)
        var provinceDefs = new (string StateCode, string Code, string Name)[]
        {
            ("MD", "28", "Madrid"),
            ("CT", "08", "Barcelona"),
            ("AN", "41", "Sevilla"),
            ("VC", "46", "Valencia"),
            ("PV", "48", "Bizkaia"),
            ("AN", "29", "Málaga"),
            ("MC", "30", "Murcia"),
            ("CL", "47", "Valladolid"),
            ("IB", "07", "Illes Balears"),
            ("VC", "03", "Alicante"),
            ("AN", "14", "Córdoba"),
            ("AN", "18", "Granada"),
            ("CB", "39", "Cantabria"),
            ("NC", "31", "Navarra"),
            ("RI", "26", "La Rioja"),
            ("AS", "33", "Asturias"),
            ("GA", "15", "A Coruña"),
            ("CM", "45", "Toledo"),
            ("EX", "06", "Badajoz"),
            ("AR", "50", "Zaragoza"),
        };

        var existingProvCodes = await _db.Provinces.Select(p => p.Code).ToHashSetAsync(ct);
        var provinces = provinceDefs
            .Where(d => !existingProvCodes.Contains(d.Code) && stateByCode.ContainsKey(d.StateCode))
            .Select(d => new Province
            {
                IdState = stateByCode[d.StateCode].Id,
                Ref     = d.Name,
                Code    = d.Code,
                Name    = d.Name
            })
            .ToList();
        _db.Provinces.AddRange(provinces);
        await _db.SaveChangesAsync(ct);

        var provById = await _db.Provinces
            .Where(p => provinceDefs.Select(d => d.Code).Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, ct);

        // ── Municipios ────────────────────────────────────────────────────────
        var municipalityDefs = new (string ProvCode, string Code, string Name)[]
        {
            ("28", "28079", "Madrid"),
            ("08", "08019", "Barcelona"),
            ("41", "41091", "Sevilla"),
            ("46", "46250", "Valencia"),
            ("48", "48020", "Bilbao"),
            ("29", "29067", "Málaga"),
            ("30", "30030", "Murcia"),
            ("47", "47186", "Valladolid"),
            ("07", "07040", "Palma"),
            ("03", "03014", "Alicante"),
            ("14", "14021", "Córdoba"),
            ("18", "18087", "Granada"),
            ("39", "39075", "Santander"),
            ("31", "31201", "Pamplona"),
            ("26", "26089", "Logroño"),
            ("33", "33044", "Oviedo"),
            ("15", "15030", "A Coruña"),
            ("45", "45168", "Toledo"),
            ("06", "06015", "Badajoz"),
            ("50", "50297", "Zaragoza"),
        };

        var existingMunCodes = await _db.Municipalities.Select(m => m.Code).ToHashSetAsync(ct);
        var municipalities = municipalityDefs
            .Where(d => !existingMunCodes.Contains(d.Code) && provById.ContainsKey(d.ProvCode))
            .Select(d => new Municipality
            {
                IdProvince = provById[d.ProvCode].Id,
                Code       = d.Code,
                Name       = d.Name
            })
            .ToList();
        _db.Municipalities.AddRange(municipalities);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("    Geography sandbox: {N} municipios insertados", municipalities.Count);
    }

    private async Task SeedTreatmentOperationsAsync(CancellationToken ct)
    {
        var existing = await _db.TreatmentOperations.Select(x => x.Code).ToHashSetAsync(ct);
        var ops = BuildTreatmentOperations().Where(o => !existing.Contains(o.Code)).ToList();
        if (ops.Count == 0) return;
        _db.TreatmentOperations.AddRange(ops);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("    TreatmentOperations insertadas: {N}", ops.Count);
    }

    private async Task SeedLerCodesAsync(CancellationToken ct)
    {
        var existing = await _db.LerCodes.Select(x => x.Code).ToHashSetAsync(ct);
        var codes = BuildLerCodes().Where(l => !existing.Contains(l.Code)).ToList();
        if (codes.Count == 0) return;
        _db.LerCodes.AddRange(codes);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("    LerCodes insertados: {N}", codes.Count);
    }

    private async Task SeedEmissionFactorSetAsync(CancellationToken ct)
    {
        if (await _db.EmissionFactorSets.AnyAsync(ct)) return;
        var set = new EmissionFactorSet
        {
            Id            = SeedGuid("efs", 1),
            OwnerId       = _ownerId,
            FactorSetName = "Set Demo GreenTransit",
            Version       = "1.0",
            Status        = "Active",
            ValidFrom     = _now.AddYears(-1),
            Publisher     = "GreenTransit Seed",
            CreatedAt     = _now,
            UpdatedAt     = _now,
            IdUser        = SeedUser
        };
        _db.EmissionFactorSets.Add(set);
        foreach (var ef in BuildEmissionFactors(set.Id, _now))
            _db.EmissionFactors.Add(ef);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("    EmissionFactorSet insertado");
    }

    // =========================================================================
    // FASE 1 — Entidades
    // =========================================================================
    private async Task Phase1_EntitiesAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.BusinessEntities.AnyAsync(x => x.SourceSystem == Seed, ct))
        {
            _log.LogInformation("  Fase 1 — skip (ya existen entidades SEED)");
            return;
        }

        var entities = BuildEntities(_ownerId, _now);
        _db.BusinessEntities.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 1 completada — {N} entidades en {Ms}ms",
            entities.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 2 — Residuos
    // =========================================================================
    private async Task Phase2_ResiduesAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.Residues.AnyAsync(x => x.SourceSystem == Seed, ct))
        {
            _log.LogInformation("  Fase 2 — skip");
            return;
        }

        var lerIds = await _db.LerCodes.Select(x => new { x.Id, x.Code }).ToListAsync(ct);
        var prodIds = await _db.BusinessEntities
            .Where(x => x.EntityRole == "Producer" && x.SourceSystem == Seed)
            .Select(x => x.Id).ToListAsync(ct);

        var residues = BuildResidues(lerIds.ToDictionary(x => x.Code, x => x.Id), prodIds);
        _db.Residues.AddRange(residues);
        await _db.SaveChangesAsync(ct);

        // Crear registros ProductSpec enlazados a los residuos con ResidueType=="ProductSpec"
        var psResidues = residues.Where(r => r.ResidueType == "ProductSpec").ToList();
        var productSpecs = psResidues.Select((r, i) => new ProductSpec
        {
            Id           = SeedGuid("ps", i + 1),
            OwnerId      = _ownerId,
            ProductRef   = $"PROD-SPEC-{i + 1:D4}",
            IdResidue    = r.Id,
            IdProducer   = r.IdProducer,
            CategoryRef  = r.ProductCategory,
            SourceSystem = Seed,
            Version      = 1,
            CreatedAt    = _now,
            UpdatedAt    = _now,
            IdUser       = SeedUser
        }).ToList();
        _db.ProductSpecs.AddRange(productSpecs);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("  Fase 2 completada — {N} residuos, {PS} ProductSpecs en {Ms}ms",
            residues.Count, productSpecs.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 3 — Contratos
    // =========================================================================
    private async Task Phase3_AgreementsAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.Agreements.AnyAsync(x => x.SourceSystem == Seed, ct))
        {
            _log.LogInformation("  Fase 3 — skip");
            return;
        }

        var scraps = await _db.BusinessEntities
            .Where(x => x.EntityRole == "SCRAP" && x.SourceSystem == Seed)
            .Select(x => x.Id).ToListAsync(ct);
        var publicEntities = await _db.BusinessEntities
            .Where(x => x.EntityRole == "PublicEntity" && x.SourceSystem == Seed)
            .Select(x => x.Id).ToListAsync(ct);
        var coordinator = await _db.BusinessEntities
            .Where(x => x.EntityRole == "Coordinator" && x.SourceSystem == Seed)
            .Select(x => x.Id).FirstOrDefaultAsync(ct);

        var (agreements, docs) = BuildAgreements(scraps, publicEntities, coordinator, _ownerId, _now);
        _db.Agreements.AddRange(agreements);
        await _db.SaveChangesAsync(ct);
        _db.AgreementDocuments.AddRange(docs);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 3 completada — {A} acuerdos, {D} docs en {Ms}ms",
            agreements.Count, docs.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 4 — Operación logística
    // =========================================================================
    private async Task Phase4_OperationsAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.WasteMoves.AnyAsync(x => x.SourceSystem == Seed && x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 4 — skip");
            return;
        }

        // Cargar FKs necesarias
        var producers = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "Producer")
            .Select(x => x.Id).ToListAsync(ct);
        var publicEnts = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "PublicEntity")
            .Select(x => x.Id).ToListAsync(ct);
        var carriers = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "Carrier")
            .Select(x => x.Id).ToListAsync(ct);
        var plants = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "Plant")
            .Select(x => x.Id).ToListAsync(ct);
        var cacs = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "CAC")
            .Select(x => x.Id).ToListAsync(ct);
        var scraps = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "SCRAP")
            .Select(x => x.Id).ToListAsync(ct);
        var opTransfers = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "OperatorTransfer")
            .Select(x => x.Id).ToListAsync(ct);
        var lerIds = await _db.LerCodes.Select(x => x.Id).ToListAsync(ct);
        var wasteResidueIds = await _db.Residues
            .Where(x => x.SourceSystem == Seed && x.ResidueType == "Waste")
            .Select(x => x.Id).ToListAsync(ct);
        var treatOpIds = await _db.TreatmentOperations
            .Where(x => x.IsActive).Select(x => x.Id).ToListAsync(ct);

        var (serviceOrders, soResidues, wasteMoves, wmResidues) = BuildOperations(
            producers, publicEnts, carriers, plants, cacs, scraps, opTransfers,
            lerIds, wasteResidueIds, treatOpIds, _ownerId, _now);

        _db.ServiceOrders.AddRange(serviceOrders);
        await _db.SaveChangesAsync(ct);
        _db.ServiceOrderResidues.AddRange(soResidues);
        await _db.SaveChangesAsync(ct);
        _db.WasteMoves.AddRange(wasteMoves);
        await _db.SaveChangesAsync(ct);
        _db.WasteMoveResidues.AddRange(wmResidues);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 4 completada — {SO} SO, {SOR} SOR, {WM} WM, {WMR} WMR en {Ms}ms",
            serviceOrders.Count, soResidues.Count, wasteMoves.Count, wmResidues.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 5 — Entradas y tratamiento
    // =========================================================================
    private async Task Phase5_EntriesAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.EntryPlants.AnyAsync(x => x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 5 — skip");
            return;
        }

        var wasteMoves = await _db.WasteMoves
            .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId)
            .Select(x => new { x.Id, x.WasteMoveReference, x.ServiceOrderId, x.PlantEntryDate, x.ServiceStatus })
            .ToListAsync(ct);

        var wasteResidueIds = await _db.Residues
            .Where(x => x.SourceSystem == Seed && x.ResidueType == "Waste")
            .Select(x => x.Id).ToListAsync(ct);
        var treatOpIds = await _db.TreatmentOperations
            .Where(x => x.IsActive && x.OperationType == "Recovery")
            .Select(x => x.Id).ToListAsync(ct);
        var productResidueIds = await _db.Residues
            .Where(x => x.SourceSystem == Seed && x.ResidueType == "Product")
            .Select(x => x.Id).ToListAsync(ct);

        var incidents = BuildIncidents(wasteMoves.Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId)).ToList(), _ownerId, _now);
        _db.Incidents.AddRange(incidents);
        await _db.SaveChangesAsync(ct);

        var closedIncidentIds = incidents.Where(x => x.ClosedAt != null).Select(x => x.Id).ToList();

        var (entryCACs, cacResidues) = BuildEntryCACs(
            wasteMoves.Where(x => x.ServiceStatus is "EN_CAC" or "EN_PLANTA" or "RECOGIDO" or "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.PlantEntryDate)).ToList(),
            wasteResidueIds, _ownerId, _now);

        var (entryPlants, plantResidues) = BuildEntryPlants(
            wasteMoves.Where(x => x.ServiceStatus is "EN_PLANTA" or "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId, x.PlantEntryDate)).ToList(),
            wasteResidueIds, _ownerId, _now);

        var (treatPlants, treatResidues) = BuildTreatmentPlants(
            wasteMoves.Where(x => x.ServiceStatus == "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId, x.PlantEntryDate)).ToList(),
            treatOpIds, wasteResidueIds, productResidueIds, closedIncidentIds, _ownerId, _now);

        _db.EntryCACs.AddRange(entryCACs);
        await _db.SaveChangesAsync(ct);
        _db.EntryCACResidues.AddRange(cacResidues);
        await _db.SaveChangesAsync(ct);
        _db.EntryPlants.AddRange(entryPlants);
        await _db.SaveChangesAsync(ct);
        _db.EntryPlantResidues.AddRange(plantResidues);
        await _db.SaveChangesAsync(ct);
        _db.TreatmentPlants.AddRange(treatPlants);
        await _db.SaveChangesAsync(ct);
        _db.TreatmentPlantResidues.AddRange(treatResidues);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("  Fase 5 completada — {EC} EntryCAC, {EP} EntryPlant, {TP} TreatmentPlant, {I} Incidents en {Ms}ms",
            entryCACs.Count, entryPlants.Count, treatPlants.Count, incidents.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 6 — Economía y cuotas
    // =========================================================================
    private async Task Phase6_EconomicsAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.Settlements.AnyAsync(x => x.SourceSystem == Seed && x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 6 — skip");
            return;
        }

        var agreements = await _db.Agreements
            .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId)
            .Select(x => new { x.Id, x.IdScrap, x.IdPublicEntity })
            .ToListAsync(ct);
        var lerIds = await _db.LerCodes.Select(x => x.Id).ToListAsync(ct);
        var scraps = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "SCRAP")
            .Select(x => x.Id).ToListAsync(ct);

        var (settlements, lines) = BuildSettlements(
            agreements.Select(a => (a.Id, a.IdScrap, a.IdPublicEntity)).ToList(), lerIds, _ownerId, _now);
        var marketShares = BuildMarketShares(scraps, _ownerId, _now);

        _db.Settlements.AddRange(settlements);
        await _db.SaveChangesAsync(ct);
        _db.SettlementLines.AddRange(lines);
        await _db.SaveChangesAsync(ct);
        _db.MarketShares.AddRange(marketShares);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("  Fase 6 completada — {S} Settlements, {L} Lines, {M} MarketShares en {Ms}ms",
            settlements.Count, lines.Count, marketShares.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 7 — Declaraciones de producción
    // =========================================================================
    private async Task Phase7_DeclarationsAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (await _db.ProductDeclarations.AnyAsync(x => x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 7 — skip");
            return;
        }

        var producers = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "Producer")
            .Select(x => new { x.Id, x.CenterCode }).ToListAsync(ct);
        var productResidueIds = await _db.Residues
            .Where(x => x.SourceSystem == Seed && x.ResidueType == "Product")
            .Select(x => x.Id).ToListAsync(ct);

        var (declarations, products) = BuildProductDeclarations(
            producers.Select(p => (p.Id, p.CenterCode ?? "CC")).ToList(), productResidueIds, _ownerId, _now);

        _db.ProductDeclarations.AddRange(declarations);
        await _db.SaveChangesAsync(ct);
        _db.Products.AddRange(products);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("  Fase 7 completada — {D} declaraciones, {P} productos en {Ms}ms",
            declarations.Count, products.Count, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 8 — Usuarios demo (uno por entidad seeded)
    // =========================================================================
    private async Task Phase8_UsersAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Comprobar si ya existen usuarios seed (por email @greentransit.dev no-ADMIN)
        var adminProfileId = await _db.UserProfiles
            .IgnoreQueryFilters()
            .Where(p => p.Reference == "ADMIN")
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        var alreadyExists = await _db.AppUsers
            .IgnoreQueryFilters()
            .AnyAsync(u => u.OwnerId == _ownerId
                        && u.IdProfile != adminProfileId
                        && u.Login != null
                        && u.Login.EndsWith("@greentransit.dev"), ct);

        if (alreadyExists)
        {
            _log.LogInformation("  Fase 8 — usuarios seed ya existen, verificando sincronización de emails de entidades");
            await SyncEntityEmailsAsync(ct);
            return;
        }

        // Mapa EntityRole → ProfileReference
        var roleToProfile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Producer",        "PRODUCER"        },
            { "SCRAP",           "SCRAP"            },
            { "Carrier",         "CARRIER"          },
            { "Plant",           "PLANT_OP"         },
            { "CAC",             "CAC_OP"           },
            { "PublicEntity",    "PUBLIC_ENT"       },
            { "Coordinator",     "COORDINATOR"      },
            { "OperatorTransfer","DISPATCH_OFFICE"  },
        };

        // Cargar perfiles disponibles
        var profiles = await _db.UserProfiles
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Reference, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Cargar entidades seeded (completas para poder actualizar su Email)
        var entities = await _db.BusinessEntities
            .Where(e => e.SourceSystem == Seed)
            .ToListAsync(ct);

        var users = new List<AppUser>();
        foreach (var entity in entities)
        {
            if (!roleToProfile.TryGetValue(entity.EntityRole ?? "", out var profileRef)) continue;
            if (!profiles.TryGetValue(profileRef, out var profileId)) continue;

            // Normalizar nombre → slug para email: "Productor Demo 01" → "productor.demo.01"
            var slug = NormalizeSlug(entity.Name ?? $"entity{entity.Id}");
            var email = $"{slug}@greentransit.dev";

            // Sincronizar el Email de la entidad para que FindEntityIdByEmailAsync lo encuentre
            if (entity.Email != email)
            {
                entity.Email     = email;
                entity.UpdatedAt = _now;
            }

            users.Add(new AppUser
            {
                Login        = email,
                Email        = email,
                CompleteName = entity.Name,
                IdProfile    = profileId,
                OwnerId      = _ownerId,
                IsActive     = true,
                CreateDate   = _now,
            });
        }

        if (users.Count > 0)
        {
            _db.AppUsers.AddRange(users);
            await _db.SaveChangesAsync(ct);
        }

        _log.LogInformation("  Fase 8 completada — {N} usuarios demo en {Ms}ms",
            users.Count, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Sincroniza el Email de cada entidad seeded con el email @greentransit.dev
    /// derivado de su nombre, para que FindEntityIdByEmailAsync pueda resolverla.
    /// </summary>
    private async Task SyncEntityEmailsAsync(CancellationToken ct)
    {
        var entities = await _db.BusinessEntities
            .Where(e => e.SourceSystem == Seed)
            .ToListAsync(ct);

        int updated = 0;
        foreach (var entity in entities)
        {
            var slug  = NormalizeSlug(entity.Name ?? $"entity{entity.Id}");
            var email = $"{slug}@greentransit.dev";
            if (entity.Email == email) continue;
            entity.Email     = email;
            entity.UpdatedAt = _now;
            updated++;
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("  SyncEntityEmails — {N} entidades actualizadas", updated);
        }
    }

    /// <summary>
    /// Convierte un nombre de entidad en un slug válido para email.
    /// "Productor Demo 01 (SL)" → "productor.demo.01.sl"
    /// </summary>
    private static string NormalizeSlug(string name)
    {
        // Eliminar caracteres no alfanuméricos excepto espacios, reemplazar espacios por puntos
        var sb = new System.Text.StringBuilder();
        bool lastWasDot = false;
        foreach (var ch in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasDot = false;
            }
            else if (!lastWasDot && sb.Length > 0)
            {
                sb.Append('.');
                lastWasDot = true;
            }
        }
        // Quitar punto final si lo hay
        if (sb.Length > 0 && sb[^1] == '.')
            sb.Remove(sb.Length - 1, 1);
        return sb.ToString();
    }

    // =========================================================================
    // FASE 8b — Usuarios sandbox _uc con conectores EDC
    // =========================================================================
    /// <summary>
    /// Crea los 11 usuarios sandbox con sufijo _uc para cada perfil del ecosistema,
    /// junto con su registro en UserEDCConnector. Idempotente: salta si ya existen.
    /// </summary>
    private async Task Phase8b_SandboxUsersUcAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Definición de usuarios sandbox _uc
        var sandboxUsers = new[]
        {
            (Login: "ayuntamiento_uc",      Profile: "PUBLIC_ENT",      Name: "Ayuntamiento UC (Sandbox)",        EDCServer: "ecoucayuntamiento.ecodatanetconn3.dataspace.wastenode.com",        EDCId: "eco_uc_ayuntamiento"),
            (Login: "ofiasignacion_uc",     Profile: "DISPATCH_OFFICE", Name: "Oficina de Asignación UC",         EDCServer: "ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com",        EDCId: "eco_uc_ofiasignacion"),
            (Login: "scrapa_uc",            Profile: "SCRAP",           Name: "SCRAP A UC",                       EDCServer: "ecoucscrapa.ecodatanetconn3.dataspace.wastenode.com",               EDCId: "eco_uc_scrapa"),
            (Login: "scrapb_uc",            Profile: "SCRAP",           Name: "SCRAP B UC",                       EDCServer: "ecoucscrapb.ecodatanetconn3.dataspace.wastenode.com",               EDCId: "eco_uc_scrapb"),
            (Login: "transportista_uc",     Profile: "CARRIER",         Name: "Transportista UC",                 EDCServer: "ecouctransportista.ecodatanetconn3.dataspace.wastenode.com",        EDCId: "eco_uc_transportista"),
            (Login: "clusterlogistico_uc",  Profile: "COORDINATOR",     Name: "Clúster Logístico UC",             EDCServer: "ecoucclusterlogistico.ecodatanetconn3.dataspace.wastenode.com",     EDCId: "eco_uc_clusterlogistico"),
            (Login: "puntorecogida_uc",     Profile: "CAC_OP",          Name: "Punto de Recogida UC",             EDCServer: "ecoucpuntorecogida.ecodatanetconn3.dataspace.wastenode.com",        EDCId: "eco_uc_puntorecogida"),
            (Login: "certificador_uc",      Profile: "CERTIFIER",       Name: "Certificador UC (AENOR Sandbox)",  EDCServer: "ecouccertificador.ecodatanetconn3.dataspace.wastenode.com",         EDCId: "eco_uc_certificador"),
            (Login: "productor_uc",         Profile: "PRODUCER",        Name: "Productor UC",                     EDCServer: "ecoucproductor.ecodatanetconn3.dataspace.wastenode.com",            EDCId: "eco_uc_productor"),
            (Login: "regulador_uc",         Profile: "REGULATOR",       Name: "Regulador UC (Sandbox)",           EDCServer: "ecoucregulador.ecodatanetconn3.dataspace.wastenode.com",            EDCId: "eco_uc_regulador"),
            (Login: "plantatratamiento_uc", Profile: "PLANT_OP",        Name: "Planta de Tratamiento UC",         EDCServer: "ecoucplantatratamiento.ecodatanetconn3.dataspace.wastenode.com",    EDCId: "eco_uc_plantatratamiento"),
        };

        // Cargar perfiles disponibles
        var profiles = await _db.UserProfiles
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Reference, p => p.Id, StringComparer.OrdinalIgnoreCase, ct);

        // Cargar logins existentes para idempotencia
        var existingLogins = await _db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.OwnerId == _ownerId)
            .Select(u => u.Login)
            .ToHashSetAsync(ct);

        var newUsers = new List<AppUser>();
        foreach (var def in sandboxUsers)
        {
            if (existingLogins.Contains(def.Login)) continue;
            if (!profiles.TryGetValue(def.Profile, out var profileId)) continue;

            newUsers.Add(new AppUser
            {
                Login        = def.Login,
                Email        = $"{def.Login}@sandbox.greentransit.es",
                CompleteName = def.Name,
                IdProfile    = profileId,
                OwnerId      = _ownerId,
                IsActive     = true,
                CreateDate   = _now,
            });
        }

        if (newUsers.Count > 0)
        {
            _db.AppUsers.AddRange(newUsers);
            await _db.SaveChangesAsync(ct);
        }

        // Insertar conectores EDC para los usuarios _uc que no tengan uno
        int edcAdded = 0;
        foreach (var def in sandboxUsers)
        {
            var user = await _db.AppUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.OwnerId == _ownerId && u.Login == def.Login, ct);
            if (user is null) continue;

            var alreadyHasConnector = await _db.UserEDCConnectors
                .AnyAsync(c => c.UserId == user.Id, ct);
            if (alreadyHasConnector) continue;

            _db.UserEDCConnectors.Add(new UserEDCConnector
            {
                UserId         = user.Id,
                EDCServerName  = def.EDCServer,
                EDCConnectorId = def.EDCId,
                ApiKey         = "ecodatanet",
            });
            edcAdded++;
        }

        if (edcAdded > 0)
            await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "  Fase 8b completada — {U} usuarios _uc, {E} conectores EDC en {Ms}ms",
            newUsers.Count, edcAdded, sw.ElapsedMilliseconds);
    }

    // =========================================================================
    // FASE 9 — Zonas DUM y conjuntos de ecomodulación
    // =========================================================================
    private async Task Phase9_DumAndEcoAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (await _db.DumZones.AnyAsync(x => x.SourceSystem == Seed && x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 9 — skip (DUM y Eco ya existen)");
            return;
        }

        var (zones, restrictions) = BuildDumZones(_ownerId, _now);
        _db.DumZones.AddRange(zones);
        await _db.SaveChangesAsync(ct);
        _db.DumRestrictionRules.AddRange(restrictions);
        await _db.SaveChangesAsync(ct);

        var (ruleSets, ecoRules) = BuildEcoModulation(_ownerId, _now);
        _db.EcoModulationRuleSets.AddRange(ruleSets);
        await _db.SaveChangesAsync(ct);
        _db.EcoModulationRules.AddRange(ecoRules);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "  Fase 9 completada — {Z} DumZones, {R} Restricciones, {RS} EcoRuleSets, {ER} EcoRules en {Ms}ms",
            zones.Count, restrictions.Count, ruleSets.Count, ecoRules.Count, sw.ElapsedMilliseconds);
    }

    // ── DumZones + DumRestrictionRules ────────────────────────────────────────
    private static (List<DumZone>, List<DumRestrictionRule>) BuildDumZones(Guid ownerId, DateTime now)
    {
        var zoneDefs = new[]
        {
            ("DUM-MAD-001", "Zona DUM Centro Madrid",      "Madrid",      41.4015, -3.7039, 0.015),
            ("DUM-BCN-001", "Zona DUM Eixample Barcelona", "Barcelona",   41.3879,  2.1699, 0.012),
            ("DUM-VAL-001", "Zona DUM L'Exemple Valencia", "Valencia",    39.4699, -0.3763, 0.013),
            ("DUM-SEV-001", "Zona DUM Centro Sevilla",     "Sevilla",     37.3891, -5.9845, 0.014),
            ("DUM-ZGZ-001", "Zona DUM Casco Zaragoza",     "Zaragoza",    41.6488, -0.8891, 0.011),
            ("DUM-BIL-001", "Zona DUM Casco Bilbao",       "Bilbao",      43.2630, -2.9350, 0.010),
            ("DUM-MAL-001", "Zona DUM Centro Málaga",      "Málaga",      36.7213, -4.4214, 0.012),
            ("DUM-VLD-001", "Zona DUM Centro Valladolid",  "Valladolid",  41.6523, -4.7245, 0.009),
        };

        var zones = new List<DumZone>();
        var rules = new List<DumRestrictionRule>();

        for (int i = 0; i < zoneDefs.Length; i++)
        {
            var (code, name, city, lat, lon, delta) = zoneDefs[i];
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var geo = $"{{\"type\":\"Polygon\",\"coordinates\":[[" +
                      $"[{(lon - delta).ToString("F6", inv)},{(lat - delta).ToString("F6", inv)}]," +
                      $"[{(lon + delta).ToString("F6", inv)},{(lat - delta).ToString("F6", inv)}]," +
                      $"[{(lon + delta).ToString("F6", inv)},{(lat + delta).ToString("F6", inv)}]," +
                      $"[{(lon - delta).ToString("F6", inv)},{(lat + delta).ToString("F6", inv)}]," +
                      $"[{(lon - delta).ToString("F6", inv)},{(lat - delta).ToString("F6", inv)}]]]}}";

            var zoneId = SeedGuid("dz", i + 1);
            zones.Add(new DumZone
            {
                Id           = zoneId,
                OwnerId      = ownerId,
                ZoneCode     = code,
                Name         = name,
                Description  = $"Zona DUM — {city}",
                Status       = "Active",
                GeometryJson = geo,
                SourceSystem = Seed,
                Version      = 1,
                Hash         = $"seed-{code}",
                CreatedAt    = now,
                UpdatedAt    = now,
                IdUser       = SeedUser,
            });

            // Regla 1: restricción horaria nocturna
            rules.Add(new DumRestrictionRule
            {
                Id             = SeedGuid("drr", i * 2 + 1),
                OwnerId        = ownerId,
                ZoneId         = zoneId,
                RuleCode       = $"{code}-R01",
                Status         = "Active",
                ValidFrom      = now.AddMonths(-6),
                ConditionsJson = "{\"days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"],\"startHour\":21,\"endHour\":7,\"vehicleTypes\":[\"Camión 12t\",\"Camión 26t\"]}",
                ActionType     = "Deny",
                ActionReason   = "Restricción horaria nocturna",
                SourceSystem   = Seed,
                Version        = 1,
                CreatedAt      = now,
                UpdatedAt      = now,
            });

            // Regla 2: límite de tonelaje
            rules.Add(new DumRestrictionRule
            {
                Id             = SeedGuid("drr", i * 2 + 2),
                OwnerId        = ownerId,
                ZoneId         = zoneId,
                RuleCode       = $"{code}-R02",
                Status         = "Active",
                ValidFrom      = now.AddMonths(-6),
                ConditionsJson = "{\"maxWeightTon\":3.5,\"vehicleTypes\":[\"Camión 26t\"]}",
                ActionType     = "Restrict",
                ActionReason   = "Límite de tonelaje en zona urbana",
                SourceSystem   = Seed,
                Version        = 1,
                CreatedAt      = now,
                UpdatedAt      = now,
            });
        }

        return (zones, rules);
    }

    // ── EcoModulationRuleSets + EcoModulationRules ────────────────────────────
    private static (List<EcoModulationRuleSet>, List<EcoModulationRule>) BuildEcoModulation(Guid ownerId, DateTime now)
    {
        // 3 conjuntos: estándar, reducido y premium
        var setDefs = new[]
        {
            ("ECO-2026-STD",  "Ecomodulación Estándar 2026"),
            ("ECO-2026-RED",  "Ecomodulación Reducida 2026"),
            ("ECO-2026-PREM", "Ecomodulación Premium 2026"),
        };

        // Categorías de producto (int) y sus etiquetas
        var categories = new[] { 1, 2, 3, 4, 5 };
        var catNames   = new[] { "Envases", "RAEE", "Municipal", "Metales", "Construccion" };

        // Impacto por (conjunto × categoría): (valor, tipo)
        var impacts = new (decimal Value, string Type)[3][]
        {
            [ (0m,"None"),    (0m,"None"),       (0m,"None"),      (0m,"None"),      (0m,"None")       ], // STD
            [ (-5m,"Reduction"),(-10m,"Reduction"),(0m,"None"),    (0m,"None"),      (5m,"Surcharge")  ], // RED
            [ (5m,"Surcharge"),(5m,"Surcharge"), (5m,"Surcharge"),(10m,"Surcharge"),(15m,"Surcharge")  ], // PREM
        };

        var ruleSets = new List<EcoModulationRuleSet>();
        var ecoRules = new List<EcoModulationRule>();
        int ruleIdx  = 1;

        for (int s = 0; s < setDefs.Length; s++)
        {
            var (code, name) = setDefs[s];
            var setId = SeedGuid("ecors", s + 1);

            ruleSets.Add(new EcoModulationRuleSet
            {
                Id                  = setId,
                OwnerId             = ownerId,
                RuleSetName         = name,
                Version             = "1.0",
                Status              = "Active",
                ValidFrom           = now.AddMonths(-6),
                PublisherName       = "GreenTransit Demo",
                PublisherNationalId = $"B0000000{s + 1}",
                PublisherCenterCode = $"CC-DEMO-{s + 1:D2}",
                SourceSystem        = Seed,
                Hash                = $"seed-{code}",
                CreatedAt           = now,
                UpdatedAt           = now,
                IdUser              = SeedUser,
            });

            for (int c = 0; c < categories.Length; c++)
            {
                var (impact, impactType) = impacts[s][c];
                ecoRules.Add(new EcoModulationRule
                {
                    Id              = SeedGuid("ecor", ruleIdx++),
                    RuleSetId       = setId,
                    RuleCode        = $"{code}-{catNames[c][..3].ToUpper()}-{c + 1:D2}",
                    ProductCategory = categories[c],
                    CriteriaJson    = $"{{\"productCategory\":\"{catNames[c]}\",\"minWeightKg\":100}}",
                    FeeImpactType   = impactType,
                    FeeImpactValue  = impact,
                    CreatedAt       = now,
                });
            }
        }

        return (ruleSets, ecoRules);
    }

    // =========================================================================
    // BUILDERS
    // =========================================================================

    // ── TreatmentOperations ───────────────────────────────────────────────────
    private static List<TreatmentOperation> BuildTreatmentOperations()
    {
        var ops = new List<(string Code, string Desc, string Type, bool Recycl, bool Energy, bool Reuse)>
        {
            ("R1",  "Utilización como combustible u otro medio de obtener energía",                "Recovery", false, true,  false),
            ("R2",  "Recuperación o regeneración de disolventes",                                   "Recovery", true,  false, false),
            ("R3",  "Reciclado o recuperación de sustancias orgánicas no utilizadas como disolventes","Recovery", true,  false, false),
            ("R4",  "Reciclado o recuperación de metales y compuestos metálicos",                  "Recovery", true,  false, false),
            ("R5",  "Reciclado o recuperación de otras materias inorgánicas",                      "Recovery", true,  false, false),
            ("R6",  "Regeneración de ácidos o bases",                                              "Recovery", false, false, false),
            ("R7",  "Recuperación de componentes utilizados para reducir la contaminación",        "Recovery", false, false, false),
            ("R8",  "Recuperación de componentes de catalizadores",                                "Recovery", false, false, false),
            ("R9",  "Regeneración u otro nuevo empleo de aceites usados",                          "Recovery", false, false, false),
            ("R10", "Tratamiento de suelos que produzca un beneficio a la agricultura",            "Recovery", false, false, false),
            ("R11", "Utilización de residuos obtenidos a partir de cualquiera de las operaciones R1-R10","Recovery", false, false, false),
            ("R12", "Intercambio de residuos para someterlos a cualquiera de las operaciones R1-R11","Recovery", false, false, false),
            ("R13", "Almacenamiento de residuos en espera de cualquiera de las operaciones R1-R12","Recovery", false, false, false),
            ("D1",  "Depósito sobre el suelo o en su interior",                                   "Disposal", false, false, false),
            ("D2",  "Tratamiento en medio terrestre",                                             "Disposal", false, false, false),
            ("D3",  "Inyección en profundidad",                                                   "Disposal", false, false, false),
            ("D4",  "Embalse superficial",                                                        "Disposal", false, false, false),
            ("D5",  "Vertedero especialmente diseñado",                                           "Disposal", false, false, false),
            ("D6",  "Vertido en el medio acuático excepto en el mar",                             "Disposal", false, false, false),
            ("D7",  "Vertido en el mar",                                                          "Disposal", false, false, false),
            ("D8",  "Tratamiento biológico no especificado en otro apartado",                     "Disposal", false, false, false),
            ("D9",  "Tratamiento fisicoquímico no especificado en otro apartado",                 "Disposal", false, false, false),
            ("D10", "Incineración en tierra",                                                     "Disposal", false, false, false),
            ("D11", "Incineración en el mar",                                                     "Disposal", false, false, false),
            ("D12", "Depósito permanente",                                                        "Disposal", false, false, false),
            ("D13", "Combinación o mezcla previa a la aplicación de cualquier D1-D12",            "Disposal", false, false, false),
            ("D14", "Reacondicionamiento previo a la aplicación de cualquier D1-D13",             "Disposal", false, false, false),
            ("D15", "Almacenamiento previo a cualquiera de las operaciones D1-D14",               "Disposal", false, false, false),
        };

        return ops.Select((o, i) => new TreatmentOperation
        {
            Id                    = SeedGuid("to", i + 1),
            Code                  = o.Code,
            OperationType         = o.Type,
            Description           = o.Desc,
            ShortDescription      = o.Code,
            IsRecycling           = o.Recycl,
            IsEnergyRecovery      = o.Energy,
            IsPreparationForReuse = o.Reuse,
            SortOrder             = i + 1,
            IsActive              = true,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow
        }).ToList();
    }

    // ── LerCodes ──────────────────────────────────────────────────────────────
    private static List<LerCode> BuildLerCodes()
    {
        var rows = new[]
        {
            ("150101", "Envases de papel y cartón",                             "15", false, false, "Envases"),
            ("150102", "Envases de plástico",                                   "15", false, false, "Envases"),
            ("150103", "Envases de madera",                                     "15", false, false, "Envases"),
            ("150104", "Envases metálicos",                                     "15", false, false, "Envases"),
            ("150107", "Envases de vidrio",                                     "15", false, false, "Envases"),
            ("150110", "Envases con restos de sustancias peligrosas",           "15", true,  false, "Envases"),
            ("160211", "Equipos desechados con CFC, HCFC, HFC",                "16", true,  true,  "RAEE"),
            ("160213", "Equipos desechados con componentes peligrosos",         "16", true,  true,  "RAEE"),
            ("160214", "Equipos desechados no peligrosos",                      "16", false, true,  "RAEE"),
            ("160216", "Componentes extraídos de equipos eléctricos",           "16", false, true,  "RAEE"),
            ("170101", "Hormigón",                                              "17", false, false, "Construcción"),
            ("170201", "Madera",                                                "17", false, false, "Construcción"),
            ("170401", "Cobre, bronce, latón",                                  "17", false, false, "Metales"),
            ("170405", "Hierro y acero",                                        "17", false, false, "Metales"),
            ("170411", "Cables no peligrosos",                                  "17", false, false, "Metales"),
            ("200101", "Papel y cartón",                                        "20", false, false, "Municipal"),
            ("200102", "Vidrio",                                                "20", false, false, "Municipal"),
            ("200108", "Residuos biodegradables de cocinas",                    "20", false, false, "Municipal"),
            ("200136", "Equipos eléctricos y electrónicos desechados (RAEE)",   "20", false, true,  "RAEE"),
            ("200301", "Mezclas de residuos municipales",                       "20", false, false, "Municipal"),
        };

        return rows.Select((r, i) => new LerCode
        {
            Id                     = SeedGuid("lc", i + 1),
            Code                   = r.Item1,
            Description            = r.Item2,
            Chapter                = r.Item3,
            IsDangerous            = r.Item4,
            IsRAEE                 = r.Item5,
            DefaultProductCategory = r.Item6,
            IsActive               = true,
            CreatedAt              = DateTime.UtcNow,
            UpdatedAt              = DateTime.UtcNow
        }).ToList();
    }

    // ── EmissionFactors ───────────────────────────────────────────────────────
    private static List<EmissionFactor> BuildEmissionFactors(Guid setId, DateTime now)
    {
        var rows = new[]
        {
            ("Camión 12t", "Diesel",   "Euro4", 1.20m),
            ("Camión 12t", "Diesel",   "Euro5", 1.05m),
            ("Camión 12t", "Diesel",   "Euro6", 0.90m),
            ("Camión 26t", "Diesel",   "Euro5", 0.85m),
            ("Camión 26t", "Diesel",   "Euro6", 0.72m),
            ("Camión 26t", "GNC",      "Euro6", 0.55m),
            ("Furgón",     "Diesel",   "Euro5", 1.50m),
            ("Furgón",     "Eléctrico","Euro6", 0.10m),
            ("Camión 12t", "GNC",      "Euro6", 0.65m),
            ("Camión 26t", "Eléctrico","Euro6", 0.08m),
        };

        return rows.Select((r, i) => new EmissionFactor
        {
            Id          = SeedGuid("ef", i + 1),
            FactorSetId = setId,
            VehicleType = r.Item1,
            FuelType    = r.Item2,
            EuroClass   = r.Item3,
            Value       = r.Item4,
            Unit        = "kgCO2e/km",
            CreatedAt   = now
        }).ToList();
    }

    // ── Entities ──────────────────────────────────────────────────────────────
    private static List<BusinessEntity> BuildEntities(Guid ownerId, DateTime now)
    {
        var list = new List<BusinessEntity>();
        var t = now;

        // 20 Productores
        var producerCities = new[]
        {
            ("Zaragoza",   "AR", "50001", "50",  "50297", "41.6488", "-0.8891"),
            ("Madrid",     "MD", "28001", "28",  "28079", "40.4168", "-3.7038"),
            ("Barcelona",  "CT", "08001", "08",  "08019", "41.3851", "2.1734"),
            ("Sevilla",    "AN", "41001", "41",  "41091", "37.3891", "-5.9845"),
            ("Valencia",   "VC", "46001", "46",  "46250", "39.4699", "-0.3763"),
            ("Bilbao",     "PV", "48001", "48",  "48020", "43.2630", "-2.9350"),
            ("Málaga",     "AN", "29001", "29",  "29067", "36.7213", "-4.4214"),
            ("Murcia",     "MC", "30001", "30",  "30030", "37.9922", "-1.1307"),
            ("Valladolid", "CL", "47001", "47",  "47186", "41.6523", "-4.7245"),
            ("Palma",      "IB", "07001", "07",  "07040", "39.5696", "2.6502"),
            ("Alicante",   "VC", "03001", "03",  "03014", "38.3452", "-0.4810"),
            ("Córdoba",    "AN", "14001", "14",  "14021", "37.8882", "-4.7794"),
            ("Granada",    "AN", "18001", "18",  "18087", "37.1773", "-3.5986"),
            ("Santander",  "CB", "39001", "39",  "39075", "43.4623", "-3.8099"),
            ("Pamplona",   "NC", "31001", "31",  "31201", "42.8169", "-1.6432"),
            ("Logroño",    "RI", "26001", "26",  "26089", "42.4650", "-2.4456"),
            ("Oviedo",     "AS", "33001", "33",  "33044", "43.3614", "-5.8593"),
            ("A Coruña",   "GA", "15001", "15",  "15030", "43.3623", "-8.4115"),
            ("Toledo",     "CM", "45001", "45",  "45168", "39.8628", "-4.0273"),
            ("Badajoz",    "EX", "06001", "06",  "06015", "38.8794", "-6.9706"),
        };
        for (int i = 0; i < 20; i++)
        {
            var (city, sc, zip, prov, mun, lat, lon) = producerCities[i];
            list.Add(Entity(SeedGuid("prod", i + 1), $"Productor Demo {i+1:D2}", $"B1234{i+1:D6}", $"NIMA-P{i+1:D3}",
                "Producer", "SL", sc, zip, prov, mun,
                $"Calle Industria {i+1}, {city}", lat, lon,
                $"+34 976 0{i+1:D5}", $"prod{i+1:D2}@greentransit.test", $"Contacto Productor {i+1}", t));
        }

        // 3 OperatorTransfer
        for (int i = 0; i < 3; i++)
        {
            list.Add(Entity(SeedGuid("ot", i + 1), $"Operador Traslado Demo {i+1}", $"B9900{i+1:D4}", $"NIMA-OT{i+1:D3}",
                "OperatorTransfer", "SL", "AR", "50001", "50", "50297",
                $"Polígono Demo {i+1}, Zaragoza", "41.6488", "-0.8891",
                $"+34 976 111 00{i+1}", $"ot{i+1}@greentransit.test", $"Contacto OT {i+1}", t,
                inscriptionNumber: $"OT-REG-{i+1:D4}"));
        }

        // 8 SCRAP
        var scrapData = new[]
        {
            ("EcoEnvases SCRAP",   "Envases"),
            ("ReciclaRAEE SCRAP",  "RAEE"),
            ("GreenPack SCRAP",    "Voluminosos"),
            ("EnviroPlas SCRAP",   "Plásticos"),
            ("MetalCiclo SCRAP",   "Metales"),
            ("PaperLoop SCRAP",    "Papel"),
            ("GlassCycle SCRAP",   "Vidrio"),
            ("BioCircle SCRAP",    "Orgánico"),
        };
        for (int i = 0; i < 8; i++)
        {
            var (name, _) = scrapData[i];
            list.Add(Entity(SeedGuid("scrap", i + 1), name, $"A8800{i+1:D4}", $"NIMA-SC{i+1:D3}",
                "SCRAP", "Asociación", "MD", "28001", "28", "28079",
                $"Paseo Castellana {i+1}, Madrid", "40.4168", "-3.7038",
                $"+34 91 000 00{i+1}", $"scrap{i+1}@greentransit.test", $"Dir. SCRAP {i+1}", t));
        }

        // 10 PublicEntity
        var peData = new[]
        {
            ("Ayuntamiento de Zaragoza",   "AR", "50001", "50", "50297", "41.6488", "-0.8891"),
            ("Ayuntamiento de Madrid",     "MD", "28001", "28", "28079", "40.4168", "-3.7038"),
            ("Ajuntament de Barcelona",    "CT", "08001", "08", "08019", "41.3851", "2.1734"),
            ("Ayuntamiento de Sevilla",    "AN", "41001", "41", "41091", "37.3891", "-5.9845"),
            ("Ayuntamiento de Valencia",   "VC", "46001", "46", "46250", "39.4699", "-0.3763"),
            ("Ayuntamiento de Bilbao",     "PV", "48001", "48", "48020", "43.2630", "-2.9350"),
            ("Ayuntamiento de Málaga",     "AN", "29001", "29", "29067", "36.7213", "-4.4214"),
            ("Ayuntamiento de Murcia",     "MC", "30001", "30", "30030", "37.9922", "-1.1307"),
            ("Ayuntamiento de Valladolid", "CL", "47001", "47", "47186", "41.6523", "-4.7245"),
            ("Ayuntamiento de Alicante",   "VC", "03001", "03", "03014", "38.3452", "-0.4810"),
        };
        for (int i = 0; i < 10; i++)
        {
            var (name, sc, zip, prov, mun, lat, lon) = peData[i];
            list.Add(Entity(SeedGuid("pe", i + 1), name, $"P0000{i+1:D4}", null,
                "PublicEntity", "Ayuntamiento", sc, zip, prov, mun,
                $"Plaza Mayor {i+1}", lat, lon,
                $"+34 9{i+1}0 000 001", $"pe{i+1}@greentransit.test", $"Técnico Medio Ambiente {i+1}", t));
        }

        // 8 Carrier
        for (int i = 0; i < 8; i++)
        {
            list.Add(Entity(SeedGuid("carr", i + 1), $"Transportes Demo {i+1} SL", $"B7700{i+1:D4}", $"NIMA-CR{i+1:D3}",
                "Carrier", "SL", "AR", "50001", "50", "50297",
                $"Carretera Nacional {i+1} km {i*10}", "41.6488", "-0.8891",
                $"+34 976 222 00{i+1}", $"carrier{i+1}@greentransit.test", $"Jefe Tráfico {i+1}", t,
                inscriptionNumber: $"CR-REG-{i+1:D4}"));
        }

        // 6 CAC
        var cacCoords = new[]
        {
            ("41.6500", "-0.8850"), ("40.4200", "-3.7100"), ("41.3900", "2.1800"),
            ("37.3900", "-5.9900"), ("39.4750", "-0.3800"), ("43.2700", "-2.9400"),
        };
        for (int i = 0; i < 6; i++)
        {
            var (lat, lon) = cacCoords[i];
            list.Add(Entity(SeedGuid("cac", i + 1), $"Centro Acopio Demo {i+1}", $"B5500{i+1:D4}", $"NIMA-CAC{i+1:D3}",
                "CAC", "SL", "AR", "50001", "50", "50297",
                $"Camino del Barranco {i+1}", lat, lon,
                $"+34 976 333 00{i+1}", $"cac{i+1}@greentransit.test", $"Encargado CAC {i+1}", t));
        }

        // 6 Plant
        var plantCoords = new[]
        {
            ("41.5800", "-0.9300"), ("40.3800", "-3.8000"), ("41.4500", "2.2000"),
            ("37.4200", "-5.8500"), ("39.5200", "-0.4200"), ("43.2800", "-2.9000"),
        };
        for (int i = 0; i < 6; i++)
        {
            var (lat, lon) = plantCoords[i];
            list.Add(Entity(SeedGuid("plant", i + 1), $"Planta Tratamiento Demo {i+1}", $"B3300{i+1:D4}", $"CC-PLANT-{i+1:D3}",
                "Plant", "SA", "AR", "50001", "50", "50297",
                $"Polígono Industrial Demo {i+1}", lat, lon,
                $"+34 976 444 00{i+1}", $"plant{i+1}@greentransit.test", $"Director Planta {i+1}", t));
        }

        // 1 Coordinator
        list.Add(Entity(SeedGuid("coord", 1), "Coordinador Demo COORD-01", "G1100001", null,
            "Coordinator", "Fundación", "MD", "28001", "28", "28079",
            "Calle Velázquez 10, Madrid", "40.4200", "-3.6900",
            "+34 91 555 0001", "coord@greentransit.test", "Coordinador General", t));

        // 1 Dispatch_office
        list.Add(Entity(SeedGuid("office", 1), "Oficina de Asignación OFFICE-01", "G2200001", null,
            "Dispatch_office", "Gestora", "MD", "28001", "28", "28079",
            "Calle Gran Vía 1, Madrid", "40.4200", "-3.7000",
            "+34 91 666 0001", "office@greentransit.test", "Jefe Asignación", t));

        return list;
    }

    private static BusinessEntity Entity(
        Guid id, string name, string nationalId, string? centerCode,
        string role, string entityType, string stateCode, string zip,
        string prov, string mun, string address, string lat, string lon,
        string phone, string email, string contact, DateTime t,
        string? inscriptionNumber = null) => new()
    {
        Id               = id,
        Name             = name,
        NationalId       = nationalId,
        CenterCode       = centerCode,
        EntityRole       = role,
        EntityType       = entityType,
        CountryCode      = "ES",
        StateCode        = stateCode,
        ZipCode          = zip,
        ProvinceCode     = prov,
        MunicipalityCode = mun,
        Address          = address,
        Latitude         = lat,
        Longitude        = lon,
        PhoneNumber      = phone,
        Email            = email,
        ContactPerson    = contact,
        InscriptionNumber = inscriptionNumber,
        IsActive         = true,
        SourceSystem     = Seed,
        CreatedAt        = t,
        UpdatedAt        = t,
        IdUser           = SeedUser
    };

    // ── Residues ──────────────────────────────────────────────────────────────
    private static List<Residue> BuildResidues(
        Dictionary<string, Guid> lerByCode, List<Guid> producerIds)
    {
        var list = new List<Residue>();
        var t = DateTime.UtcNow;

        // Waste (20)
        // Tupla: (nombre, lerCode, flowType, productCategory, isDangerous, isRAEE)
        // ProductCategory debe coincidir con MarketShare.Category: "Envases" | "RAEE" | "Voluminosos"
        // FlowType debe coincidir con MarketShare.FlowType:        "Recogida" | "Reciclaje" | "Valorización"
        var wasteDef = new[]
        {
            ("Papel y cartón mezclado",       "150101", "Recogida",     "Envases",     false, false),
            ("Plástico mezclado",             "150102", "Recogida",     "Envases",     false, false),
            ("Madera reciclable",             "150103", "Recogida",     "Voluminosos", false, false),
            ("Envases metálicos mixtos",      "150104", "Reciclaje",    "Envases",     false, false),
            ("Vidrio envases",               "150107", "Reciclaje",    "Envases",     false, false),
            ("Envase peligroso contaminado",  "150110", "Valorización", "Envases",     true,  false),
            ("RAEE grande",                  "160213", "Recogida",     "RAEE",        true,  true),
            ("RAEE pequeño",                 "160214", "Recogida",     "RAEE",        false, true),
            ("Cobre chatarra",               "170401", "Reciclaje",    "Envases",     false, false),
            ("Hierro y acero",               "170405", "Valorización", "Envases",     false, false),
            ("Papel oficina",                "200101", "Recogida",     "Envases",     false, false),
            ("Vidrio doméstico",             "200102", "Reciclaje",    "Envases",     false, false),
            ("Orgánico cocina",              "200108", "Recogida",     "Voluminosos", false, false),
            ("RAEE municipal",               "200136", "Reciclaje",    "RAEE",        false, true),
            ("Residuos mixtos",              "200301", "Valorización", "Voluminosos", false, false),
            ("Chatarra metálica",            "170401", "Valorización", "Envases",     false, false),
            ("Plástico industrial",          "150102", "Reciclaje",    "RAEE",        false, false),
            ("Cartón ondulado",              "150101", "Valorización", "RAEE",        false, false),
            ("Equipo informático obsoleto",  "160214", "Recogida",     "RAEE",        false, true),
            ("Cable eléctrico",             "170411", "Valorización",  "Voluminosos", false, false),
            // 4 residuos adicionales para cubrir LER sin residuo asociado
            ("RAEE con CFC/HCFC",           "160211", "Recogida",     "RAEE",        true,  true),
            ("Componentes eléctricos extraídos", "160216", "Reciclaje", "RAEE",      false, true),
            ("Hormigón obra",               "170101", "Valorización", "Construcción", false, false),
            ("Madera de construcción",      "170201", "Reciclaje",    "Voluminosos", false, false),
        };
        for (int i = 0; i < wasteDef.Length; i++)
        {
            var (name, lerCode, flow, category, danger, raee) = wasteDef[i];
            lerByCode.TryGetValue(lerCode, out var lerId);
            list.Add(new Residue
            {
                Id              = SeedGuid("rw", i + 1),
                ResidueType     = "Waste",
                Name            = name,
                FlowType        = flow,
                ProductCategory = category,
                IdLERCode       = lerId == Guid.Empty ? null : lerId,
                IsDangerous     = danger,
                IsRAEE          = raee,
                IdProducer      = null, // catálogo global — visible para todos los perfiles
                IsActive        = true,
                SourceSystem    = Seed,
                Version         = 1,
                CreatedAt       = t,
                UpdatedAt       = t,
                IdUser          = SeedUser
            });
        }

        // Product (15)
        // Tupla: (nombre, category, use, lerCode)
        var productDef = new[]
        {
            ("Botella PET 1.5L",          "Envases",    "Beverage",    "150102"),
            ("Lata aluminio 330ml",       "Envases",    "Beverage",    "150104"),
            ("Caja cartón 30x20x15cm",    "Envases",    "Packaging",   "150101"),
            ("Botella vidrio 75cl",       "Envases",    "Beverage",    "150107"),
            ("Envase plástico 5kg",       "Envases",    "Food",        "150102"),
            ("Ordenador portátil",        "RAEE",       "Electronics", "160213"),
            ("Televisor LED 42\"",        "RAEE",       "Electronics", "160213"),
            ("Frigorífico A++",           "RAEE",       "WhiteGoods",  "160213"),
            ("Lavadora 8kg",              "RAEE",       "WhiteGoods",  "160213"),
            ("Móvil smartphone",          "RAEE",       "Mobile",      "160214"),
            ("Palé madera",               "Envases",    "Logistics",   "150103"),
            ("Film estirable",            "Envases",    "Packaging",   "150102"),
            ("Tóner impresora",           "RAEE",       "Consumable",  "160214"),
            ("Batería Li-ion",            "RAEE",       "Battery",     "160214"),
            ("Luminaria LED",             "RAEE",       "Lighting",    "160214"),
        };
        for (int i = 0; i < productDef.Length; i++)
        {
            var (name, category, use, lerCode) = productDef[i];
            lerByCode.TryGetValue(lerCode, out var lerProdId);
            list.Add(new Residue
            {
                Id              = SeedGuid("rp", i + 1),
                ResidueType     = "Product",
                Name            = name,
                ProductCategory = category,
                ProductUse      = use,
                IdLERCode       = lerProdId == Guid.Empty ? null : lerProdId,
                WeightPerUnitKg = 0.5m + i * 0.3m,
                RecycledContentPercent = 10 + i * 3,
                IdProducer      = null, // catálogo global — visible para todos los perfiles
                IsActive        = true,
                SourceSystem    = Seed,
                Version         = 1,
                CreatedAt       = t,
                UpdatedAt       = t,
                IdUser          = SeedUser
            });
        }

        // ProductSpec (12)
        var specDefs = new[]
        {
            // (nombre, category, lerCode, reparability, recycledPct, disassembly, containsHazardous, potentialLer)
            ("Botella PET ecomod",        "Envases",    "150102", 7,  45m, "Easy",   false, true),
            ("Lata aluminio ecomod",      "Envases",    "150104", 9,  85m, "Easy",   false, true),
            ("Caja cartón ecomod",        "Envases",    "150101", 6,  70m, "Easy",   false, true),
            ("Botella vidrio ecomod",     "Envases",    "150107", 8,  60m, "Medium", false, true),
            ("Ordenador portátil RAEE",   "RAEE",       "160213", 5,  30m, "Medium", true,  true),
            ("Televisor LED ecomod",      "RAEE",       "160213", 4,  25m, "Hard",   true,  true),
            ("Frigorífico ecomod",        "RAEE",       "160213", 3,  20m, "Hard",   true,  true),
            ("Móvil smartphone ecomod",   "RAEE",       "160214", 6,  35m, "Medium", true,  true),
            ("Palé madera ecomod",        "Voluminosos","150103", 8,  80m, "Easy",   false, true),
            ("Film estirable ecomod",     "Envases",    "150102", 3,  15m, "Hard",   false, false),
            ("Tóner impresora ecomod",    "RAEE",       "160214", 4,  10m, "Hard",   true,  false),
            ("Luminaria LED ecomod",      "RAEE",       "160214", 7,  40m, "Medium", false, true),
        };
        for (int i = 0; i < specDefs.Length; i++)
        {
            var (spName, spCat, spLer, spRepair, spRecycled, spDis, spHazard, spHasLer) = specDefs[i];
            lerByCode.TryGetValue(spLer, out var spLerId);
            var compJson = $"[{{\"material\":\"Plástico\",\"pct\":{100 - spRecycled}}},{{\"material\":\"Reciclado\",\"pct\":{spRecycled}}}]";
            list.Add(new Residue
            {
                Id                     = SeedGuid("rps", i + 1),
                ResidueType            = "ProductSpec",
                Name                   = spName,
                ProductCategory        = spCat,
                IdLERCode              = spLerId == Guid.Empty ? null : spLerId,
                IdProducer             = producerIds.Count > 0 ? producerIds[i % producerIds.Count] : null,
                ReparabilityIndex      = spRepair,
                RecycledContentPercent = spRecycled,
                DisassemblyEase        = spDis,
                ContainsHazardous      = spHazard,
                CompositionJson        = compJson,
                PotentialLERCodesJson  = spHasLer ? $"[\"{spLer}\"]" : null,
                IsActive               = true,
                SourceSystem           = Seed,
                Version                = 1,
                CreatedAt              = t,
                UpdatedAt              = t,
                IdUser                 = SeedUser
            });
        }

        return list;
    }

    // ── Agreements ────────────────────────────────────────────────────────────
    private static (List<Agreement>, List<AgreementDocument>) BuildAgreements(
        List<Guid> scraps, List<Guid> publicEntities, Guid coordinator, Guid ownerId, DateTime now)
    {
        var agreements = new List<Agreement>();
        var docs       = new List<AgreementDocument>();
        var t = now;
        var wasteStreams = new[] { "Envases", "RAEE", "Voluminosos", "Plásticos", "Metales" };
        var ccaas       = new[] { "Aragón", "Madrid", "Cataluña", "Andalucía", "Comunidad Valenciana" };

        // Códigos LER representativos por WasteStream para las reglas tarifarias
        var lerByStream = new Dictionary<string, string>
        {
            ["Envases"]    = "150101",
            ["RAEE"]       = "160213",
            ["Voluminosos"]= "150103",
            ["Plásticos"]  = "150102",
            ["Metales"]    = "170405",
        };

        int idx = 0;
        for (int s = 0; s < Math.Min(scraps.Count, 5); s++)
        {
            for (int p = 0; p < Math.Min(publicEntities.Count, 5); p++)
            {
                idx++;
                var status = idx <= 20 ? "Active" : idx <= 23 ? "Expired" : "Draft";
                var stream = wasteStreams[s];
                var lerCode = lerByStream.TryGetValue(stream, out var lc) ? lc : "150101";
                var agr = new Agreement
                {
                    Id              = SeedGuid("agr", idx),
                    OwnerId         = ownerId,
                    AgreementNumber = $"AGR-{t.Year}-{idx:D4}",
                    Status          = status,
                    EffectiveFrom   = t.AddMonths(-6),
                    EffectiveTo     = status == "Active" ? null : t.AddMonths(6),
                    IdScrap         = scraps[s],
                    IdPublicEntity  = publicEntities[p],
                    IdCoordinator   = coordinator,
                    WasteStream     = stream,
                    SubStream       = s % 2 == 0 ? "Plástico" : "Metal",
                    AutonomousCommunity = ccaas[p],
                    Currency        = "EUR",
                    TariffModelType = idx % 3 == 0 ? "PorUnidad" : "PorPeso",
                    TariffRulesJson = $"{{\"lerCode\":\"{lerCode}\",\"pricePerKg\":0.15,\"minWeight\":1000}}",
                    MinimumsJson    = "{\"minMonthlyKg\":500}",
                    ObligationsJson = "{\"reportingFrequency\":\"Monthly\"}",
                    SourceSystem    = Seed,
                    Version         = 1,
                    CreatedAt       = t,
                    UpdatedAt       = t,
                    IdUser          = SeedUser
                };
                agreements.Add(agr);

                var docTypes = new[] { "Contrato", "Anexo", "Acta" };
                for (int d = 0; d < 2 + (idx % 2); d++)
                {
                    docs.Add(new AgreementDocument
                    {
                        Id                = SeedGuid($"adoc{idx}", d + 1),
                        AgreementId       = agr.Id,
                        DocumentType      = docTypes[d % 3],
                        DocumentId        = $"DMS-DOC-{SeedGuid($"adoc{idx}", d + 1):N}",
                        DocumentHash      = $"sha256-seed-{idx}-{d}",
                        SignedAt          = t.AddDays(-30),
                        SignatureProvider  = "VIDsigner"
                    });
                }
            }
        }

        return (agreements, docs);
    }

    // ── ServiceOrders + WasteMoves + WasteMoveResidues ─────────────────────────
    private static (List<ServiceOrder>, List<ServiceOrderResidue>, List<WasteMove>, List<WasteMoveResidue>) BuildOperations(
        List<Guid> producers, List<Guid> publicEnts, List<Guid> carriers,
        List<Guid> plants, List<Guid> cacs, List<Guid> scraps, List<Guid> opTransfers,
        List<Guid> lerIds, List<Guid> wasteResidueIds, List<Guid> treatOpIds, Guid ownerId, DateTime now)
    {
        var sos     = new List<ServiceOrder>();
        var sors    = new List<ServiceOrderResidue>();
        var wms     = new List<WasteMove>();
        var wmrs    = new List<WasteMoveResidue>();

        // ServiceOrder statuses según el prompt
        var soStatuses = new[] { "Pending", "Pending", "Pending", "Pending", "Pending",
                                  "Pending", "Pending", "Pending", "Pending", "Pending",
                                  "Scheduled","Scheduled","Scheduled","Scheduled","Scheduled",
                                  "Scheduled","Scheduled","Scheduled","Scheduled","Scheduled",
                                  "Scheduled","Scheduled","Scheduled","Scheduled","Scheduled",
                                  "Scheduled","Scheduled","Scheduled","Scheduled","Scheduled",
                                  "InProgress","InProgress","InProgress","InProgress","InProgress",
                                  "InProgress","InProgress","InProgress","InProgress","InProgress",
                                  "InProgress","InProgress","InProgress","InProgress","InProgress",
                                  "InProgress","InProgress","InProgress","InProgress","InProgress",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Completed","Completed",
                                  "Completed","Completed","Completed","Cancelled","Cancelled" };

        var serviceStatuses = new[]
        {
            "SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO",
            "PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO",
            "RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO",
            "EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC",
            "EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA",
            "EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA",
            "CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO",
            "CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO",
            "CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO",
        };
        var vehicles  = new[] { "Camión 12t", "Camión 26t", "Furgón", "Camión 12t", "Furgón" };
        var fuels     = new[] { "Diesel", "GNC", "Eléctrico", "Diesel", "Diesel" };
        var euros     = new[] { "Euro4", "Euro5", "Euro6", "Euro6", "Euro5" };
        var wasteStreams = new[] { "RAEE", "RAEE", "RAEE", "RAEE", "Envases", "RAEE", "Voluminosos", "RAEE" };
        // Horas con franja punta y fuera de punta
        var hours     = new[] { 7, 8, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 17, 18, 19, 20, 7, 8, 17, 18 };
        var minutes   = new[] { 30, 0, 45, 15, 0, 30, 0, 0, 30, 0, 0, 30, 45, 0, 30, 0, 45, 15, 30, 0 };
        var year      = now.Year;

        var allIssuers = producers.Concat(publicEnts).ToList();

        // Distribuimos los 100 WasteMoves de forma que todos los trimestres del año actual
        // queden poblados con suficientes registros CLASIFICADOS para que los gráficos
        // de evolución trimestral muestren datos llamativos.
        //
        // Índices 0-9   → SOLICITADO (futuro)
        // Índices 10-24 → PLANIFICADO/RECOGIDO  (hace 1-3 meses)
        // Índices 25-39 → EN_CAC/EN_PLANTA       (hace 1-6 meses)
        // Índices 40-69 → EN_PLANTA              (hace 2-9 meses)
        // Índices 70-99 → CLASIFICADO, distribuidos 1 por cada 10 días del año actual
        //                 → asegura registros en Q1, Q2, Q3 y Q4
        for (int i = 0; i < 100; i++)
        {
            DateTime issuedAt;
            if (i < 10)
            {
                // Futuros 1-14 días
                issuedAt = now.AddDays(1 + (i * 13) % 13).Date
                    .AddHours(hours[i % hours.Length]).AddMinutes(minutes[i % minutes.Length]);
            }
            else if (i < 70)
            {
                // Últimos 9 meses, distribuidos para cubrir todo el rango
                issuedAt = now.AddMonths(-9).AddDays((i - 10) * 4).Date
                    .AddHours(hours[i % hours.Length]).AddMinutes(minutes[i % minutes.Length]);
                // Asegurar que no supere hoy
                if (issuedAt > now) issuedAt = now.AddDays(-1);
            }
            else
            {
                // CLASIFICADOS: distribuidos uniformemente en el año actual (día 1 a día ~330)
                // para que Q1, Q2, Q3, Q4 tengan datos en EntryPlants y TreatmentPlants
                var dayOfYear = (i - 70) * (330 / 30) + 1; // 1 registro cada ~11 días
                issuedAt = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddDays(dayOfYear - 1)
                    .AddHours(hours[i % hours.Length]).AddMinutes(minutes[i % minutes.Length]);
                if (issuedAt > now) issuedAt = now.AddDays(-2);
            }

            var issuedBy = allIssuers[i % allIssuers.Count];
            var carrier  = carriers.Count > 0 ? carriers[i % carriers.Count] : (Guid?)null;
            var plant    = plants[i % plants.Count];
            var ler      = lerIds[i % lerIds.Count];
            var scrap    = scraps[i % scraps.Count];
            var soStatus = i < soStatuses.Length ? soStatuses[i] : "Completed";
            var svcStatus = i < serviceStatuses.Length ? serviceStatuses[i] : "SOLICITADO";

            var so = new ServiceOrder
            {
                Id                   = SeedGuid("so", i + 1),
                OwnerId              = ownerId,
                ServiceOrderNumber   = $"SO-{year}-{i+1:D5}",
                IssuedAt             = issuedAt,
                IdIssuedBy           = issuedBy,
                Status               = soStatus,
                Priority             = (i % 8 < 4) ? "Normal" : (i % 8 < 7 ? "High" : "Urgent"),
                WasteStream          = wasteStreams[i % wasteStreams.Length],
                SubStream            = i % 3 == 0 ? "RAEE Cat1" : (i % 3 == 1 ? "RAEE Cat2" : "Plástico"),
                IdLERCode            = ler,
                IdPickupPoint        = issuedBy,
                PlannedPickupStart   = issuedAt.AddDays(2),
                PlannedPickupEnd     = issuedAt.AddDays(2).AddHours(4),
                PlannedDeliveryStart = issuedAt.AddDays(3),
                PlannedDeliveryEnd   = issuedAt.AddDays(3).AddHours(4),
                EstimatedWeight      = 100 + (i * 47) % 4900,
                MeasureUnit          = 1,
                IdCarrier            = carrier,
                IdPlannedPlant       = plant,
                VehicleType          = vehicles[i % vehicles.Length],
                FuelType             = fuels[i % fuels.Length],
                EuroClass            = euros[i % euros.Length],
                SourceSystem         = Seed,
                Version              = 1,
                CreatedAt            = issuedAt,
                UpdatedAt            = issuedAt,
                IdUser               = SeedUser
            };
            sos.Add(so);

            // Líneas de residuo de la SO (al menos 1, hasta 3)
            int soLineCount = 1 + (i % 3);
            for (int sl = 0; sl < soLineCount; sl++)
            {
                sors.Add(new ServiceOrderResidue
                {
                    Id             = SeedGuid($"sor{i+1}", sl + 1),
                    IdServiceOrder = so.Id,
                    SortOrder      = sl,
                    IdLERCode      = lerIds[(i + sl) % lerIds.Count],
                    EstimatedWeight = 100 + (i * 31 + sl * 47) % 2000,
                    MeasureUnit    = 1,
                    Units          = 1 + sl
                });
            }

            var opTransfer = opTransfers.Count > 0 && i % 3 == 0 ? opTransfers[i % opTransfers.Count] : (Guid?)null;
            var pickupHour  = hours[(i * 2) % hours.Length];
            var pickupMin   = minutes[(i * 2) % minutes.Length];
            var actualPickup = issuedAt.AddDays(2).Date.AddHours(pickupHour).AddMinutes(pickupMin);

            var wm = new WasteMove
            {
                Id                  = SeedGuid("wm", i + 1),
                OwnerId             = ownerId,
                WasteMoveReference  = $"WM-{year}-{i+1:D5}",
                Lot                 = $"LOT-{i+1:D3}",
                ServiceOrderId      = so.Id,
                IdScrap             = scrap,
                IdSource            = issuedBy,
                IdDestination       = plant,
                IdOperatorTransfer  = opTransfer,
                ServiceStatus       = svcStatus,
                RequestDate         = issuedAt,
                GatheredDate        = svcStatus is "RECOGIDO" or "EN_CAC" or "EN_PLANTA" or "CLASIFICADO"
                                      ? actualPickup.AddHours(2) : null,
                PlantEntryDate      = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                      ? issuedAt.AddDays(4).Date.AddHours(hours[(i+3) % hours.Length]) : null,
                PlannedPickupStart  = issuedAt.AddDays(2),
                PlannedPickupEnd    = issuedAt.AddDays(2).AddHours(4),
                ActualPickupStart   = svcStatus != "SOLICITADO" && svcStatus != "PLANIFICADO"
                                      ? actualPickup : null,
                ActualPickupEnd     = svcStatus != "SOLICITADO" && svcStatus != "PLANIFICADO"
                                      ? actualPickup.AddHours(1).AddMinutes(30) : null,
                ActualDeliveryStart = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                      ? issuedAt.AddDays(4).Date.AddHours(hours[(i+3) % hours.Length]) : null,
                ActualDeliveryEnd   = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                      ? issuedAt.AddDays(4).Date.AddHours(hours[(i+3) % hours.Length]).AddHours(2) : null,
                DateCreateSys       = issuedAt,
                DateModifiedSys     = issuedAt,
                SourceSystem        = Seed,
                Version             = 1,
                IdUser              = SeedUser
            };
            wms.Add(wm);

            int lineCount = 1 + (i % 3);
            for (int l = 0; l < lineCount; l++)
            {
                var residueId = wasteResidueIds[(i + l) % wasteResidueIds.Count];
                var distance  = 15 + (i * 7 + l * 11) % 335;
                var emFactor  = fuels[i % fuels.Length] == "Eléctrico" ? 0.08m : (fuels[i % fuels.Length] == "GNC" ? 0.55m : 0.90m);
                wmrs.Add(new WasteMoveResidue
                {
                    Id                                    = SeedGuid($"wmr{i+1}", l + 1),
                    IdWasteMove                           = wm.Id,
                    IdResidue                             = residueId,
                    Weight                                = 50 + (i * 31 + l * 113) % 3950,
                    MeasureUnit                           = "Kg",
                    Units                                 = 1 + l,
                    UnitPriceKg                           = 0.05m + (i % 9) * 0.05m,
                    IdTreatmentOperationDestiny           = treatOpIds.Count > 0 ? treatOpIds[(i + l) % treatOpIds.Count] : null,
                    IdCarrier                             = carrier,
                    TransportInfo_VehicleRegistration     = $"{1000 + i} ABC",
                    TransportInfo_TransportDistance       = distance,
                    TransportInfo_TransportDuration       = (decimal)(distance / 60.0),
                    TransportInfo_TransportCarbonEmissions = distance * emFactor,
                    VehicleType                           = vehicles[i % vehicles.Length],
                    FuelType                              = fuels[i % fuels.Length],
                    EuroClass                             = euros[i % euros.Length]
                });
            }
        }

        return (sos, sors, wms, wmrs);
    }

    // ── Incidents
    private static List<Incident> BuildIncidents(
        List<(Guid soId, string wmRef, Guid? soGuid)> wasteMoves, Guid ownerId, DateTime now)
    {
        var incidents = new List<Incident>();
        var t = now;

        var defs = new[]
        {
            ("WeightMismatch",       "Medium",   true),
            ("WeightMismatch",       "High",     true),
            ("WeightMismatch",       "Critical", false),
            ("WeightMismatch",       "Medium",   true),
            ("NonCompliantWaste",    "Low",      true),
            ("NonCompliantWaste",    "Medium",   true),
            ("NonCompliantWaste",    "High",     false),
            ("TransportDelay",       "Low",      true),
            ("TransportDelay",       "Low",      true),
            ("TransportDelay",       "Medium",   true),
            ("VehicleBreakdown",     "Medium",   false),
            ("VehicleBreakdown",     "High",     true),
            ("FractionContamination","High",     false),
            ("FractionContamination","Critical", false),
            ("MissingDocument",      "Medium",   true),
        };

        for (int i = 0; i < defs.Length && i < wasteMoves.Count; i++)
        {
            var (type, severity, closed) = defs[i];
            var wm = wasteMoves[i % wasteMoves.Count];
            incidents.Add(new Incident
            {
                Id                  = SeedGuid("inc", i + 1),
                OwnerId             = ownerId,
                Type                = type,
                Severity            = severity,
                OpenedAt            = t.AddDays(-60 + i * 4),
                ClosedAt            = closed ? t.AddDays(-50 + i * 4) : null,
                ServiceOrderId      = wm.soGuid,
                WasteMoveReference  = wm.wmRef,
                ReportedByName      = "Operador Demo",
                ReportedByNationalId= "12345678A",
                Description         = $"Incidencia tipo {type} en traslado {wm.wmRef}",
                ResolutionJson      = closed ? "{\"action\":\"Corregido\",\"notes\":\"Resuelto en revisión\"}" : null,
                SourceSystem        = Seed,
                Version             = 1,
                CreatedAt           = t.AddDays(-60 + i * 4),
                UpdatedAt           = t.AddDays(-60 + i * 4),
                IdUser              = SeedUser
            });
        }

        return incidents;
    }

    // ── EntryCACs ─────────────────────────────────────────────────────────────
    private static (List<EntryCAC>, List<EntryCACResidue>) BuildEntryCACs(
        List<(Guid wmId, string wmRef, DateTime? entryDate)> wasteMoves,
        List<Guid> residueIds, Guid ownerId, DateTime now)
    {
        var entries  = new List<EntryCAC>();
        var residues = new List<EntryCACResidue>();
        var containers = new[] { "Bigbag", "Contenedor", "Palé", "Granel" };
        var methods    = new[] { "Puerta a puerta", "Punto limpio", "Contenedor vía pública" };

        for (int i = 0; i < Math.Min(wasteMoves.Count, 100); i++)
        {
            var (wmId, wmRef, date) = wasteMoves[i];
            var entry = new EntryCAC
            {
                Id               = SeedGuid("ecac", i + 1),
                OwnerId          = ownerId,
                IdWasteMove      = wmId,
                WasteMoveReference = wmRef,
                CACEntryDate     = date ?? now.AddDays(-(150 - i % 150)),
                TypeContainer    = containers[i % containers.Length],
                CollectionMethod = methods[i % methods.Length],
                DateCreateSys    = now,
                DateModifiedSys  = now,
                IdUser           = SeedUser
            };
            entries.Add(entry);

            for (int l = 0; l < 1 + (i % 2); l++)
            {
                residues.Add(new EntryCACResidue
                {
                    Id          = SeedGuid($"ecacr{i+1}", l + 1),
                    IdEntryCAC  = entry.Id,
                    IdResidue   = residueIds[(i + l) % residueIds.Count],
                    Weight      = 20 + (i + l * 30) % 1480,
                    MeasureUnit = "Kg",
                    Units       = 1
                });
            }
        }

        return (entries, residues);
    }

    // ── EntryPlants ───────────────────────────────────────────────────────────
    private static (List<EntryPlant>, List<EntryPlantResidue>) BuildEntryPlants(
        List<(Guid wmId, string wmRef, Guid? soId, DateTime? entryDate)> wasteMoves,
        List<Guid> residueIds, Guid ownerId, DateTime now)
    {
        var entries  = new List<EntryPlant>();
        var residues = new List<EntryPlantResidue>();

        for (int i = 0; i < Math.Min(wasteMoves.Count, 100); i++)
        {
            var (wmId, wmRef, soId, date) = wasteMoves[i];
            var gross = 100 + (i * 43) % 4900;
            var tare  = 50 + (i * 7) % 200;
            var net   = gross - tare;
            var entry = new EntryPlant
            {
                Id               = SeedGuid("ep", i + 1),
                OwnerId          = ownerId,
                IdWasteMove      = wmId,
                WasteMoveReference = wmRef,
                ServiceOrderId   = soId,
                TicketScale      = $"TICKET-{i+1:D5}",
                PlantEntryDate   = date ?? now.AddDays(-(150 - i % 150)),
                GrossWeight      = gross,
                TareWeight       = tare,
                NetWeight        = net,
                WeighbridgeId    = $"BASCULA-0{(i % 3) + 1}",
                TypeContainer    = "Contenedor",
                DateCreateSys    = now,
                DateModifiedSys  = now,
                IdUser           = SeedUser
            };
            entries.Add(entry);

            for (int l = 0; l < 1 + (i % 2); l++)
            {
                var w = (decimal)(net / (1 + (i % 2)));
                residues.Add(new EntryPlantResidue
                {
                    Id          = SeedGuid($"epr{i+1}", l + 1),
                    IdEntryPlant = entry.Id,
                    IdResidue   = residueIds[(i + l) % residueIds.Count],
                    Weight      = w,
                    MeasureUnit = "Kg",
                    Units       = 1
                });
            }
        }

        return (entries, residues);
    }

    // ── TreatmentPlants ───────────────────────────────────────────────────────
    private static (List<TreatmentPlant>, List<TreatmentPlantResidue>) BuildTreatmentPlants(
        List<(Guid wmId, string wmRef, Guid? soId, DateTime? entryDate)> wasteMoves,
        List<Guid> treatOpIds, List<Guid> wasteResidueIds, List<Guid> productResidueIds,
        List<Guid> incidentIds, Guid ownerId, DateTime now)
    {
        var plants   = new List<TreatmentPlant>();
        var residues = new List<TreatmentPlantResidue>();
        int incIdx   = 0;

        // Distribución de operaciones de tratamiento para obtener tasas visualmente llamativas:
        // ~65% reciclaje (R3/R4/R5), ~20% valorización energética (R1), ~15% otros
        // Los índices en treatOpIds corresponden a: 0=R1(energía), 1=R2, 2=R3(recicl), 3=R4(recicl), 4=R5(recicl)...
        // Para forzar el porcentaje deseado usamos un patrón fijo de 20 elementos:
        // 13 reciclaje (R3/R4/R5), 4 valorización (R1), 3 otros
        var treatOpPattern = new int[] { 2, 3, 4, 2, 3, 4, 2, 3, 4, 2, 3, 4, 2, 0, 3, 4, 0, 2, 0, 1 };

        for (int i = 0; i < Math.Min(wasteMoves.Count, 100); i++)
        {
            var (wmId, wmRef, soId, date) = wasteMoves[i];
            var treatDate = (date ?? now.AddDays(-(150 - i % 150))).AddDays(1);

            // Vincular ~5 incidencias a plants de tipo WeightMismatch/FractionContamination
            Guid? incidentId = null;
            if (incIdx < incidentIds.Count && i < 5)
                incidentId = incidentIds[incIdx++];

            // Selección de operación según patrón para garantizar tasas de reciclaje realistas
            var opPatternIdx = treatOpPattern[i % treatOpPattern.Length];
            var selectedOpId = treatOpIds.Count > opPatternIdx ? treatOpIds[opPatternIdx]
                             : treatOpIds.Count > 0            ? treatOpIds[i % treatOpIds.Count]
                             : (Guid?)null;

            var tp = new TreatmentPlant
            {
                Id                   = SeedGuid("tp", i + 1),
                OwnerId              = ownerId,
                IdWasteMove          = wmId,
                WasteMoveReference   = wmRef,
                ServiceOrderId       = soId,
                TicketScale          = $"TICKET-TREAT-{i+1:D5}",
                PlantTreatmentDate   = treatDate,
                IdTreatmentOperation = selectedOpId,
                ImproperWeight       = (i * 3) % 50,
                QualityMetricsJson   = $"{{\"contaminationPct\":{(i % 10) * 0.5},\"moisture\":{10 + i % 15}}}",
                IncidentId           = incidentId,
                SourceSystem         = Seed,
                DateCreateSys        = now,
                DateModifiedSys      = now,
                IdUser               = SeedUser
            };
            plants.Add(tp);

            // Balance de masas: WeightReused + WeightValued + WeightRemove + ImproperWeight ≈ WeightTotal
            var improper   = tp.ImproperWeight ?? 0;
            // Variación de total para que los gráficos de evolución de pesos sean dinámicos
            var total      = 800m + (i * 137 + 1) % 4200;
            var reused     = Math.Round(total * 0.15m, 2);
            var valued     = Math.Round(total * 0.68m, 2);
            var remove     = total - reused - valued - improper;

            for (int l = 0; l < 1 + (i % 3); l++)
            {
                var inputResidue  = wasteResidueIds[(i + l) % wasteResidueIds.Count];
                var reusedResidue = productResidueIds.Count > 0
                    ? productResidueIds[(i + l) % productResidueIds.Count] : (Guid?)null;
                var valuedResidue = productResidueIds.Count > 0
                    ? productResidueIds[(i + l + 1) % productResidueIds.Count] : (Guid?)null;
                var removeResidue = wasteResidueIds[(i + l + 2) % wasteResidueIds.Count];

                residues.Add(new TreatmentPlantResidue
                {
                    Id               = SeedGuid($"tpr{i+1}", l + 1),
                    IdTreatmentPlant = tp.Id,
                    IdResidue        = inputResidue,
                    WeightTotal      = total,
                    MeasureUnit      = "Kg",
                    Units            = 1,
                    IdResidueReused  = reusedResidue,
                    WeightReused     = reused,
                    MeasureUnitReused = "Kg",
                    IdResidueValued  = valuedResidue,
                    WeightValued     = valued,
                    MeasureUnitValued = "Kg",
                    IdResidueRemove  = removeResidue,
                    WeightRemove     = remove,
                    MeasureUnitRemove = "Kg"
                });
            }
        }

        return (plants, residues);
    }

    // ── Settlements ───────────────────────────────────────────────────────────
    private static (List<Settlement>, List<SettlementLine>) BuildSettlements(
        List<(Guid agreementId, Guid? idScrap, Guid? idPublicEntity)> agreements,
        List<Guid> lerIds, Guid ownerId, DateTime now)
    {
        var settlements = new List<Settlement>();
        var lines       = new List<SettlementLine>();
        // Distribución más realista: mayoría Approved, algunos Pending, pocos Rejected
        var statuses    = new[] { "Approved", "Approved", "Approved", "Approved", "Approved",
                                   "Approved", "Approved", "Approved", "Pending", "Pending",
                                   "Pending",  "Rejected" };
        var t = now;

        // Factores mensuales para simular estacionalidad (picos en Q2-jun y Q4-nov/dic)
        var monthFactors = new decimal[]
        {
            0.75m, 0.80m, 0.95m,   // Q1: ene-mar
            1.10m, 1.20m, 1.35m,   // Q2: abr-jun (pico primaveral)
            0.90m, 0.85m, 1.00m,   // Q3: jul-sep
            1.15m, 1.30m, 1.45m    // Q4: oct-dic (pico recogida anual)
        };

        for (int i = 0; i < Math.Min(agreements.Count, 25); i++)
        {
            var (agrId, scrapId, peId) = agreements[i];
            var month       = 1 + (i % 12);
            var mFactor     = monthFactors[month - 1];
            var baseAmount  = Math.Round((8000m + (i * 2371) % 42000) * mFactor, 2);
            var adjustments = Math.Round((-800m + (i * 43) % 1600) * mFactor, 2);
            var tax         = Math.Round(baseAmount * 0.21m, 2);
            var total       = baseAmount + adjustments + tax;
            var status      = statuses[i % statuses.Length];
            var s = new Settlement
            {
                Id               = SeedGuid("set", i + 1),
                OwnerId          = ownerId,
                SettlementNumber = $"LIQ-{t.Year}-{i+1:D4}",
                Status           = status,
                AgreementId      = agrId,
                Year             = t.Year,
                Month            = month,
                IdScrap          = scrapId,
                IdPublicEntity   = peId,
                Currency         = "EUR",
                BaseAmount       = baseAmount,
                AdjustmentsAmount = adjustments,
                TaxAmount        = tax,
                TotalAmount      = total,
                ValidationStatus = status,
                ValidatedAt      = status == "Approved" ? t.AddDays(-(25 - i % 20)) : null,
                SourceSystem     = Seed,
                Version          = 1,
                CreatedAt        = t,
                UpdatedAt        = t,
                IdUser           = SeedUser
            };
            settlements.Add(s);

            for (int l = 0; l < 3 + (i % 3); l++)
            {
                var weightKg   = Math.Round((800m + (i + l * 1200) % 9200) * mFactor, 0);
                var pricePerKg = 0.05m + (l % 5) * 0.05m;
                lines.Add(new SettlementLine
                {
                    Id              = SeedGuid($"sl{i+1}", l + 1),
                    SettlementId    = s.Id,
                    ProductCategory = 1 + l % 5,
                    IdLERCode       = lerIds[(i + l) % lerIds.Count],
                    WeightKg        = weightKg,
                    PricePerKg      = pricePerKg,
                    Amount          = weightKg * pricePerKg,
                    EvidenceType    = "TicketBascula",
                    SourceIdsJson   = "[]"
                });
            }
        }

        return (settlements, lines);
    }

    // ── MarketShares ──────────────────────────────────────────────────────────
    private static List<MarketShare> BuildMarketShares(List<Guid> scraps, Guid ownerId, DateTime now)
    {
        var list       = new List<MarketShare>();
        var categories = new[] { "Envases", "RAEE", "Voluminosos" };
        // Todas las CCAA del seed para que el heatmap territorial sea visualmente rico
        var ccaas = new[]
        {
            "Aragón", "Comunidad de Madrid", "Cataluña", "Andalucía",
            "Comunitat Valenciana", "País Vasco", "Región de Murcia",
            "Castilla y León", "Illes Balears", "Cantabria",
            "Comunidad Foral de Navarra", "La Rioja", "Principado de Asturias",
            "Galicia", "Castilla-La Mancha", "Extremadura"
        };
        var flowTypes  = new[] { "Recogida", "Reciclaje", "Valorización" };

        // Pesos base por categoría (kg) — valores representativos de un SCRAP español mediano
        // Envases ~200-800t, RAEE ~100-400t, Voluminosos ~80-300t por CCAA y trimestre
        var baseWeightsByCat = new Dictionary<string, decimal>
        {
            ["Envases"]     = 450_000m,
            ["RAEE"]        = 250_000m,
            ["Voluminosos"] = 180_000m
        };

        // Factor de crecimiento por CCAA (más densidad de población = más peso)
        var ccaaFactor = new decimal[]
        {
            0.80m, 2.20m, 1.90m, 1.60m,  // Aragón, Madrid, Cataluña, Andalucía
            1.30m, 1.10m, 0.70m, 0.75m,  // Valencia, País Vasco, Murcia, Castilla y León
            0.45m, 0.35m, 0.40m, 0.28m,  // Baleares, Cantabria, Navarra, La Rioja
            0.38m, 0.90m, 0.65m, 0.50m   // Asturias, Galicia, Castilla-LM, Extremadura
        };

        int idx = 0;
        for (int s = 0; s < scraps.Count; s++)
        {
            foreach (var cat in categories)
            {
                var baseW = baseWeightsByCat[cat];
                for (int c = 0; c < ccaas.Length; c++)
                {
                    // Cuatro períodos trimestrales con tendencia creciente (Q1<Q2<Q3<Q4)
                    for (int period = 1; period <= 4; period++)
                    {
                        idx++;
                        // Tendencia: Q4 = 1.25× Q1, con variación aleatoria determinista
                        var trendFactor = 0.88m + period * 0.12m;
                        var variance    = 1m + ((idx * 7919) % 21 - 10) * 0.02m; // ±20% pseudo-aleatorio
                        var weight      = Math.Round(baseW * ccaaFactor[c] * trendFactor * variance / scraps.Count, 0);
                        // Mínimo razonable por CCAA pequeña
                        if (weight < 15_000m) weight = 15_000m + idx % 10_000;

                        var qMonth = (period - 1) * 3 + 1;
                        list.Add(new MarketShare
                        {
                            Id                  = SeedGuid("ms", idx),
                            OwnerId             = ownerId,
                            IdScrap             = scraps[s],
                            Category            = cat,
                            AutonomousCommunity = ccaas[c],
                            Year                = now.Year,
                            Weight              = weight,
                            Period              = period,
                            EffectiveFrom       = new DateOnly(now.Year, qMonth, 1),
                            EffectiveTo         = new DateOnly(now.Year, qMonth + 2, DateTime.DaysInMonth(now.Year, qMonth + 2)),
                            FlowType            = flowTypes[(s + c + period) % flowTypes.Length],
                            SourceSystem        = Seed,
                            Version             = 1
                        });
                    }
                }
            }
        }

        return list;
    }

    // ── ProductDeclarations ───────────────────────────────────────────────────
    private static (List<ProductDeclaration>, List<Product>) BuildProductDeclarations(
        List<(Guid producerId, string centerCode)> producers, List<Guid> productResidueIds, Guid ownerId, DateTime now)
    {
        var declarations = new List<ProductDeclaration>();
        var products     = new List<Product>();
        var states       = new[] { "Borrador", "Emitido", "Validado", "Rechazado" };
        var types        = new[] { "DeclaraciónAnual", "DeclaraciónTrimestral" };

        // Datos para completar las líneas de producto: categoría, origen y uso
        var productCategories = new[] { "Envases", "RAEE", "Voluminosos", "Metales", "Papel" };
        var productSources    = new[]
        {
            "Producción propia", "Importación", "Producción propia",
            "Fabricación nacional", "Distribución"
        };
        var productUses = new[]
        {
            "Beverage", "Electronics", "Packaging", "WhiteGoods",
            "Food", "Logistics", "Mobile", "Consumable", "Battery", "Lighting"
        };

        int declIdx = 0;
        foreach (var (producerId, centerCode) in producers)
        {
            for (int d = 0; d < 3 + (declIdx % 2); d++)
            {
                declIdx++;
                var state = states[declIdx % states.Length];
                var decl  = new ProductDeclaration
                {
                    Id          = SeedGuid("pd", declIdx),
                    OwnerId     = ownerId,
                    IdProducer  = producerId,
                    Period      = 1 + (declIdx % 4),
                    Year        = now.Year,
                    Month       = 1 + (declIdx % 12),
                    Currency    = "EUR",
                    State       = state,
                    Type        = types[declIdx % types.Length],
                    Reference   = $"DECL-{centerCode}-{now.Year}-{d+1:D2}",
                    Amount      = 1000m + (declIdx * 3721) % 99000,
                    DateCreate  = now.AddDays(-90 + declIdx),
                    DateEmit    = state != "Borrador" ? now.AddDays(-80 + declIdx) : null,
                    DateCreateSys   = now,
                    DateModifiedSys = now,
                    IdUser      = SeedUser
                };
                declarations.Add(decl);

                for (int p = 0; p < 2 + (declIdx % 4); p++)
                {
                    products.Add(new Product
                    {
                        Id                    = SeedGuid($"pr{declIdx}", p + 1),
                        IdProductDeclaration  = decl.Id,
                        IdResidue             = productResidueIds.Count > 0
                            ? productResidueIds[(declIdx + p) % productResidueIds.Count] : null,
                        ProductName           = $"Producto Demo {p+1}",
                        Reference             = $"REF-PROD-{p+1:D3}",
                        Source                = productSources[(declIdx + p) % productSources.Length],
                        ProductCategory       = productCategories[(declIdx + p) % productCategories.Length],
                        ProductUse            = productUses[(declIdx + p) % productUses.Length],
                        Quantity              = 100m + (declIdx + p * 500) % 9900,
                        MeasureUnit           = "Kg",
                        Units                 = 10 + p * 20,
                        Price                 = 1m + (p * 7) % 49
                    });
                }
            }
        }

        return (declarations, products);
    }

    // =========================================================================
    // FASE 10 — Objetivos regulatorios
    // =========================================================================
    private async Task Phase10_RegulatoryTargetsAsync(CancellationToken ct)
    {
        if (await _db.RegulatoryTargets.AnyAsync(x => x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 10 — skip (RegulatoryTargets ya existen)");
            return;
        }

        var targets = new[]
        {
            ("Grandes aparatos", 55.0, 5.0),
            ("Pantallas",        65.0, 5.0),
            ("Pequeños aparatos",55.0, 5.0),
            ("Iluminación",      80.0, 5.0),
            ("RAEE",             55.0, 5.0),
            ("Envases",          70.0, 5.0),
            ("Metales",          70.0, 0.0),
        }.Select((r, i) => new RegulatoryTarget
        {
            Id                  = SeedGuid("rt", i + 1),
            OwnerId             = _ownerId,
            Category            = r.Item1,
            Year                = _now.Year,
            MinRecyclingPercent = r.Item2,
            MinReusePercent     = r.Item3
        }).ToList();

        _db.RegulatoryTargets.AddRange(targets);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 10 completada — {N} RegulatoryTargets", targets.Count);
    }

    // =========================================================================
    // FASE 11 — Energía de plantas
    // =========================================================================
    private async Task Phase11_PlantEnergiesAsync(CancellationToken ct)
    {
        if (await _db.PlantEnergies.AnyAsync(x => x.SourceSystem == Seed && x.OwnerId == _ownerId, ct))
        {
            _log.LogInformation("  Fase 11 — skip (PlantEnergies ya existen)");
            return;
        }

        var plants = await _db.BusinessEntities
            .Where(x => x.SourceSystem == Seed && x.EntityRole == "Plant")
            .Select(x => new { x.Name, x.CenterCode })
            .ToListAsync(ct);

        var energies = new List<PlantEnergy>();
        // Variación estacional: más consumo en invierno/verano
        var monthlyFactor = new[] { 1.3m, 1.2m, 1.0m, 0.9m, 0.85m, 0.8m, 0.8m, 0.85m, 0.9m, 1.0m, 1.1m, 1.25m };
        int idx = 0;
        foreach (var p in plants)
        {
            for (int m = 0; m < 6; m++)
            {
                var targetMonth = _now.AddMonths(-(5 - m));
                var baseKwh = 20000m + (idx * 5000) % 25000;
                energies.Add(new PlantEnergy
                {
                    Id              = SeedGuid("pe_e", ++idx),
                    OwnerId         = _ownerId,
                    PlantName       = p.Name,
                    PlantCenterCode = p.CenterCode,
                    Year            = targetMonth.Year,
                    Month           = targetMonth.Month,
                    KwhTotal        = Math.Round(baseKwh * monthlyFactor[(targetMonth.Month - 1) % 12], 0),
                    Source          = "Red eléctrica",
                    GridMixRef      = "REE-HIDRÓGENO-2024",
                    SourceSystem    = Seed,
                    Version         = 1,
                    CreatedAt       = _now,
                    UpdatedAt       = _now,
                    IdUser          = SeedUser
                });
            }
        }

        _db.PlantEnergies.AddRange(energies);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 11 completada — {N} PlantEnergies", energies.Count);
    }

    // =========================================================================
    // UTILIDADES
    // =========================================================================

    /// <summary>
    /// Genera GUIDs deterministas por prefijo + índice para facilitar el debugging.
    /// Formato: 0000XXXX-0000-0000-0000-YYYYYYYYYYYY donde X=hash del prefix, Y=index
    /// </summary>
    private static Guid SeedGuid(string prefix, int index)
    {
        var hashBytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(prefix));
        var prefixShort = (ushort)((hashBytes[0] << 8) | hashBytes[1]);
        return new Guid(
            (uint)(prefixShort << 16) | (uint)(index & 0xFFFF),
            (ushort)(prefixShort >> 4),
            0x5EED,
            0x80, 0x00,
            (byte)((index >> 32) & 0xFF),
            (byte)((index >> 24) & 0xFF),
            (byte)((index >> 16) & 0xFF),
            (byte)((index >> 8) & 0xFF),
            (byte)((index >> 4) & 0xFF),
            (byte)(index & 0xFF));
    }
}
