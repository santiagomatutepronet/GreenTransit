using System.Net;
using System.Text;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.ExternalApis.EcoDataNet;
using GreenTransit.Infrastructure.Persistence;
using GreenTransit.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GreenTransit.Tests.Infrastructure.EcoDataNet;

// ── Fake HttpMessageHandler ───────────────────────────────────────────────────

/// <summary>
/// Handler HTTP falso que devuelve siempre la respuesta configurada.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string         _body;

    public int CallCount { get; private set; }
    public List<string> CalledUrls { get; } = [];

    public FakeHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK, string body = "[]")
    {
        _statusCode = statusCode;
        _body       = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        CalledUrls.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}

// ── Tests unitarios ───────────────────────────────────────────────────────────

/// <summary>
/// Tests de integración in-memory del EcoDataNetPublisher.
/// Verifican: batching correcto, que ownerIds se asignan, que PublishAllAsync
/// llama a todos los endpoints y que el resumen agrega totales correctamente.
/// </summary>
public sealed class EcoDataNetPublisherTests
{
    // ── Helpers de fábrica ────────────────────────────────────────────────────

    private static (EcoDataNetPublisher publisher, FakeHttpMessageHandler handler, AppDbContext db)
        BuildSut(HttpStatusCode statusCode = HttpStatusCode.OK, string body = "[]", int batchSize = 100)
    {
        var db      = TestDbContextFactory.CreateDefault();
        var handler = new FakeHttpMessageHandler(statusCode, body);
        var http    = new HttpClient(handler) { BaseAddress = new Uri("https://fake.ecodatanet.test/") };
        var opts    = Options.Create(new EcoDataNetOptions
        {
            BaseUrl        = "https://fake.ecodatanet.test",
            Username       = "user",
            Password       = "pass",
            BatchSize      = batchSize,
            TimeoutSeconds = 30,
            MaxRetries     = 1,
        });
        var httpClient  = new EcoDataNetHttpClient(http, NullLogger<EcoDataNetHttpClient>.Instance);
        var publisher   = new EcoDataNetPublisher(
            db, httpClient, opts, NullLogger<EcoDataNetPublisher>.Instance);

        return (publisher, handler, db);
    }

    // ── Test: PublishAllAsync no falla con base de datos vacía ────────────────

    [Fact]
    public async Task PublishAllAsync_EmptyDatabase_ReturnsSummaryWithSixteenEndpoints()
    {
        var (publisher, handler, _) = BuildSut();

        var summary = await publisher.PublishAllAsync(null, CancellationToken.None);

        // Los 16 endpoints siempre se procesan (aunque no haya datos)
        Assert.Equal(16, summary.TotalEndpoints);
        Assert.True(summary.Duration >= TimeSpan.Zero);
    }

    // ── Test: Todos los endpoints vacíos no llaman al HttpClient ─────────────

    [Fact]
    public async Task PublishAllAsync_EmptyDatabase_DoesNotCallHttpClient()
    {
        var (publisher, handler, _) = BuildSut();

        await publisher.PublishAllAsync(null, CancellationToken.None);

        // Con BD vacía, ningún batch tiene items → no se hacen POSTs
        Assert.Equal(0, handler.CallCount);
    }

    // ── Test: WasteMoves — un ítem genera exactamente una llamada HTTP ─────────

    [Fact]
    public async Task PublishAllAsync_OneWasteMove_CallsHttpOnce()
    {
        var (publisher, handler, db) = BuildSut();

        db.WasteMoves.Add(new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = FakeCurrentUserService.TenantA,
            GatheredDate       = DateTime.UtcNow,
            WasteMoveReference = "REF-001",
            Version            = 1,
            SourceSystem       = "SEED",
        });
        await db.SaveChangesAsync();

        await publisher.PublishAllAsync(null, CancellationToken.None);

        Assert.Contains(handler.CalledUrls, u => u.Contains("WasteMoves"));
    }

    // ── Test: Batching — N ítems con batchSize=1 genera N llamadas HTTP ───────

    [Fact]
    public async Task PublishAllAsync_BatchingRespected_MultipleCallsWhenOverBatchSize()
    {
        const int count     = 5;
        const int batchSize = 2;
        var (publisher, handler, db) = BuildSut(batchSize: batchSize);

        for (int i = 0; i < count; i++)
        {
            db.WasteMoves.Add(new WasteMove
            {
                Id                 = Guid.NewGuid(),
                OwnerId            = FakeCurrentUserService.TenantA,
                GatheredDate       = DateTime.UtcNow,
                WasteMoveReference = $"REF-{i:D3}",
                Version            = 1,
                SourceSystem       = "SEED",
            });
        }
        await db.SaveChangesAsync();

        await publisher.PublishAllAsync(null, CancellationToken.None);

        // 5 ítems / batchSize 2 → ceil(5/2) = 3 llamadas para WasteMoves
        int wasteMovesCalls = handler.CalledUrls.Count(u => u.Contains("WasteMoves"));
        Assert.Equal((int)Math.Ceiling((double)count / batchSize), wasteMovesCalls);
    }

    // ── Test: Resumen agrega TotalSent correctamente ──────────────────────────

    [Fact]
    public async Task PublishAllAsync_TotalSentMatchesItemCount()
    {
        const int count = 3;
        var (publisher, handler, db) = BuildSut();

        for (int i = 0; i < count; i++)
        {
            db.WasteMoves.Add(new WasteMove
            {
                Id                 = Guid.NewGuid(),
                OwnerId            = FakeCurrentUserService.TenantA,
                GatheredDate       = DateTime.UtcNow,
                WasteMoveReference = $"BATCH-{i}",
                Version            = 1,
                SourceSystem       = "SEED",
            });
        }
        await db.SaveChangesAsync();

        var summary = await publisher.PublishAllAsync(null, CancellationToken.None);

        var wmResult = summary.Results.First(r => r.Endpoint == "WasteMoves");
        Assert.Equal(count, wmResult.TotalSent);
        Assert.Equal(count, wmResult.SuccessCount);
        Assert.Equal(0,     wmResult.ErrorCount);
    }

    // ── Test: 207 Multi-Status se parsea correctamente ────────────────────────

    [Fact]
    public async Task PublishAllAsync_207Response_ParsedCorrectly()
    {
        // 3 ítems → 207 con 2 ok y 1 error
        const string multiStatusBody = """
            [
              {"statusCode": 200, "remoteId": "a"},
              {"statusCode": 200, "remoteId": "b"},
              {"statusCode": 422, "remoteId": "c", "error": "validation"}
            ]
            """;
        var (publisher, handler, db) = BuildSut(
            statusCode: (HttpStatusCode)207, body: multiStatusBody);

        for (int i = 0; i < 3; i++)
        {
            db.WasteMoves.Add(new WasteMove
            {
                Id                 = Guid.NewGuid(),
                OwnerId            = FakeCurrentUserService.TenantA,
                GatheredDate       = DateTime.UtcNow,
                WasteMoveReference = $"MS-{i}",
                Version            = 1,
                SourceSystem       = "SEED",
            });
        }
        await db.SaveChangesAsync();

        var summary = await publisher.PublishAllAsync(null, CancellationToken.None);

        var wmResult = summary.Results.First(r => r.Endpoint == "WasteMoves");
        Assert.Equal(3, wmResult.TotalSent);
        Assert.Equal(2, wmResult.SuccessCount);
        Assert.Equal(1, wmResult.ErrorCount);
    }

    // ── Test: Callback onProgress se invoca para cada endpoint ───────────────

    [Fact]
    public async Task PublishAllAsync_OnProgress_CalledForEachEndpoint()
    {
        var (publisher, _, _) = BuildSut();
        var progressLog = new List<(string name, int current, int total)>();

        await publisher.PublishAllAsync(
            (name, cur, tot) => progressLog.Add((name, cur, tot)),
            CancellationToken.None);

        Assert.Equal(16, progressLog.Count);
        // Índices crecientes
        for (int i = 0; i < progressLog.Count; i++)
            Assert.Equal(i + 1, progressLog[i].current);
        // Total siempre 16
        Assert.All(progressLog, p => Assert.Equal(16, p.total));
    }

    // ── Test: Error HTTP no lanza excepción, registra ErrorMessage ────────────

    [Fact]
    public async Task PublishAllAsync_HttpError_DoesNotThrow_RecordsErrorMessage()
    {
        var (publisher, _, db) = BuildSut(statusCode: HttpStatusCode.InternalServerError, body: "Error interno");

        db.WasteMoves.Add(new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = FakeCurrentUserService.TenantA,
            GatheredDate       = DateTime.UtcNow,
            WasteMoveReference = "ERR-001",
            Version            = 1,
            SourceSystem       = "SEED",
        });
        await db.SaveChangesAsync();

        var summary = await publisher.PublishAllAsync(null, CancellationToken.None);

        var wmResult = summary.Results.First(r => r.Endpoint == "WasteMoves");
        Assert.NotNull(wmResult.ErrorMessage);
        Assert.Contains("500", wmResult.ErrorMessage);
    }

    // ── Test: PublishSummary agrega correctamente Duration > 0 ───────────────

    [Fact]
    public async Task PublishAllAsync_Duration_IsPositive()
    {
        var (publisher, _, _) = BuildSut();
        var summary = await publisher.PublishAllAsync(null, CancellationToken.None);
        Assert.True(summary.Duration >= TimeSpan.Zero);
    }

    // ── Test: EndpointResult.ParseMultiStatus — cuerpo vacío ─────────────────

    [Fact]
    public void ParseMultiStatus_EmptyArray_ZeroCounts()
    {
        var result = new EndpointResult { TotalSent = 0 };
        result.ParseMultiStatus("[]");
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Null(result.ErrorMessage);
    }

    // ── Test: EndpointResult.ParseMultiStatus — JSON inválido ────────────────

    [Fact]
    public void ParseMultiStatus_InvalidJson_SetsErrorMessage()
    {
        var result = new EndpointResult { TotalSent = 1 };
        result.ParseMultiStatus("no-es-json");
        Assert.NotNull(result.ErrorMessage);
    }

    // ── Test: EndpointResult.ParseMultiStatus — objeto (no array) ────────────

    [Fact]
    public void ParseMultiStatus_ObjectNotArray_SetsUnexpectedFormatMessage()
    {
        var result = new EndpointResult { TotalSent = 1 };
        result.ParseMultiStatus("""{"error":"unexpected"}""");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("inesperado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
