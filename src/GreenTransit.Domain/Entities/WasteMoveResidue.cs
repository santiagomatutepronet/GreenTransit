namespace GreenTransit.Domain.Entities;

/// <summary>Líneas de residuos por traslado. Tabla: WasteMoveResidues</summary>
public class WasteMoveResidue
{
    public Guid Id { get; set; }
    public Guid IdWasteMove { get; set; }
    public Guid? IdResidue { get; set; }
    public decimal? Weight { get; set; }
    public string? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public decimal? UnitPriceKg { get; set; }
    public DateTime? DateDelivery { get; set; }
    public string? NTNumber { get; set; }
    public string? DINumber { get; set; }
    public string? DIPhase { get; set; }
    public Guid? IdTreatmentOperationDestiny { get; set; }
    public Guid? IdCarrier { get; set; }
    public string? TransportInfo_VehicleRegistration { get; set; }
    public string? TransportInfo_VehicleRegistrationTrailer { get; set; }
    public decimal? TransportInfo_TransportDuration { get; set; }
    public decimal? TransportInfo_TransportDistance { get; set; }
    public decimal? TransportInfo_TransportCarbonEmissions { get; set; }
    public string? VehicleType { get; set; }
    public string? FuelType { get; set; }
    public string? EuroClass { get; set; }
    public Guid? EmissionFactorSetId { get; set; }
    public string? EmissionFactorVersion { get; set; }

    public WasteMove WasteMove { get; set; } = null!;
    public Residue? Residue { get; set; }
    public TreatmentOperation? TreatmentOperationDestiny { get; set; }
    public BusinessEntity? Carrier { get; set; }
    public EmissionFactorSet? EmissionFactorSet { get; set; }
}
