using UnityEngine;
using PetShop.Commerce;
using PetShop.Pets;

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
                _game.Notify("Not enough money for that order.");
                _game.Audio?.PlaySfx("deny");
                return false;
            }

            _game.Notify($"Ordered {units} {category} units for €{order.Cost:N2} — the van is on its way.");
            _game.Audio?.PlaySfx("restock");
            return true;
        }

        /// <summary>Puts the pallet on the forecourt and tells the player it has landed.</summary>
        public void OnDeliveryArrived(SupplierOrder order)
        {
            Vector3 spot = _game.Generator != null ? _game.Generator.ForecourtPosition : Vector3.zero;
            // Spread pallets out so two deliveries never stack in the same spot.
            spot += new Vector3(UnityEngine.Random.Range(-2.4f, 2.4f), 0f, UnityEngine.Random.Range(-1f, 1.4f));

            DeliveryCrate.Spawn(spot, order.Category, order.Units);
            _game.Notify($"Delivery: {order.Units} {order.Category} units are on the forecourt. Press E to collect.");
            _game.Audio?.PlaySfx("restock");
        }

        /// <summary>Carries a delivered pallet into the stockroom.</summary>
        public void CollectDelivery(DeliveryCrate crate)
        {
            if (crate == null) return;

            int units = crate.Collect(_game.Shop);
            _game.Notify($"Collected {units} units — they are in the stockroom, ready to shelve.");
            _game.Audio?.PlaySfx("restock");
        }

        public void RestockShelf(ShelfUnit shelf)
        {
            if (shelf == null) return;

            var shop   = _game.Shop;
            var result = shelf.Restock(shop, _game.Catalog);

            if (result.Units <= 0)
                _game.Notify(shelf.HasSpace ? "Not enough money to restock." : "Shelf is already full.");
            else if (result.Spent <= 0.01f)
                _game.Notify($"Shelved {result.Units} {shelf.Category} units from the stockroom " +
                             $"({shop.Warehouse(shelf.Category)} left).");
            else if (result.FromWarehouse > 0)
                _game.Notify($"Shelved {result.FromWarehouse} from the stockroom and bought {result.Units - result.FromWarehouse} " +
                             $"at the cash-and-carry for €{result.Spent:N2}.");
            else
                _game.Notify($"Restocked {shelf.Category} for €{result.Spent:N2} at cash-and-carry prices " +
                             $"— ordering ahead is {(1f - ShopManager.WholesaleDiscount / ShopManager.EmergencyMarkup) * 100f:0}% cheaper.");

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
                    _game.Notify($"Fed and mucked out the {pen.PenSpecies} pen — €{cost:N2}");
                    _game.Audio?.PlaySfx("restock");
                }
                else
                {
                    _game.Notify($"Servicing that pen costs €{cost:N2} — not enough money.");
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
                    _game.Notify($"Bought {pet.DisplayName()} for €{price:N2}");
                    _game.Audio?.PlaySfx("restock");
                }
                else
                {
                    _game.Notify($"A {pen.PenSpecies} costs €{price:N2} — not enough money.");
                    _game.Audio?.PlaySfx("deny");
                }
            }
            else
            {
                _game.Audio?.PlaySfx("click");
            }

            _game.OnInfoPanel.Invoke(pen.Describe());
        }

        /// <summary>
        /// Interacting with the counter serves whoever is next in line; with nobody waiting
        /// it just opens the books.
        /// </summary>
        public void UseCounter()
        {
            var queue = _game.Queue;
            if (queue != null && queue.AnyWaiting)
            {
                var shopper = queue.Front;
                float value = shopper.BasketValue;
                int   items = shopper.BasketCount;

                queue.ServeFront();
                _game.Audio?.PlaySfx("sale");
                _game.Notify($"Served {shopper.ShopperName} — {items} item(s), €{value:N2}");

                if (queue.AnyWaiting)
                    _game.Notify($"{queue.Length} still waiting (€{queue.WaitingValue:N0}).");
                return;
            }

            OpenShopSummary();
        }

        public void OpenShopSummary()
        {
            var shop = _game.Shop;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Day {shop.Day}   ·   Balance €{shop.Balance:N2}   ·   Rep {shop.Reputation:0}/100");
            sb.AppendLine($"Rent due tonight: €{shop.DailyRent:N2}");
            sb.AppendLine($"Prices at {shop.PriceMultiplier * 100f:0}% — demand {shop.DemandFactor * 100f:0}%");
            sb.AppendLine($"Shelves: {_game.Shelves.Count}   Pens: {_game.Pens.Count}");
            sb.AppendLine();

            if (shop.TodaysSales.Count == 0)
            {
                sb.AppendLine("No sales yet today.");
            }
            else
            {
                sb.AppendLine("Today's sales:");
                float total = 0f;
                int shown = 0;
                foreach (var s in shop.TodaysSales)
                {
                    total += s.Revenue;
                    if (shown++ < 8) sb.AppendLine($"  {s.Label}  €{s.Revenue:0.00}");
                }
                if (shop.TodaysSales.Count > 8) sb.AppendLine($"  ... and {shop.TodaysSales.Count - 8} more");
                sb.AppendLine($"  Total: €{total:N2}");
            }
            _game.OnInfoPanel.Invoke(sb.ToString().TrimEnd());
            _game.Audio?.PlaySfx("click");
        }
    }
}
