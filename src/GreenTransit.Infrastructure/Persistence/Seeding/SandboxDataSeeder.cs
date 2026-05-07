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
    private static readonly DateTime Now = DateTime.UtcNow;

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
            await Phase9_DumAndEcoAsync(ct);
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
        _log.LogInformation("🧹 SandboxDataSeeder — limpieza (OwnerId={OwnerId})", _ownerId);
        _db.IgnoreTenantFilter();
        try
        {
            // Orden inverso de FK
            await _db.TreatmentPlantResidues
                .Where(x => x.TreatmentPlant.SourceSystem == Seed).ExecuteDeleteAsync(ct);
            await _db.TreatmentPlants
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryPlantResidues
                .Where(x => x.EntryPlant.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryPlants
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACResidues
                .Where(x => x.EntryCAC.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EntryCACs
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.WasteMoveResidues
                .Where(x => x.WasteMove.SourceSystem == Seed).ExecuteDeleteAsync(ct);
            await _db.WasteMoves
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ServiceOrders
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Incidents
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.SettlementLines
                .Where(x => x.Settlement.SourceSystem == Seed).ExecuteDeleteAsync(ct);
            await _db.Settlements
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.MarketShares
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Products
                .Where(x => x.ProductDeclaration.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.ProductDeclarations
                .Where(x => x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.AgreementDocuments
                .Where(x => x.Agreement.SourceSystem == Seed).ExecuteDeleteAsync(ct);
            await _db.Agreements
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.Residues
                .Where(x => x.SourceSystem == Seed).ExecuteDeleteAsync(ct);

            // Borrar usuarios seed (@greentransit.dev) preservando el admin
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

            // DUM y Ecomodulación
            await _db.DumRestrictionRules
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.DumZones
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRules
                .Where(x => x.RuleSet.SourceSystem == Seed && x.RuleSet.OwnerId == _ownerId).ExecuteDeleteAsync(ct);
            await _db.EcoModulationRuleSets
                .Where(x => x.SourceSystem == Seed && x.OwnerId == _ownerId).ExecuteDeleteAsync(ct);

            await _db.TreatmentOperations
                .Where(x => x.CreatedAt == Now.Date).ExecuteDeleteAsync(ct);
            await _db.LerCodes
                .Where(x => !x.IsActive).ExecuteDeleteAsync(ct);

            _log.LogInformation("✅ SandboxDataSeeder — limpieza completada");
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
        await SeedTreatmentOperationsAsync(ct);
        await SeedLerCodesAsync(ct);
        await SeedEmissionFactorSetAsync(ct);
        _log.LogInformation("  Fase 0 completada en {Ms}ms", sw.ElapsedMilliseconds);
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
            FactorSetName = "Set Demo 2026",
            Version       = "1.0",
            Status        = "Active",
            ValidFrom     = new DateTime(2026, 1, 1),
            Publisher     = "GreenTransit Seed",
            CreatedAt     = Now,
            UpdatedAt     = Now,
            IdUser        = SeedUser
        };
        _db.EmissionFactorSets.Add(set);
        foreach (var ef in BuildEmissionFactors(set.Id))
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

        var entities = BuildEntities(_ownerId);
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
        _log.LogInformation("  Fase 2 completada — {N} residuos en {Ms}ms",
            residues.Count, sw.ElapsedMilliseconds);
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

        var (agreements, docs) = BuildAgreements(scraps, publicEntities, coordinator, _ownerId);
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

        var (serviceOrders, wasteMoves, wmResidues) = BuildOperations(
            producers, publicEnts, carriers, plants, cacs, scraps, opTransfers,
            lerIds, wasteResidueIds, treatOpIds, _ownerId);

        _db.ServiceOrders.AddRange(serviceOrders);
        await _db.SaveChangesAsync(ct);
        _db.WasteMoves.AddRange(wasteMoves);
        await _db.SaveChangesAsync(ct);
        _db.WasteMoveResidues.AddRange(wmResidues);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("  Fase 4 completada — {SO} SO, {WM} WM, {WMR} WMR en {Ms}ms",
            serviceOrders.Count, wasteMoves.Count, wmResidues.Count, sw.ElapsedMilliseconds);
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

        var incidents = BuildIncidents(wasteMoves.Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId)).ToList(), _ownerId);
        _db.Incidents.AddRange(incidents);
        await _db.SaveChangesAsync(ct);

        var closedIncidentIds = incidents.Where(x => x.ClosedAt != null).Select(x => x.Id).ToList();

        var (entryCACs, cacResidues) = BuildEntryCACs(
            wasteMoves.Where(x => x.ServiceStatus is "EN_CAC" or "EN_PLANTA" or "RECOGIDO" or "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.PlantEntryDate)).ToList(),
            wasteResidueIds, _ownerId);

        var (entryPlants, plantResidues) = BuildEntryPlants(
            wasteMoves.Where(x => x.ServiceStatus is "EN_PLANTA" or "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId, x.PlantEntryDate)).ToList(),
            wasteResidueIds, _ownerId);

        var (treatPlants, treatResidues) = BuildTreatmentPlants(
            wasteMoves.Where(x => x.ServiceStatus == "CLASIFICADO").ToList()
                      .Select(x => (x.Id, x.WasteMoveReference ?? "", x.ServiceOrderId, x.PlantEntryDate)).ToList(),
            treatOpIds, wasteResidueIds, productResidueIds, closedIncidentIds, _ownerId);

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
            agreements.Select(a => (a.Id, a.IdScrap, a.IdPublicEntity)).ToList(), lerIds, _ownerId);
        var marketShares = BuildMarketShares(scraps, _ownerId);

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
            producers.Select(p => (p.Id, p.CenterCode ?? "CC")).ToList(), productResidueIds, _ownerId);

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
            _log.LogInformation("  Fase 8 — skip (usuarios seed ya existen)");
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

        // Cargar entidades seeded
        var entities = await _db.BusinessEntities
            .Where(e => e.SourceSystem == Seed)
            .Select(e => new { e.Id, e.Name, e.EntityRole })
            .ToListAsync(ct);

        var users = new List<AppUser>();
        foreach (var entity in entities)
        {
            if (!roleToProfile.TryGetValue(entity.EntityRole ?? "", out var profileRef)) continue;
            if (!profiles.TryGetValue(profileRef, out var profileId)) continue;

            // Normalizar nombre → slug para email: "Productor Demo 01" → "productor.demo.01"
            var slug = NormalizeSlug(entity.Name ?? $"entity{entity.Id}");
            var email = $"{slug}@greentransit.dev";

            users.Add(new AppUser
            {
                Login        = email,
                Email        = email,
                CompleteName = entity.Name,
                IdProfile    = profileId,
                OwnerId      = _ownerId,
                IsActive     = true,
                CreateDate   = Now,
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

        var (zones, restrictions) = BuildDumZones(_ownerId);
        _db.DumZones.AddRange(zones);
        await _db.SaveChangesAsync(ct);
        _db.DumRestrictionRules.AddRange(restrictions);
        await _db.SaveChangesAsync(ct);

        var (ruleSets, ecoRules) = BuildEcoModulation(_ownerId);
        _db.EcoModulationRuleSets.AddRange(ruleSets);
        await _db.SaveChangesAsync(ct);
        _db.EcoModulationRules.AddRange(ecoRules);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "  Fase 9 completada — {Z} DumZones, {R} Restricciones, {RS} EcoRuleSets, {ER} EcoRules en {Ms}ms",
            zones.Count, restrictions.Count, ruleSets.Count, ecoRules.Count, sw.ElapsedMilliseconds);
    }

    // ── DumZones + DumRestrictionRules ────────────────────────────────────────
    private static (List<DumZone>, List<DumRestrictionRule>) BuildDumZones(Guid ownerId)
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
                CreatedAt    = Now,
                UpdatedAt    = Now,
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
                ValidFrom      = new DateTime(2026, 1, 1),
                ConditionsJson = "{\"days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"],\"startHour\":21,\"endHour\":7,\"vehicleTypes\":[\"Camión 12t\",\"Camión 26t\"]}",
                ActionType     = "Deny",
                ActionReason   = "Restricción horaria nocturna",
                SourceSystem   = Seed,
                Version        = 1,
                CreatedAt      = Now,
                UpdatedAt      = Now,
            });

            // Regla 2: límite de tonelaje
            rules.Add(new DumRestrictionRule
            {
                Id             = SeedGuid("drr", i * 2 + 2),
                OwnerId        = ownerId,
                ZoneId         = zoneId,
                RuleCode       = $"{code}-R02",
                Status         = "Active",
                ValidFrom      = new DateTime(2026, 1, 1),
                ConditionsJson = "{\"maxWeightTon\":3.5,\"vehicleTypes\":[\"Camión 26t\"]}",
                ActionType     = "Restrict",
                ActionReason   = "Límite de tonelaje en zona urbana",
                SourceSystem   = Seed,
                Version        = 1,
                CreatedAt      = Now,
                UpdatedAt      = Now,
            });
        }

        return (zones, rules);
    }

    // ── EcoModulationRuleSets + EcoModulationRules ────────────────────────────
    private static (List<EcoModulationRuleSet>, List<EcoModulationRule>) BuildEcoModulation(Guid ownerId)
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
                ValidFrom           = new DateTime(2026, 1, 1),
                PublisherName       = "GreenTransit Demo",
                PublisherNationalId = $"B0000000{s + 1}",
                PublisherCenterCode = $"CC-DEMO-{s + 1:D2}",
                SourceSystem        = Seed,
                Hash                = $"seed-{code}",
                CreatedAt           = Now,
                UpdatedAt           = Now,
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
                    CreatedAt       = Now,
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
            CreatedAt             = Now,
            UpdatedAt             = Now
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
            CreatedAt              = Now,
            UpdatedAt              = Now
        }).ToList();
    }

    // ── EmissionFactors ───────────────────────────────────────────────────────
    private static List<EmissionFactor> BuildEmissionFactors(Guid setId)
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
            CreatedAt   = Now
        }).ToList();
    }

    // ── Entities ──────────────────────────────────────────────────────────────
    private static List<BusinessEntity> BuildEntities(Guid ownerId)
    {
        var list = new List<BusinessEntity>();
        var t = Now;

        // 10 Productores
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
        };
        for (int i = 0; i < 10; i++)
        {
            var (city, sc, zip, prov, mun, lat, lon) = producerCities[i];
            list.Add(Entity(SeedGuid("prod", i + 1), $"Productor Demo {i+1:D2}", $"B1234560{i+1}", $"NIMA-P{i+1:D3}",
                "Producer", "SL", sc, zip, prov, mun,
                $"Calle Industria {i+1}, {city}", lat, lon,
                $"+34 976 0000{i+1:D2}", $"prod{i+1:D2}@greentransit.test", $"Contacto Productor {i+1}", t));
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

        // 5 SCRAP
        var scrapData = new[]
        {
            ("EcoEnvases SCRAP",  "Envases"),
            ("ReciclaRAEE SCRAP", "RAEE"),
            ("GreenPack SCRAP",   "Voluminosos"),
            ("EnviroPlas SCRAP",  "Plásticos"),
            ("MetalCiclo SCRAP",  "Metales"),
        };
        for (int i = 0; i < 5; i++)
        {
            var (name, _) = scrapData[i];
            list.Add(Entity(SeedGuid("scrap", i + 1), name, $"A8800{i+1:D4}", $"NIMA-SC{i+1:D3}",
                "SCRAP", "Asociación", "MD", "28001", "28", "28079",
                $"Paseo Castellana {i+1}, Madrid", "40.4168", "-3.7038",
                $"+34 91 000 00{i+1}", $"scrap{i+1}@greentransit.test", $"Dir. SCRAP {i+1}", t));
        }

        // 5 PublicEntity
        var peData = new[]
        {
            ("Ayuntamiento de Zaragoza",  "AR", "50001", "50", "50297", "41.6488", "-0.8891"),
            ("Ayuntamiento de Madrid",    "MD", "28001", "28", "28079", "40.4168", "-3.7038"),
            ("Ajuntament de Barcelona",   "CT", "08001", "08", "08019", "41.3851", "2.1734"),
            ("Ayuntamiento de Sevilla",   "AN", "41001", "41", "41091", "37.3891", "-5.9845"),
            ("Ayuntamiento de Valencia",  "VC", "46001", "46", "46250", "39.4699", "-0.3763"),
        };
        for (int i = 0; i < 5; i++)
        {
            var (name, sc, zip, prov, mun, lat, lon) = peData[i];
            list.Add(Entity(SeedGuid("pe", i + 1), name, $"P0000{i+1:D4}", null,
                "PublicEntity", "Ayuntamiento", sc, zip, prov, mun,
                $"Plaza Mayor {i+1}", lat, lon,
                $"+34 9{i+1}0 000 001", $"pe{i+1}@greentransit.test", $"Técnico Medio Ambiente {i+1}", t));
        }

        // 5 Carrier
        for (int i = 0; i < 5; i++)
        {
            list.Add(Entity(SeedGuid("carr", i + 1), $"Transportes Demo {i+1} SL", $"B7700{i+1:D4}", $"NIMA-CR{i+1:D3}",
                "Carrier", "SL", "AR", "50001", "50", "50297",
                $"Carretera Nacional {i+1} km {i*10}", "41.6488", "-0.8891",
                $"+34 976 222 00{i+1}", $"carrier{i+1}@greentransit.test", $"Jefe Tráfico {i+1}", t,
                inscriptionNumber: $"CR-REG-{i+1:D4}"));
        }

        // 5 CAC
        var cacCoords = new[]
        {
            ("41.6500", "-0.8850"), ("40.4200", "-3.7100"), ("41.3900", "2.1800"),
            ("37.3900", "-5.9900"), ("39.4750", "-0.3800"),
        };
        for (int i = 0; i < 5; i++)
        {
            var (lat, lon) = cacCoords[i];
            list.Add(Entity(SeedGuid("cac", i + 1), $"Centro Acopio Demo {i+1}", $"B5500{i+1:D4}", $"NIMA-CAC{i+1:D3}",
                "CAC", "SL", "AR", "50001", "50", "50297",
                $"Camino del Barranco {i+1}", lat, lon,
                $"+34 976 333 00{i+1}", $"cac{i+1}@greentransit.test", $"Encargado CAC {i+1}", t));
        }

        // 5 Plant
        var plantCoords = new[]
        {
            ("41.5800", "-0.9300"), ("40.3800", "-3.8000"), ("41.4500", "2.2000"),
            ("37.4200", "-5.8500"), ("39.5200", "-0.4200"),
        };
        for (int i = 0; i < 5; i++)
        {
            var (lat, lon) = plantCoords[i];
            list.Add(Entity(SeedGuid("plant", i + 1), $"Planta Tratamiento Demo {i+1}", $"B3300{i+1:D4}", $"NIMA-PL{i+1:D3}",
                "Plant", "SA", "AR", "50001", "50", "50297",
                $"Polígono Industrial Demo {i+1}", lat, lon,
                $"+34 976 444 00{i+1}", $"plant{i+1}@greentransit.test", $"Director Planta {i+1}", t));
        }

        // 1 Coordinator
        list.Add(Entity(SeedGuid("coord", 1), "Coordinador Demo COORD-01", "G1100001", null,
            "Coordinator", "Fundación", "MD", "28001", "28", "28079",
            "Calle Velázquez 10, Madrid", "40.4200", "-3.6900",
            "+34 91 555 0001", "coord@greentransit.test", "Coordinador General", t));

        // 1 Other (Oficina Asignación)
        list.Add(Entity(SeedGuid("office", 1), "Oficina de Asignación OFFICE-01", "G2200001", null,
            "Other", "Gestora", "MD", "28001", "28", "28079",
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
        var t = Now;

        // Waste (20)
        var wasteDef = new[]
        {
            ("Papel y cartón mezclado",       "150101", "Recogida", false, false),
            ("Plástico mezclado",             "150102", "Recogida", false, false),
            ("Madera reciclable",             "150103", "Recogida", false, false),
            ("Envases metálicos mixtos",      "150104", "Recogida", false, false),
            ("Vidrio envases",               "150107", "Recogida", false, false),
            ("Envase peligroso contaminado",  "150110", "Recogida", true,  false),
            ("RAEE grande",                  "160213", "RAEE",     true,  true),
            ("RAEE pequeño",                 "160214", "RAEE",     false, true),
            ("Cobre chatarra",               "170401", "Reciclaje", false, false),
            ("Hierro y acero",               "170405", "Reciclaje", false, false),
            ("Papel oficina",                "200101", "Recogida",  false, false),
            ("Vidrio doméstico",             "200102", "Recogida",  false, false),
            ("Orgánico cocina",              "200108", "Recogida",  false, false),
            ("RAEE municipal",               "200136", "RAEE",     false, true),
            ("Residuos mixtos",              "200301", "Recogida",  false, false),
            ("Chatarra metálica",            "170401", "Valorización", false, false),
            ("Plástico industrial",          "150102", "Reciclaje",  false, false),
            ("Cartón ondulado",              "150101", "Reciclaje",  false, false),
            ("Equipo informático obsoleto",  "160214", "RAEE",     false, true),
            ("Cable eléctrico",              "170411", "Valorización", false, false),
        };
        for (int i = 0; i < wasteDef.Length; i++)
        {
            var (name, lerCode, flow, danger, raee) = wasteDef[i];
            lerByCode.TryGetValue(lerCode, out var lerId);
            list.Add(new Residue
            {
                Id              = SeedGuid("rw", i + 1),
                ResidueType     = "Waste",
                Name            = name,
                FlowType        = flow,
                IdLERCode       = lerId == Guid.Empty ? null : lerId,
                IsDangerous     = danger,
                IsRAEE          = raee,
                IdProducer      = producerIds.Count > 0 ? producerIds[i % producerIds.Count] : null,
                IsActive        = true,
                SourceSystem    = Seed,
                Version         = 1,
                CreatedAt       = t,
                UpdatedAt       = t,
                IdUser          = SeedUser
            });
        }

        // Product (15)
        var productDef = new[]
        {
            ("Botella PET 1.5L",          "Envases",    "Beverage"),
            ("Lata aluminio 330ml",       "Envases",    "Beverage"),
            ("Caja cartón 30x20x15cm",    "Envases",    "Packaging"),
            ("Botella vidrio 75cl",       "Envases",    "Beverage"),
            ("Envase plástico 5kg",       "Envases",    "Food"),
            ("Ordenador portátil",        "RAEE",       "Electronics"),
            ("Televisor LED 42\"",        "RAEE",       "Electronics"),
            ("Frigorífico A++",           "RAEE",       "WhiteGoods"),
            ("Lavadora 8kg",              "RAEE",       "WhiteGoods"),
            ("Móvil smartphone",          "RAEE",       "Mobile"),
            ("Palé madera",               "Envases",    "Logistics"),
            ("Film estirable",            "Envases",    "Packaging"),
            ("Tóner impresora",           "RAEE",       "Consumable"),
            ("Batería Li-ion",            "RAEE",       "Battery"),
            ("Luminaria LED",             "RAEE",       "Lighting"),
        };
        for (int i = 0; i < productDef.Length; i++)
        {
            var (name, category, use) = productDef[i];
            list.Add(new Residue
            {
                Id              = SeedGuid("rp", i + 1),
                ResidueType     = "Product",
                Name            = name,
                ProductCategory = category,
                ProductUse      = use,
                WeightPerUnitKg = 0.5m + i * 0.3m,
                RecycledContentPercent = 10 + i * 3,
                IdProducer      = producerIds.Count > 0 ? producerIds[i % producerIds.Count] : null,
                IsActive        = true,
                SourceSystem    = Seed,
                Version         = 1,
                CreatedAt       = t,
                UpdatedAt       = t,
                IdUser          = SeedUser
            });
        }

        // ProductSpec (5)
        for (int i = 0; i < 5; i++)
        {
            list.Add(new Residue
            {
                Id              = SeedGuid("rps", i + 1),
                ResidueType     = "ProductSpec",
                Name            = $"Ficha Técnica Demo {i+1}",
                ProductCategory = "Envases",
                IdProducer      = producerIds.Count > 0 ? producerIds[i % producerIds.Count] : null,
                IsActive        = true,
                SourceSystem    = Seed,
                Version         = 1,
                CreatedAt       = t,
                UpdatedAt       = t,
                IdUser          = SeedUser
            });
        }

        return list;
    }

    // ── Agreements ────────────────────────────────────────────────────────────
    private static (List<Agreement>, List<AgreementDocument>) BuildAgreements(
        List<Guid> scraps, List<Guid> publicEntities, Guid coordinator, Guid ownerId)
    {
        var agreements = new List<Agreement>();
        var docs       = new List<AgreementDocument>();
        var t = Now;
        var wasteStreams = new[] { "Envases", "RAEE", "Voluminosos", "Plásticos", "Metales" };
        var ccaas       = new[] { "Aragón", "Madrid", "Cataluña", "Andalucía", "Comunidad Valenciana" };

        int idx = 0;
        for (int s = 0; s < Math.Min(scraps.Count, 5); s++)
        {
            for (int p = 0; p < Math.Min(publicEntities.Count, 5); p++)
            {
                idx++;
                var status = idx <= 20 ? "Active" : idx <= 23 ? "Expired" : "Draft";
                var agr = new Agreement
                {
                    Id              = SeedGuid("agr", idx),
                    OwnerId         = ownerId,
                    AgreementNumber = $"AGR-2026-{idx:D4}",
                    Status          = status,
                    EffectiveFrom   = new DateTime(2026, 1, 1),
                    EffectiveTo     = status == "Active" ? null : new DateTime(2026, 12, 31),
                    IdScrap         = scraps[s],
                    IdPublicEntity  = publicEntities[p],
                    IdCoordinator   = coordinator,
                    WasteStream     = wasteStreams[s],
                    SubStream       = s % 2 == 0 ? "Plástico" : "Metal",
                    AutonomousCommunity = ccaas[p],
                    Currency        = "EUR",
                    TariffModelType = idx % 3 == 0 ? "PorUnidad" : "PorPeso",
                    TariffRulesJson = "{\"pricePerKg\":0.15,\"minWeight\":1000}",
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
    private static (List<ServiceOrder>, List<WasteMove>, List<WasteMoveResidue>) BuildOperations(
        List<Guid> producers, List<Guid> publicEnts, List<Guid> carriers,
        List<Guid> plants, List<Guid> cacs, List<Guid> scraps, List<Guid> opTransfers,
        List<Guid> lerIds, List<Guid> wasteResidueIds, List<Guid> treatOpIds, Guid ownerId)
    {
        var sos     = new List<ServiceOrder>();
        var wms     = new List<WasteMove>();
        var wmrs    = new List<WasteMoveResidue>();
        var statuses  = new[] { "Active", "Completed", "Cancelled" };
        var priorities = new[] { "Normal", "Normal", "Normal", "Normal", "High", "High", "High", "Urgent" };
        var serviceStatuses = new[]
        {
            "SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO","SOLICITADO",
            "PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO","PLANIFICADO",
            "RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO","RECOGIDO",
            "EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC","EN_CAC",
            "EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA","EN_PLANTA",
            "CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO","CLASIFICADO",
        };
        var vehicles = new[] { "Camión 12t", "Camión 26t", "Furgón" };
        var fuels    = new[] { "Diesel", "GNC", "Eléctrico" };
        var euros    = new[] { "Euro4", "Euro5", "Euro6" };

        var allIssuers = producers.Concat(publicEnts).ToList();

        for (int i = 0; i < 100; i++)
        {
            // Distribución uniforme sobre 150 días (ene-may 2026)
            // i=0 → 1-Jan, i=50 → ~17-Mar, i=87 → ~11-May, i=99 → ~29-May
            var issuedAt = new DateTime(2026, 1, 1).AddDays((i * 150) / 100);
            var issuedBy = allIssuers[i % allIssuers.Count];
            var carrier  = carriers.Count > 0 ? carriers[i % carriers.Count] : (Guid?)null;
            var plant    = plants[i % plants.Count];
            var ler      = lerIds[i % lerIds.Count];
            var scrap    = scraps[i % scraps.Count];

            var so = new ServiceOrder
            {
                Id                   = SeedGuid("so", i + 1),
                OwnerId              = ownerId,
                ServiceOrderNumber   = $"SO-2026-{i+1:D5}",
                IssuedAt             = issuedAt,
                IdIssuedBy           = issuedBy,
                Status               = statuses[i % statuses.Length],
                Priority             = priorities[i % priorities.Length],
                WasteStream          = "Envases",
                SubStream            = i % 2 == 0 ? "Plástico" : "Metal",
                IdLERCode            = ler,
                IdPickupPoint        = issuedBy,
                PlannedPickupStart   = issuedAt.AddDays(2),
                PlannedPickupEnd     = issuedAt.AddDays(3),
                PlannedDeliveryStart = issuedAt.AddDays(3),
                PlannedDeliveryEnd   = issuedAt.AddDays(4),
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

            var svcStatus = i < serviceStatuses.Length ? serviceStatuses[i] : "SOLICITADO";
            var opTransfer = opTransfers.Count > 0 && i % 3 == 0 ? opTransfers[i % opTransfers.Count] : (Guid?)null;
            var wm = new WasteMove
            {
                Id                 = SeedGuid("wm", i + 1),
                OwnerId            = ownerId,
                WasteMoveReference = $"WM-2026-{i+1:D5}",
                Lot                = $"LOT-{i+1:D3}",
                ServiceOrderId     = so.Id,
                IdScrap            = scrap,
                IdSource           = issuedBy,
                IdDestination      = plant,
                IdOperatorTransfer = opTransfer,
                ServiceStatus      = svcStatus,
                RequestDate        = issuedAt,
                GatheredDate       = svcStatus is "RECOGIDO" or "EN_CAC" or "EN_PLANTA" or "CLASIFICADO"
                                     ? issuedAt.AddDays(3) : null,
                PlantEntryDate     = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                     ? issuedAt.AddDays(4) : null,
                PlannedPickupStart  = issuedAt.AddDays(2),
                PlannedPickupEnd    = issuedAt.AddDays(3),
                ActualPickupStart   = svcStatus != "SOLICITADO" && svcStatus != "PLANIFICADO"
                                      ? issuedAt.AddDays(2).AddHours(1) : null,
                ActualPickupEnd     = svcStatus != "SOLICITADO" && svcStatus != "PLANIFICADO"
                                      ? issuedAt.AddDays(3) : null,
                ActualDeliveryStart = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                      ? issuedAt.AddDays(3).AddHours(2) : null,
                ActualDeliveryEnd   = svcStatus is "EN_PLANTA" or "CLASIFICADO"
                                      ? issuedAt.AddDays(4) : null,
                DateCreateSys      = issuedAt,
                DateModifiedSys    = issuedAt,
                SourceSystem       = Seed,
                Version            = 1,
                IdUser             = SeedUser
            };
            wms.Add(wm);

            // 1–3 WasteMoveResidues por traslado
            int lineCount = 1 + (i % 3);
            for (int l = 0; l < lineCount; l++)
            {
                var residueId = wasteResidueIds[(i + l) % wasteResidueIds.Count];
                var distance  = 10 + (i * 3 + l * 7) % 290;
                wmrs.Add(new WasteMoveResidue
                {
                    Id                                  = SeedGuid($"wmr{i+1}", l + 1),
                    IdWasteMove                         = wm.Id,
                    IdResidue                           = residueId,
                    Weight                              = 50 + (i + l * 50) % 2950,
                    MeasureUnit                         = "Kg",
                    Units                               = 1 + l,
                    UnitPriceKg                         = 0.05m + (i % 9) * 0.05m,
                    IdTreatmentOperationDestiny         = treatOpIds.Count > 0 ? treatOpIds[l % treatOpIds.Count] : null,
                    IdCarrier                           = carrier,
                    TransportInfo_VehicleRegistration   = $"{1000 + i} ABC",
                    TransportInfo_TransportDistance     = distance,
                    TransportInfo_TransportDuration     = (decimal)(distance / 60.0),
                    TransportInfo_TransportCarbonEmissions = distance * 0.9m,
                    VehicleType                         = vehicles[i % vehicles.Length],
                    FuelType                            = fuels[i % fuels.Length],
                    EuroClass                           = euros[i % euros.Length]
                });
            }
        }

        return (sos, wms, wmrs);
    }

    // ── Incidents ─────────────────────────────────────────────────────────────
    private static List<Incident> BuildIncidents(
        List<(Guid soId, string wmRef, Guid? soGuid)> wasteMoves, Guid ownerId)
    {
        var incidents = new List<Incident>();
        var t = Now;

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
        List<Guid> residueIds, Guid ownerId)
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
                CACEntryDate     = date ?? new DateTime(2026, 1, 1).AddDays(i % 150),
                TypeContainer    = containers[i % containers.Length],
                CollectionMethod = methods[i % methods.Length],
                DateCreateSys    = Now,
                DateModifiedSys  = Now,
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
        List<Guid> residueIds, Guid ownerId)
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
                PlantEntryDate   = date ?? new DateTime(2026, 1, 1).AddDays(i % 150),
                GrossWeight      = gross,
                TareWeight       = tare,
                NetWeight        = net,
                WeighbridgeId    = $"BASCULA-0{(i % 3) + 1}",
                TypeContainer    = "Contenedor",
                DateCreateSys    = Now,
                DateModifiedSys  = Now,
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
        List<Guid> incidentIds, Guid ownerId)
    {
        var plants   = new List<TreatmentPlant>();
        var residues = new List<TreatmentPlantResidue>();
        int incIdx   = 0;

        for (int i = 0; i < Math.Min(wasteMoves.Count, 100); i++)
        {
            var (wmId, wmRef, soId, date) = wasteMoves[i];
            var treatDate = (date ?? new DateTime(2026, 1, 1).AddDays(i % 150)).AddDays(1);

            // Vincular ~5 incidencias a plants de tipo WeightMismatch/FractionContamination
            Guid? incidentId = null;
            if (incIdx < incidentIds.Count && i < 5)
                incidentId = incidentIds[incIdx++];

            var tp = new TreatmentPlant
            {
                Id                   = SeedGuid("tp", i + 1),
                OwnerId              = ownerId,
                IdWasteMove          = wmId,
                WasteMoveReference   = wmRef,
                ServiceOrderId       = soId,
                TicketScale          = $"TICKET-TREAT-{i+1:D5}",
                PlantTreatmentDate   = treatDate,
                IdTreatmentOperation = treatOpIds.Count > 0 ? treatOpIds[i % treatOpIds.Count] : null,
                ImproperWeight       = (i * 3) % 50,
                QualityMetricsJson   = $"{{\"contaminationPct\":{(i % 10) * 0.5},\"moisture\":{10 + i % 15}}}",
                IncidentId           = incidentId,
                SourceSystem         = Seed,
                DateCreateSys        = Now,
                DateModifiedSys      = Now,
                IdUser               = SeedUser
            };
            plants.Add(tp);

            // Balance de masas: WeightReused + WeightValued + WeightRemove + ImproperWeight ≈ WeightTotal
            var improper   = tp.ImproperWeight ?? 0;
            var total      = 1000m;
            var reused     = Math.Round(total * 0.15m, 2);
            var valued     = Math.Round(total * 0.70m, 2);
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
        List<Guid> lerIds, Guid ownerId)
    {
        var settlements = new List<Settlement>();
        var lines       = new List<SettlementLine>();
        var statuses    = new[] { "Approved", "Approved", "Approved", "Pending", "Rejected" };
        var t = Now;

        for (int i = 0; i < Math.Min(agreements.Count, 25); i++)
        {
            var (agrId, scrapId, peId) = agreements[i];
            var baseAmount  = 5000m + (i * 1873) % 45000;
            var adjustments = -500m + (i * 43) % 1000;
            var tax         = Math.Round(baseAmount * 0.21m, 2);
            var total       = baseAmount + adjustments + tax;
            var s = new Settlement
            {
                Id               = SeedGuid("set", i + 1),
                OwnerId          = ownerId,
                SettlementNumber = $"LIQ-2026-{i+1:D4}",
                Status           = statuses[i % statuses.Length],
                AgreementId      = agrId,
                Year             = 2026,
                Month            = 1 + (i % 5),
                IdScrap          = scrapId,
                IdPublicEntity   = peId,
                Currency         = "EUR",
                BaseAmount       = baseAmount,
                AdjustmentsAmount = adjustments,
                TaxAmount        = tax,
                TotalAmount      = total,
                SourceSystem     = Seed,
                Version          = 1,
                CreatedAt        = t,
                UpdatedAt        = t,
                IdUser           = SeedUser
            };
            settlements.Add(s);

            for (int l = 0; l < 3 + (i % 3); l++)
            {
                var weightKg   = 500m + (i + l * 1000) % 9500;
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
    private static List<MarketShare> BuildMarketShares(List<Guid> scraps, Guid ownerId)
    {
        var list       = new List<MarketShare>();
        var categories = new[] { "Envases", "RAEE", "Voluminosos" };
        var ccaas      = new[] { "Aragón", "Madrid", "Cataluña" };
        var flowTypes  = new[] { "Recogida", "Reciclaje", "Valorización" };
        int idx        = 0;

        for (int s = 0; s < scraps.Count; s++)
        {
            foreach (var cat in categories)
            {
                foreach (var ccaa in ccaas)
                {
                    for (int period = 1; period <= 2; period++)
                    {
                        idx++;
                        list.Add(new MarketShare
                        {
                            Id                  = SeedGuid("ms", idx),
                            OwnerId             = ownerId,
                            IdScrap             = scraps[s],
                            Category            = cat,
                            AutonomousCommunity = ccaa,
                            Year                = 2026,
                            Weight              = 10000m + (idx * 9973) % 490000,
                            Period              = period,
                            EffectiveFrom       = period == 1
                                ? new DateOnly(2026, 1, 1) : new DateOnly(2026, 4, 1),
                            EffectiveTo         = period == 1
                                ? new DateOnly(2026, 3, 31) : new DateOnly(2026, 6, 30),
                            FlowType            = flowTypes[idx % flowTypes.Length],
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
        List<(Guid producerId, string centerCode)> producers, List<Guid> productResidueIds, Guid ownerId)
    {
        var declarations = new List<ProductDeclaration>();
        var products     = new List<Product>();
        var states       = new[] { "Borrador", "Emitido", "Validado", "Rechazado" };
        var types        = new[] { "DeclaraciónAnual", "DeclaraciónTrimestral" };

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
                    Year        = 2026,
                    Month       = 1 + (declIdx % 5),
                    Currency    = "EUR",
                    State       = state,
                    Type        = types[declIdx % types.Length],
                    Reference   = $"DECL-{centerCode}-2026-{d+1:D2}",
                    Amount      = 1000m + (declIdx * 3721) % 99000,
                    DateCreate  = Now.AddDays(-90 + declIdx),
                    DateEmit    = state != "Borrador" ? Now.AddDays(-80 + declIdx) : null,
                    DateCreateSys   = Now,
                    DateModifiedSys = Now,
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
                        Source                = "Producción propia",
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
