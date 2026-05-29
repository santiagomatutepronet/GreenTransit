namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class ServiceOrderItem
{
    public Guid      RemoteId                    { get; set; }
    public Guid      OwnerId                     { get; set; }
    public string?   ServiceOrderNumber          { get; set; }
    public DateTime? IssuedAt                    { get; set; }
    public string?   IssuedByName                { get; set; }
    public string?   IssuedByNationalId          { get; set; }
    public string?   IssuedByCenterCode          { get; set; }
    public string?   Status                      { get; set; }
    public string?   Priority                    { get; set; }
    public string?   WasteStream                 { get; set; }
    public string?   SubStream                   { get; set; }
    public int?      ProductUse                  { get; set; }
    public int?      ProductCategory             { get; set; }
    public string?   LerCode                     { get; set; }
    public string?   LerCodeExtended             { get; set; }
    public string?   PointName                   { get; set; }
    public string?   PointType                   { get; set; }
    public string?   PointAddress                { get; set; }
    public string?   MunicipalityCode            { get; set; }
    public string?   Latitude                    { get; set; }
    public string?   Longitude                   { get; set; }
    public DateTime? PlannedPickupStart          { get; set; }
    public DateTime? PlannedPickupEnd            { get; set; }
    public DateTime? PlannedDeliveryStart        { get; set; }
    public DateTime? PlannedDeliveryEnd          { get; set; }
    public decimal?  EstimatedWeight             { get; set; }
    public int?      MeasureUnit                 { get; set; }
    public int?      Units                       { get; set; }
    public string?   ContainersJson              { get; set; }
    public string?   AssignedCarrierName         { get; set; }
    public string?   AssignedCarrierNationalId   { get; set; }
    public string?   AssignedCarrierCenterCode   { get; set; }
    public string?   AssignedCarrierInscriptionType   { get; set; }
    public string?   AssignedCarrierInscriptionNumber { get; set; }
    public string?   PlannedPlantName            { get; set; }
    public string?   PlannedPlantCenterCode      { get; set; }
    public string?   WasteMoveReference          { get; set; }
    public string?   TicketScalePlanned          { get; set; }
    public DateTime? ActualPickupStart           { get; set; }
    public DateTime? ActualPickupEnd             { get; set; }
    public DateTime? ActualDeliveryStart         { get; set; }
    public DateTime? ActualDeliveryEnd           { get; set; }
    public decimal?  TransportDistanceKm         { get; set; }
    public int?      TransportDurationMin        { get; set; }
    public string?   VehicleRegistration         { get; set; }
    public string?   VehicleType                 { get; set; }
    public string?   FuelType                    { get; set; }
    public string?   EuroClass                   { get; set; }
    public string?   SourceSystem                { get; set; }
    public int       Version                     { get; set; }
    public string?   Hash                        { get; set; }
    public DateTime  CreatedAt                   { get; set; }
    public DateTime  UpdatedAt                   { get; set; }
}
