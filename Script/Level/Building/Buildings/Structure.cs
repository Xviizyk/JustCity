using Godot;

namespace JustCity.Script.Level.Building.Buildings;

[GlobalClass]
public partial class Structure(string buildingName, int buildCost, byte rank, byte currentLevel, byte maxLevel, Structure.BuildingTypes buildingType = Structure.BuildingTypes.None, bool isFactory = false, int monthCost = 0, Structure.ProductionTypes productionType = Structure.ProductionTypes.None, int productCapacity = 0, int productPerBatch = 0, float productTransportationInterval = 0, bool isService = false, float workRadius = 0, int servicesNeeded = 0, bool isLiving = false, int humanCapacity = 0) : Resource
{
    [ExportGroup("Metadata")]
    [Export] public string BuildingName { get; set; } = buildingName;
    [Export] public Vector2 Position { get; set; }

    [ExportGroup("Economy")]
    [Export] public int BuildCost { get; set; } = buildCost;
    [Export] public int MonthCost { get; set; } = monthCost;

    [ExportGroup("Ranking")]
    [Export] public byte Rank { get; set; } = rank;
    [Export] public byte CurrentLevel { get; set; } = currentLevel;
    [Export] public byte MaxLevel { get; set; } = maxLevel;
    [Export] public BuildingTypes BuildingType { get; set; } = buildingType;

    [ExportGroup("Factory")]
    [Export] public bool IsFactory { get; set; } = isFactory;
    [Export] public ProductionTypes ProductionType { get; set; } = productionType;
    [Export] public int ProductCapacity { get; set; } = productCapacity;
    [Export] public int ProductPerBatch { get; set; } = productPerBatch;
    [Export] public float ProductTransportationInterval { get; set; } = productTransportationInterval;

    [ExportGroup("Service")]
    [Export] public bool IsService { get; set; } = isService;
    [Export] public float WorkRadius { get; set; } = workRadius;
    [Export] public int ServicesNeeded { get; set; } = servicesNeeded;

    [ExportGroup("Living")]
    [Export] public bool IsLiving { get; set; } = isLiving;
    [Export] public int HumanCapacity { get; set; } = humanCapacity;

    public enum BuildingTypes
    {
        None = 0,
        Housing,
        Production,
        Commerce,
        Energy,
        Water,
        Safety,
        Administration,
        Misc
    }

    public enum ProductionTypes
    {
        None = 0,
        Food,
        Things,
        Chemistry,
        Military
    }
}