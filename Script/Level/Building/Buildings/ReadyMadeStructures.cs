namespace JustCity.Script.Level.Building.Buildings;

public partial class ReadyMadeStructures
{
    public Structure HousingFirstLevel = new(
        buildingName: "RESIDENTIAL_BUILDING",
        buildCost: 100,
        rank: 1,
        currentLevel: 1,
        maxLevel: 3,
        buildingType: Structure.BuildingTypes.Housing,
        isLiving: true,
        humanCapacity: 5
    );
}