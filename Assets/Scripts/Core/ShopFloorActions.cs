using UnityEngine;
using PetShop.Commerce;
using PetShop.Pets;
using PetShop.Shop;
using PetShop.Localization;
using static PetShop.Player.InteractionSystem;

namespace PetShop.Core
{
    /// <summary>
    /// The player's shop-floor interactions: supplier orders and deliveries, restocking
    /// shelves, pen purchases and upkeep, and the counter. Plain class owned by
    /// <see cref="GameManager"/>; Shop, Queue, Audio, Generator and Catalog are read through
    /// the manager at call time, because the bootstrapper wires those fields after Awake.
    /// </summary>
    internal sealed class ShopFloorActions
    {
        /// <summary>How far either side of the forecourt centre a crate may land, in metres.</summary>
        private const float SpreadX      = 2.4f;
        /// <summary>Nearest a crate lands behind the forecourt centre, in metres.</summary>
        private const float SpreadBackZ  = -1f;
        /// <summary>Furthest a crate lands in front of the forecourt centre, in metres.</summary>
        private const float SpreadFrontZ = 1.4f;

        private readonly GameManager _game;

        /// <summary>Creates the actions for <paramref name="game"/>; nothing is read yet.</summary>
        public ShopFloorActions(GameManager game)
        {
            _game = game;
        }

        /// <summary>Orders a pallet of stock for a category, to arrive later today.</summary>
        public bool OrderStock(ProductCategory category, int units)
        {
            var shop = _game.Shop;
            if (shop == null) return false;

            float unitCost = _game.Catalog != null ? _game.Catalog.AverageUnitCost(category) : 3.2f;
            var order = shop.PlaceOrder(category, units, unitCost, _game.DayProgress);
            if (order == null)
            {
                _game.Notify(Loc.T("order.no_money"));
                _game.Audio?.PlaySfx("deny");
                return false;
            }

            _game.Notify(Loc.F("order.placed", units, CategoryName(category), order.Cost));
            _game.Audio?.PlaySfx("restock");
            return true;
        }

        /// <summary>Puts the pallet on the forecourt and tells the player it has landed.</summary>
        public void OnDeliveryArrived(SupplierOrder order)
        {
            ByTruck(() =>
            {
                DeliveryCrate.Spawn(ForecourtSpot(), order.Category, order.Units);
                _game.Notify(Loc.F("delivery.stock", order.Units, CategoryName(order.Category),
                                   InputBindings.Label(GameAction.Interact)));
                _game.Audio?.PlaySfx("restock");
            });
        }

        /// <summary>
        /// Brings a load by delivery truck while the shop is trading: <paramref name="drop"/> runs when the
        /// truck pulls up outside. Overnight, or with no street to drive, the load is dropped straight away.
        /// </summary>
        private void ByTruck(System.Action drop)
        {
            if (_game.IsDayRunning && Traffic.DeliveryTruck.Dispatch(drop)) return;
            drop();
        }

        /// <summary>A landing spot on the forecourt for a delivery.</summary>
        private Vector3 ForecourtSpot()
        {
            Vector3 spot = _game.Layout != null ? _game.Layout.ForecourtPosition : Vector3.zero;
            // Spread pallets out so two deliveries never stack in the same spot.
            return spot + new Vector3(UnityEngine.Random.Range(-SpreadX, SpreadX), 0f,
                                      UnityEngine.Random.Range(SpreadBackZ, SpreadFrontZ));
        }

        /// <summary>Orders one catalogue item, paid now, to arrive later today as a crate.</summary>
        public bool OrderFurniture(string catalogId)
        {
            var def = BuildCatalog.Get(catalogId);
            if (def == null) return false;
            if (_game.Shop == null)
            {
                _game.Notify(Loc.F("order.furniture.no_shop", def.LocalizedName));
                _game.Audio?.PlaySfx("deny");
                return false;
            }

            if (_game.Furniture.Order(catalogId, _game.Shop, _game.DayProgress) == null)
            {
                _game.Notify(Loc.F("order.furniture.no_money", def.LocalizedName, def.Cost));
                _game.Audio?.PlaySfx("deny");
                return false;
            }

            _game.Notify(Loc.F("order.furniture.placed", def.LocalizedName, def.Cost));
            _game.Audio?.PlaySfx("click");
            return true;
        }

        /// <summary>Puts a furniture crate on the forecourt and tells the player it has landed.</summary>
        public void OnFurnitureArrived(FurnitureOrder order)
        {
            if (order == null) return;

            ByTruck(() =>
            {
                var crate = DeliveryCrate.SpawnFurniture(ForecourtSpot(), order);
                if (crate == null) return;
                _game.Notify(Loc.F("delivery.furniture", FurnitureLabel(crate),
                                   InputBindings.Label(GameAction.Interact)));
                _game.Audio?.PlaySfx("restock");
            });
        }

        /// <summary>Unpacks a furniture crate into the furniture inventory.</summary>
        private void CollectFurnitureCrate(DeliveryCrate crate)
        {
            string name = FurnitureLabel(crate);
            if (!crate.CollectFurniture(_game.Furniture))
            {
                _game.Notify(Loc.T("crate.empty"));
                _game.Audio?.PlaySfx("deny");
                return;
            }
            _game.Notify(Loc.F("crate.unpacked", name, InputBindings.Label(GameAction.BuildMode)));
            _game.Audio?.PlaySfx("restock");
        }

        /// <summary>The crate's furniture name in the current language.</summary>
        private static string FurnitureLabel(DeliveryCrate crate) =>
            crate.FurnitureOrder?.CatalogId is string id ? BuildCatalog.Get(id)?.LocalizedName ?? crate.FurnitureName
                                                         : crate.FurnitureName;

        /// <summary>Carries a delivered pallet into the stockroom, or unpacks a furniture crate.</summary>
        public void CollectDelivery(DeliveryCrate crate)
        {
            if (crate == null) return;
            if (crate.IsFurniture) { CollectFurnitureCrate(crate); return; }

            int units = crate.Collect(_game.Shop);
            _game.Notify(Loc.F("delivery.collected", units));
            _game.Audio?.PlaySfx("restock");
        }

        public void RestockShelf(ShelfUnit shelf)
        {
            if (shelf == null) return;

            var shop   = _game.Shop;
            var result = shelf.Restock(shop, _game.Catalog);

            if (result.Units <= 0)
                _game.Notify(Loc.T(shelf.HasSpace ? "restock.no_money" : "restock.full"));
            else if (result.Spent <= 0.01f)
                _game.Notify(Loc.F("restock.from_stockroom", result.Units, CategoryName(shelf.Category),
                                   shop.Warehouse(shelf.Category)));
            else if (result.FromWarehouse > 0)
                _game.Notify(Loc.F("restock.mixed", result.FromWarehouse, result.Units - result.FromWarehouse, result.Spent));
            else
                _game.Notify(Loc.F("restock.cash_and_carry", CategoryName(shelf.Category), result.Spent,
                                   (1f - ShopManager.WholesaleDiscount / ShopManager.EmergencyMarkup) * 100f));

            _game.Audio?.PlaySfx(result.Units > 0 ? "restock" : "deny");
            _game.OnInfoPanel.Invoke(shelf.Describe());
        }

        /// <summary>
        /// Interacting with a pen buys a young pet from the breeder when there is room —
        /// without this the pens could be sold out for good and the shop would stall.
        /// </summary>
        public void InspectPen(PetPen pen)
        {
            if (pen == null) return;

            var shop = _game.Shop;

            // Care comes first: an animal that needs feeding matters more than buying another.
            if (pen.NeedsService)
            {
                float cost = pen.ServiceCost;
                if (shop.ChangeBalance(-cost, "Pen upkeep"))
                {
                    pen.Service();
                    _game.Notify(Loc.F("pen.serviced", SpeciesName(pen.PenSpecies), cost));
                    _game.Audio?.PlaySfx("restock");
                }
                else
                {
                    _game.Notify(Loc.F("pen.service_no_money", cost));
                    _game.Audio?.PlaySfx("deny");
                }
                _game.OnInfoPanel.Invoke(pen.Describe());
                return;
            }

            if (pen.HasSpace)
            {
                float price = Pet.WholesalePrice(pen.PenSpecies);
                if (shop.ChangeBalance(-price, $"Buy {pen.PenSpecies}"))
                {
                    var pet = BreedingSystem.GenerateRandom(pen.PenSpecies);
                    pet.growthStage = Pet.GrowthStage.Juvenile;
                    pet.ageDays     = 1;
                    pen.AddPet(pet);
                    _game.Notify(Loc.F("pen.bought", pet.DisplayName(), price));
                    _game.Audio?.PlaySfx("restock");
                }
                else
                {
                    _game.Notify(Loc.F("pen.buy_no_money", SpeciesName(pen.PenSpecies), price));
                    _game.Audio?.PlaySfx("deny");
                }
            }
            else
            {
                _game.Audio?.PlaySfx("click");
            }

            _game.OnInfoPanel.Invoke(pen.Describe());
        }

        /// <summary>Shown when the counter is used with nobody in line, so E never seems dead.</summary>
        public static string NobodyWaitingNotice => Loc.T("counter.nobody");

        /// <summary>
        /// Interacting with the counter serves whoever is next in line; with nobody waiting
        /// it says so.
        /// </summary>
        public void UseCounter()
        {
            var queue = _game.Queue;
            if (queue == null || !queue.AnyWaiting)
            {
                _game.Notify(NobodyWaitingNotice);
                return;
            }
            if (!queue.FrontReady)
            {
                _game.Notify(Loc.T("counter.not_ready"));
                return;
            }

            var shopper = queue.NextReady;
            float value = shopper.BasketValue;
            int   items = shopper.BasketCount;

            queue.ServeFront();
            _game.Audio?.PlaySfx("sale");
            _game.Notify(Loc.Plural("counter.served", items, shopper.ShopperName, value));

            if (queue.AnyWaiting)
                _game.Notify(Loc.F("counter.still_waiting", queue.Length, queue.WaitingValue));
        }
    }
}
