namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

/// <summary>DTO reutilizable para referencias a terceros (Entity en GreenTransit).</summary>
public class ThirdPartyRef
{
    public int?    TypeThirdParty      { get; set; }
    public string? Name                { get; set; }
    public string? NationalId          { get; set; }
    public string? CenterCode          { get; set; }
    public string? EntityType          { get; set; }
    public string? InscriptionType     { get; set; }
    public string? InscriptionNumber   { get; set; }
    public string? CountryCode         { get; set; }
    public string? StateCode           { get; set; }
    public string? ZipCode             { get; set; }
    public string? Latitude            { get; set; }
    public string? Longitude           { get; set; }
}
