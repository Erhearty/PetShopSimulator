using PetShop.Shop;

namespace PetShop.Core
{
    /// <summary>
    /// The furniture supply chain as seen from the facade: the inventory and orders live in a
    /// <see cref="FurnitureSupply"/>; ordering and crate handling are delegated to
    /// <see cref="ShopFloorActions"/>.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The furniture inventory and the catalogue orders on their way or on the forecourt.</summary>
        public FurnitureSupply Furniture { get; } = new FurnitureSupply();

        /// <summary>
        /// Orders one <paramref name="catalogId"/>, paid now at catalogue cost, to arrive later today
        /// as a crate on the forecourt. False (with a notification) when unknown or unaffordable.
        /// </summary>
        public bool OrderFurniture(string catalogId) => _floor.OrderFurniture(catalogId);
    }
}
