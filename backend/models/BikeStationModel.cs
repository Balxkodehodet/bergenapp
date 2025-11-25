namespace BergenCollectionApi.models;

public class BikeStationQuery
{
    public int? MinBikes { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public double? RadiusKm { get; set; }
}