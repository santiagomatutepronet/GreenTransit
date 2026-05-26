using FluentAssertions;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.Commands;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Application.Features.EcoDataNet.Queries;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Persistence;
using GreenTransit.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GreenTransit.Tests.Application.EcoDataNet;

/// <summary>
/// Tests del flujo completo EDC: negociación → polling → transferencia → EDR → descarga.
/// El cliente EDC está mockeado; se verifica que cada handler orquesta correctamente el flujo.
/// </summary>
public sealed class EcoDataNetNegotiationFlowTests
{
    // ── Constantes del escenario de prueba ─────────────────────────────────

    private const int    ConsumerUserId        = 1;
    private const string ConsumerEDCServer     = "ecoucscrapa.ecodatanetconn3.dataspace.wastenode.com";
    private const string ConsumerApiKey        = "test-api-key-consumer";
    private const string ProviderParticipantId = "eco_uc_ofiasignacion";
    private const string ProviderProtocol      = "https://proto.ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com/protocol";
    private const string AssetId               = "ServiceOrders";
    private const string OfferId               = "Q29udHJhY3RfVUMxX09GSUFTSUdOQUNJT05fQ29tcGxpYW5jZWQ=:U2VydmljZU9yZGVycw==:abc123";
    private const string NegotiationId         = "neg-1234-abcd";
    private const string ContractAgreementId   = "agr-5678-efgh";
    private const string TransferId            = "xfer-9012-ijkl";
    private const string DataPlaneEndpoint     = "https://data.ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com/api/public";
    private const string AuthCode              = "eyJhbGciOiJSUzI1NiJ9.test-edr-token";
    private const string DownloadedData        = """{"records":[{"id":1,"asset":"ServiceOrders"}]}""";

    // ── Oferta del catálogo con permisos reales (ServiceOrders) ────────────
    // RawOfferJson simula el nodo odrl:hasPolicy[0] tal como lo devuelve el
    // catálogo del provider EDC (formato preservado por GetRawText()).

    private const string SampleRawOfferJson = """
    {
        "@id": "Q29udHJhY3RfVUMxX09GSUFTSUdOQUNJT05fQ29tcGxpYW5jZWQ=:U2VydmljZU9yZGVycw==:abc123",
        "@type": "odrl:Offer",
        "odrl:permission": [
            {
                "odrl:action": { "@id": "odrl:use" },
                "odrl:constraint": [
                    {
                        "odrl:leftOperand": { "@id": "edc:participant" },
                        "odrl:operator": { "@id": "odrl:isAnyOf" },
                        "odrl:rightOperand": ["eco_uc_scrapa", "eco_uc_scrapb"]
                    }
                ]
            }
        ],
        "odrl:prohibition": [],
        "odrl:obligation": []
    }
    """;

    private static readonly EdcOfferDto SampleOffer = new()
    {
        OfferId      = OfferId,
        RawOfferJson = SampleRawOfferJson,
        Permissions =
        [
            new EdcPermissionDto
            {
                Action = "odrl:use",
                Constraints =
                [
                    new EdcConstraintDto
                    {
                        LeftOperand  = "edc:participant",
                        Operator     = "odrl:isAnyOf",
                        // Formato Java toString real devuelto por el catálogo EDC
                        RightOperand = "[{@value={valueType=STRING, chars=eco_uc_scrapa, string=eco_uc_scrapa}}, {@value={valueType=STRING, chars=eco_uc_scrapb, string=eco_uc_scrapb}}]"
                    }
                ]
            },
            new EdcPermissionDto
            {
                Action = "odrl:use",
                Constraints =
                [
                    new EdcConstraintDto
                    {
                        LeftOperand  = "edc:purpose",
                        Operator     = "odrl:eq",
                        RightOperand = "commercial"
                    }
                ]
            }
        ],
        Prohibitions =
        [
            new EdcProhibitionDto { Action = "odrl:distribute", Constraints = [] }
        ],
        Obligations = []
    };

    // ── Setup: DB con usuario + conector y mock del cliente EDC ────────────

    private static (
        AppDbContext db,
        FakeCurrentUserService user,
        Mock<IEdcManagementClient> edcMock)
    CreateSetup()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var db   = TestDbContextFactory.Create(user);

        // Seed del usuario consumidor con su conector EDC
        var appUser = new AppUser
        {
            Id        = ConsumerUserId,
            Login     = "consumer@test.dev",
            OwnerId   = FakeCurrentUserService.TenantA,
            IdProfile = 1,
            Profile   = new UserProfile { Id = 1, Reference = "OPERATOR", Description = "Operador" }
        };
        var connector = new UserEDCConnector
        {
            Id             = 1,
            UserId         = ConsumerUserId,
            EDCServerName  = ConsumerEDCServer,
            EDCConnectorId = ProviderParticipantId,
            ApiKey         = ConsumerApiKey,
            User           = appUser
        };
        appUser.EDCConnector = connector;

        db.AppUsers.Add(appUser);
        db.UserEDCConnectors.Add(connector);
        db.SaveChanges();

        var edcMock = new Mock<IEdcManagementClient>(MockBehavior.Strict);
        return (db, user, edcMock);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEST 1 — BuildContractRequestPayload genera el formato correcto
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task StartNegotiation_PayloadHasCorrectFormat()
    {
        // Arrange
        var (db, user, edcMock) = CreateSetup();

        string? capturedPayload = null;
        edcMock.Setup(c => c.StartNegotiationAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string?, CancellationToken>((_, payload, _, _) =>
                capturedPayload = payload)
            .ReturnsAsync(new EdcNegotiationResponse
            {
                Success       = true,
                NegotiationId = NegotiationId,
                HttpStatusCode = 200
            });

        var handler = new StartContractNegotiationCommandHandler(
            db, user, edcMock.Object,
            NullLogger<StartContractNegotiationCommandHandler>.Instance);

        var command = new StartContractNegotiationCommand
        {
            ConsumerUserId         = ConsumerUserId,
            DatasetId              = AssetId,
            OfferId                = OfferId,
            ProviderParticipantId  = ProviderParticipantId,
            ProviderProtocolEndpoint = ProviderProtocol,
            RawOfferJson           = SampleOffer.RawOfferJson,
            Offer                  = SampleOffer
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — negociación iniciada
        result.Success.Should().BeTrue();
        result.NegotiationId.Should().Be(NegotiationId);

        // Assert — formato del payload
        capturedPayload.Should().NotBeNullOrEmpty();
        capturedPayload.Should().Contain(@"""@vocab"":""https://w3id.org/edc/v0.0.1/ns/""");
        capturedPayload.Should().Contain(@"""@type"":""ContractRequest""");
        capturedPayload.Should().Contain(@"""counterPartyAddress"":""" + ProviderProtocol + @"""");
        capturedPayload.Should().Contain(@"""protocol"":""dataspace-protocol-http""");

        // La policy se construye reenviando las reglas ODRL parseadas del catálogo
        // (permission/prohibition/obligation), normalizando el rightOperand Java
        // toString a un array JSON limpio. Esto evita el TERMINATED que produce
        // el provider cuando recibe arrays vacíos en lugar de las reglas reales.
        capturedPayload.Should().Contain(@"""@context"":""http://www.w3.org/ns/odrl.jsonld""");
        capturedPayload.Should().Contain(@"""@id"":""" + OfferId + @"""");
        capturedPayload.Should().Contain(@"""@type"":""Offer""");
        capturedPayload.Should().Contain(@"""assigner"":""" + ProviderParticipantId + @"""");
        capturedPayload.Should().Contain(@"""target"":""" + AssetId + @"""");

        // Permission con constraint poblada
        capturedPayload.Should().Contain("odrl:permission");
        capturedPayload.Should().Contain("odrl:constraint");
        capturedPayload.Should().Contain(@"""@id"":""edc:participant""");
        capturedPayload.Should().Contain(@"""@id"":""odrl:isAnyOf""");
        capturedPayload.Should().Contain("eco_uc_scrapa");
        capturedPayload.Should().Contain("eco_uc_scrapb");

        // Prohibition poblada (caso real del log)
        capturedPayload.Should().Contain(@"""@id"":""odrl:distribute""");

        // El rightOperand Java toString debe estar saneado: no debe filtrarse
        // el formato sin parsear al payload final.
        capturedPayload.Should().NotContain("valueType=STRING");
        capturedPayload.Should().NotContain("@value=");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEST 2 — Flujo completo: negociación → polling → transferencia → EDR → descarga
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FullEdcFlow_NegotiationToDownload_Succeeds()
    {
        // Arrange
        var (db, user, edcMock) = CreateSetup();
        var expectedMgmtUrl = $"https://mgmt.{ConsumerEDCServer}/management";

        // Mock: iniciar negociación → 200 OK
        edcMock.Setup(c => c.StartNegotiationAsync(
                expectedMgmtUrl, It.IsAny<string>(),
                ConsumerApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcNegotiationResponse
            {
                Success       = true,
                NegotiationId = NegotiationId,
                HttpStatusCode = 200
            });

        // Mock: polling estado negociación → FINALIZED con acuerdo
        edcMock.Setup(c => c.GetNegotiationStateAsync(
                expectedMgmtUrl, NegotiationId,
                ConsumerApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcNegotiationStateResponse
            {
                Success             = true,
                NegotiationId       = NegotiationId,
                State               = "FINALIZED",
                ContractAgreementId = ContractAgreementId,
                HttpStatusCode      = 200
            });

        // Mock: iniciar transferencia → 200 OK
        edcMock.Setup(c => c.StartTransferAsync(
                expectedMgmtUrl, It.IsAny<string>(),
                ConsumerApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcTransferResponse
            {
                Success          = true,
                TransferProcessId = TransferId,
                HttpStatusCode   = 200
            });

        // Mock: polling estado transferencia → STARTED
        edcMock.Setup(c => c.GetTransferStateAsync(
                expectedMgmtUrl, TransferId,
                ConsumerApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcTransferStateResponse
            {
                Success          = true,
                TransferProcessId = TransferId,
                State            = "STARTED",
                HttpStatusCode   = 200
            });

        // Mock: obtener EDR
        edcMock.Setup(c => c.GetEndpointDataReferenceAsync(
                expectedMgmtUrl, TransferId,
                ConsumerApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcEndpointDataReferenceResponse
            {
                Success        = true,
                Endpoint       = DataPlaneEndpoint,
                AuthType       = "Bearer",
                AuthCode       = AuthCode,
                HttpStatusCode = 200
            });

        // Mock: descarga de datos
        edcMock.Setup(c => c.DownloadDataAsync(
                DataPlaneEndpoint, "Bearer", AuthCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcDataDownloadResponse
            {
                Success     = true,
                ContentType = "application/json",
                Data        = DownloadedData,
                HttpStatusCode = 200
            });

        // ── PASO 1: Iniciar negociación ──────────────────────────────────────
        var negotiationHandler = new StartContractNegotiationCommandHandler(
            db, user, edcMock.Object,
            NullLogger<StartContractNegotiationCommandHandler>.Instance);

        var negotiationResult = await negotiationHandler.Handle(
            new StartContractNegotiationCommand
            {
                ConsumerUserId           = ConsumerUserId,
                DatasetId                = AssetId,
                OfferId                  = OfferId,
                ProviderParticipantId    = ProviderParticipantId,
                ProviderProtocolEndpoint = ProviderProtocol,
                RawOfferJson             = SampleOffer.RawOfferJson,
                Offer                    = SampleOffer
            }, CancellationToken.None);

        negotiationResult.Success.Should().BeTrue("la negociación debe iniciarse correctamente");
        negotiationResult.NegotiationId.Should().Be(NegotiationId);

        // ── PASO 2: Consultar estado de negociación → FINALIZED ─────────────
        var negStateHandler = new GetNegotiationStateQueryHandler(
            db, user, edcMock.Object,
            NullLogger<GetNegotiationStateQueryHandler>.Instance);

        var negState = await negStateHandler.Handle(
            new GetNegotiationStateQuery
            {
                ConsumerUserId = ConsumerUserId,
                NegotiationId  = NegotiationId
            }, CancellationToken.None);

        negState.Success.Should().BeTrue();
        negState.State.Should().Be("FINALIZED");
        negState.ContractAgreementId.Should().Be(ContractAgreementId);

        // ── PASO 3: Iniciar transferencia ────────────────────────────────────
        var transferHandler = new StartTransferProcessCommandHandler(
            db, user, edcMock.Object,
            NullLogger<StartTransferProcessCommandHandler>.Instance);

        var transferResult = await transferHandler.Handle(
            new StartTransferProcessCommand
            {
                ConsumerUserId           = ConsumerUserId,
                ContractAgreementId      = ContractAgreementId,
                AssetId                  = AssetId,
                ProviderProtocolEndpoint = ProviderProtocol,
                TransferType             = "HttpData-PULL"
            }, CancellationToken.None);

        transferResult.Success.Should().BeTrue("la transferencia debe iniciarse correctamente");
        transferResult.TransferProcessId.Should().Be(TransferId);

        // ── PASO 4: Consultar estado transferencia → STARTED ─────────────────
        var transferStateHandler = new GetTransferStateQueryHandler(
            db, user, edcMock.Object,
            NullLogger<GetTransferStateQueryHandler>.Instance);

        var transferState = await transferStateHandler.Handle(
            new GetTransferStateQuery
            {
                ConsumerUserId   = ConsumerUserId,
                TransferProcessId = TransferId
            }, CancellationToken.None);

        transferState.Success.Should().BeTrue();
        transferState.State.Should().Be("STARTED");

        // ── PASO 5: Obtener EDR ───────────────────────────────────────────────
        var edrHandler = new GetEndpointDataReferenceQueryHandler(
            db, user, edcMock.Object,
            NullLogger<GetEndpointDataReferenceQueryHandler>.Instance);

        var edrResult = await edrHandler.Handle(
            new GetEndpointDataReferenceQuery
            {
                ConsumerUserId   = ConsumerUserId,
                TransferProcessId = TransferId
            }, CancellationToken.None);

        edrResult.Success.Should().BeTrue();
        edrResult.Endpoint.Should().Be(DataPlaneEndpoint);
        edrResult.AuthType.Should().Be("Bearer");
        edrResult.AuthCode.Should().Be(AuthCode);

        // ── PASO 6: Descargar datos ───────────────────────────────────────────
        var downloadHandler = new DownloadTransferDataCommandHandler(
            edcMock.Object,
            NullLogger<DownloadTransferDataCommandHandler>.Instance);

        var downloadResult = await downloadHandler.Handle(
            new DownloadTransferDataCommand
            {
                DataPlaneEndpoint = edrResult.Endpoint!,
                AuthType          = edrResult.AuthType!,
                AuthCode          = edrResult.AuthCode!
            }, CancellationToken.None);

        downloadResult.Success.Should().BeTrue("los datos deben descargarse correctamente");
        downloadResult.Data.Should().Be(DownloadedData);
        downloadResult.ContentType.Should().Be("application/json");

        // Verificar que todos los mocks fueron invocados exactamente una vez
        edcMock.Verify(c => c.StartNegotiationAsync(
            expectedMgmtUrl, It.IsAny<string>(), ConsumerApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
        edcMock.Verify(c => c.GetNegotiationStateAsync(
            expectedMgmtUrl, NegotiationId, ConsumerApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
        edcMock.Verify(c => c.StartTransferAsync(
            expectedMgmtUrl, It.IsAny<string>(), ConsumerApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
        edcMock.Verify(c => c.GetTransferStateAsync(
            expectedMgmtUrl, TransferId, ConsumerApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
        edcMock.Verify(c => c.GetEndpointDataReferenceAsync(
            expectedMgmtUrl, TransferId, ConsumerApiKey, It.IsAny<CancellationToken>()),
            Times.Once);
        edcMock.Verify(c => c.DownloadDataAsync(
            DataPlaneEndpoint, "Bearer", AuthCode, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEST 3 — TERMINATED en negociación → Success=false con estado TERMINATED
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PollNegotiation_WhenTerminated_ReturnsTerminatedState()
    {
        // Arrange
        var (db, user, edcMock) = CreateSetup();

        edcMock.Setup(c => c.GetNegotiationStateAsync(
                It.IsAny<string>(), NegotiationId,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcNegotiationStateResponse
            {
                Success       = true,
                NegotiationId = NegotiationId,
                State         = "TERMINATED",
                ErrorDetail   = "Policy evaluation failed: constraint not satisfied",
                HttpStatusCode = 200
            });

        var handler = new GetNegotiationStateQueryHandler(
            db, user, edcMock.Object,
            NullLogger<GetNegotiationStateQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GetNegotiationStateQuery
            {
                ConsumerUserId = ConsumerUserId,
                NegotiationId  = NegotiationId
            }, CancellationToken.None);

        // Assert
        result.State.Should().Be("TERMINATED");
        result.ErrorDetail.Should().NotBeNullOrWhiteSpace();
        result.ErrorDetail.Should().Contain("constraint not satisfied");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEST 4 — Error HTTP 400 en negociación → Success=false con mensaje
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task StartNegotiation_WhenHttp400_ReturnsFalseWithMessage()
    {
        // Arrange
        var (db, user, edcMock) = CreateSetup();

        edcMock.Setup(c => c.StartNegotiationAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EdcNegotiationResponse
            {
                Success        = false,
                HttpStatusCode = 400,
                ErrorMessage   = """HTTP 400: {"message":"Bad Request","status":"400"}"""
            });

        var handler = new StartContractNegotiationCommandHandler(
            db, user, edcMock.Object,
            NullLogger<StartContractNegotiationCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new StartContractNegotiationCommand
            {
                ConsumerUserId           = ConsumerUserId,
                DatasetId                = AssetId,
                OfferId                  = OfferId,
                ProviderParticipantId    = ProviderParticipantId,
                ProviderProtocolEndpoint = ProviderProtocol,
                Offer                    = SampleOffer
            }, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.HttpStatusCode.Should().Be(400);
        result.ErrorMessage.Should().Contain("400");
    }
}
