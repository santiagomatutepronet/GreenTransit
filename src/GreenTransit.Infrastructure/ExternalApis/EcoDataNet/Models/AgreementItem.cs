namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class AgreementItem
{
    public Guid      RemoteId                  { get; set; }
    public Guid      OwnerId                   { get; set; }
    public string?   AgreementNumber           { get; set; }
    public string?   Status                    { get; set; }
    public DateTime? EffectiveFrom             { get; set; }
    public DateTime? EffectiveTo               { get; set; }
    public string?   ScrapName                 { get; set; }
    public string?   ScrapNationalId           { get; set; }
    public string?   ScrapCenterCode           { get; set; }
    public string?   PublicEntityName          { get; set; }
    public string?   PublicEntityNationalId    { get; set; }
    public string?   PublicEntityCenterCode    { get; set; }
    public string?   CoordinatorName           { get; set; }
    public string?   CoordinatorNationalId     { get; set; }
    public string?   CoordinatorCenterCode     { get; set; }
    public string?   WasteStream               { get; set; }
    public string?   SubStream                 { get; set; }
    public string?   AutonomousCommunity       { get; set; }
    public string?   ProvinceCode              { get; set; }
    public string?   MunicipalityCode          { get; set; }
    public string?   CoveredMethodsJson        { get; set; }
    public string?   TariffModelType           { get; set; }
    public string?   Currency                  { get; set; }
    public string?   TariffRulesJson           { get; set; }
    public string?   MinimumsJson              { get; set; }
    public string?   ObligationsJson           { get; set; }
    public string?   SourceSystem              { get; set; }
    public int       Version                   { get; set; }
    public string?   Hash                      { get; set; }
    public DateTime  CreatedAt                 { get; set; }
    public DateTime  UpdatedAt                 { get; set; }

    public List<AgreementDocumentItem> Documents { get; set; } = [];
}
