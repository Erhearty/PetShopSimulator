using PetShop.Core;

namespace PetShop.Shop
{
    /// <summary>
    /// Model tables for the street and the city behind it, best pack first: the Asset Store
    /// street/city packs when present, the CC0 Kenney kits otherwise.
    /// </summary>
    internal static class StreetModels
    {
        // ── Model choices, best pack first ──────────────────────────────────────

        internal static string Road      => ModelLibrary.FirstAvailable("Packs/Street/Roads/Streets/Road_Streight",
                                                                        ModelLibrary.Roads + "road-straight");
        internal static string Crossing  => ModelLibrary.FirstAvailable("Packs/Street/Roads/Streets/Road_Crosswalk",
                                                                        ModelLibrary.Roads + "road-crossing");
        // "TraficLights/LampPost_*" really are traffic lights, whatever the file is called —
        // one every 19 m along a high street looked absurd. ParkLamp is the actual lamp post.
        internal static string LampPost => ModelLibrary.FirstAvailable("Packs/Street/StreetProps/ParkLamp/ParkLamp",
                                                                      ModelLibrary.Roads + "light-square");

        internal static string TrafficLight => ModelLibrary.FirstAvailable(
            "Packs/Street/StreetProps/TraficLights/LampPost_A", ModelLibrary.Roads + "traffic-light");

        internal static readonly string[] ParadeShops =
        {
            "Packs/City/Buildings/Building_Bakery",     "Packs/City/Buildings/Building_Books Shop",
            "Packs/City/Buildings/Building_Coffee Shop","Packs/City/Buildings/Building_Gift Shop",
            "Packs/City/Buildings/Building_Clothing",   "Packs/City/Buildings/Building_Music Store",
            "Packs/City/Buildings/Building_Drug Store", "Packs/City/Buildings/Building_Bar",
        };

        internal static readonly string[] OppositeShops =
        {
            "Packs/City/Buildings/Building_Pizza",       "Packs/City/Buildings/Building_Fast Food",
            "Packs/City/Buildings/Building_Fruits  Shop","Packs/City/Buildings/Building_Chicken Shop",
            "Packs/City/Buildings/Building_Residential_color01",
            "Packs/City/Buildings/Building_Residential_color02",
            "Packs/City/Buildings/Building_House_01_color01",
            "Packs/City/Buildings/Building_House_02_color02",
        };

        internal static readonly string[] CityFill =
        {
            "Packs/City/Buildings/Building_Residential_color01",
            "Packs/City/Buildings/Building_Residential_color02",
            "Packs/City/Buildings/Building_Residential_color03",
            "Packs/City/Buildings/Building_House_01_color01",
            "Packs/City/Buildings/Building_House_02_color02",
            "Packs/City/Buildings/Building_House_03_color03",
            "Packs/City/Buildings/Building_Clothing",
            "Packs/City/Buildings/Building_Factory",
            ModelLibrary.Commercial + "building-l", ModelLibrary.Commercial + "building-m",
            ModelLibrary.Commercial + "building-n", ModelLibrary.Commercial + "building-skyscraper-a",
            ModelLibrary.Commercial + "building-skyscraper-c",
        };

        internal static readonly string[] Towers =
        {
            "Packs/City/Buildings/Building_Residential_color03",
            ModelLibrary.Commercial + "building-skyscraper-a", ModelLibrary.Commercial + "building-skyscraper-b",
            ModelLibrary.Commercial + "building-skyscraper-c", ModelLibrary.Commercial + "building-skyscraper-d",
        };

        internal static readonly string[] Cars =
        {
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color01",
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color02",
            "Packs/City/Vehicles/Vehicle with Static Wheels/Vehicle_Car_color03",
            ModelLibrary.Cars + "sedan", ModelLibrary.Cars + "suv", ModelLibrary.Cars + "van",
            ModelLibrary.Cars + "taxi", ModelLibrary.Cars + "delivery",
        };

        internal static readonly string[] Trees =
        {
            "Packs/Street/Foliage/Trees/Tree_A_V01", "Packs/Street/Foliage/Trees/Tree_A_V02_Leaves03",
            "Packs/Street/Foliage/Trees/Tree_B_V01_Leaves02",
            "Packs/Nature/Tree_01", "Packs/Nature/Tree_02", "Packs/Nature/Tree_03",
            ModelLibrary.Nature + "tree_default", ModelLibrary.Nature + "tree_oak",
        };
    }
}
