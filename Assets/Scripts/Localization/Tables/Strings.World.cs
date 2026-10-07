using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Core notifications, interaction prompts, build mode and furniture.</summary>
    public static class World
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Day cycle and prices
            Add("day.open", "Day {0} — open. Tonight: rent €{1:N0}", "День {0} — відчинено. Увечері: оренда €{1:N0}");
            Add("day.open_wages", " + wages €{0:N0}", " + зарплати €{0:N0}");
            Add("day.closing", "Closing up for the night...", "Зачиняємося на ніч...");
            Add("prices.now", "Prices now {0:0}% of list — demand {1:0}%.", "Ціни зараз {0:0}% від прайсу — попит {1:0}%.");

            // Orders and deliveries
            Add("order.no_money", "Not enough money for that order.", "Не вистачає грошей на це замовлення.");
            Add("order.placed", "Ordered {0} {1} units for €{2:N2} — the van is on its way.", "Замовлено {0} од. ({1}) за €{2:N2} — фургон уже в дорозі.");
            Add("delivery.stock", "Delivery: {0} {1} units are on the forecourt. Press {2} to collect.", "Доставка: {0} од. ({1}) біля входу. Натисніть {2}, щоб забрати.");
            Add("order.furniture.no_shop", "Cannot order a {0} — there is no shop to pay for it.", "Не можна замовити «{0}» — немає крамниці, щоб заплатити.");
            Add("order.furniture.no_money", "Not enough money for a {0} (€{1:N0}).", "Не вистачає грошей на «{0}» (€{1:N0}).");
            Add("order.furniture.placed", "Ordered a {0} for €{1:N0} — it arrives on the forecourt later today.", "Замовлено «{0}» за €{1:N0} — привезуть до входу сьогодні пізніше.");
            Add("delivery.furniture", "Delivery: a {0} crate is on the forecourt. Press {1} to unpack it.", "Доставка: ящик «{0}» біля входу. Натисніть {1}, щоб розпакувати.");
            Add("crate.empty", "There is nothing left in that crate.", "У цьому ящику нічого не лишилося.");
            Add("crate.unpacked", "Unpacked the {0} — it is in your furniture inventory. Press {1} to place it.", "«{0}» розпаковано — це у вашому запасі меблів. Натисніть {1}, щоб поставити.");
            Add("delivery.collected", "Collected {0} units — they are in the stockroom, ready to shelve.", "Забрано {0} од. — вони на складі, готові до викладки.");

            // Restocking
            Add("restock.no_money", "Not enough money to restock.", "Не вистачає грошей на поповнення.");
            Add("restock.full", "Shelf is already full.", "Полиця вже повна.");
            Add("restock.from_stockroom", "Shelved {0} {1} units from the stockroom ({2} left).", "Викладено {0} од. ({1}) зі складу (лишилося {2}).");
            Add("restock.mixed", "Shelved {0} from the stockroom and bought {1} at the cash-and-carry for €{2:N2}.", "Викладено {0} зі складу й докуплено {1} на гуртівні за €{2:N2}.");
            Add("restock.cash_and_carry", "Restocked {0} for €{1:N2} at cash-and-carry prices — ordering ahead is {2:0}% cheaper.", "Поповнено «{0}» за €{1:N2} за цінами гуртівні — замовлення наперед на {2:0}% дешевше.");

            // Pens
            Add("pen.serviced", "Fed and mucked out the {0} pen — €{1:N2}", "Вольєр «{0}» нагодовано й прибрано — €{1:N2}");
            Add("pen.service_no_money", "Servicing that pen costs €{0:N2} — not enough money.", "Догляд за цим вольєром коштує €{0:N2} — не вистачає грошей.");
            Add("pen.bought", "Bought {0} for €{1:N2}", "Куплено {0} за €{1:N2}");
            Add("pen.buy_no_money", "A {0} costs €{1:N2} — not enough money.", "{0} коштує €{1:N2} — не вистачає грошей.");

            // Counter
            Add("counter.nobody", "Nobody is waiting at the till.", "Біля каси ніхто не чекає.");
            Add("counter.not_ready", "The next customer is still on their way to the till.", "Наступний покупець ще йде до каси.");
            Add("counter.served.one", "Served {1} — {0} item, €{2:N2}", "Обслуговано: {1} — {0} товар, €{2:N2}");
            Add("counter.served.other", "Served {1} — {0} items, €{2:N2}", "Обслуговано: {1} — {0} товарів, €{2:N2}");
            uk["counter.served.few"]  = "Обслуговано: {1} — {0} товари, €{2:N2}";
            uk["counter.served.many"] = "Обслуговано: {1} — {0} товарів, €{2:N2}";
            Add("counter.still_waiting", "{0} still waiting (€{1:N0}).", "Ще чекають: {0} (€{1:N0}).");

            // Staff: {0} is the person's name unless noted.
            Add("staff.hire.no_counter", "Place a counter first — a cashier needs a counter to work behind.", "Спершу поставте прилавок — касирові потрібен прилавок, за яким працювати.");
            Add("staff.hire.no_room", "There is no room behind that counter for another assistant.", "За цим прилавком немає місця для ще одного помічника.");
            // {0} name, {1} role, {2} skill word, {3} daily wage, {4} head count.
            Add("staff.hired", "Hired {0} — {1}, {2}, €{3:N0} a day, {4} on the payroll.", "Найнято: {0} — {1}, {2}, €{3:N0} на день, у штаті: {4}.");
            Add("staff.sign_on_no_money", "You cannot cover {0}'s €{1:N0} sign-on fee.", "Не вистачає грошей на вступну виплату €{1:N0} ({0}).");
            Add("staff.nobody_to_fire", "There is nobody to let go.", "Немає кого звільнити.");
            // {0} name, {1} head count left.
            Add("staff.fired", "Let {0} go — {1} left on the payroll.", "{0}: звільнено — у штаті лишилося: {1}.");

            // New game and saves. Welcome: {0} build key, {1} interact key, {2} end-day key.
            Add("game.welcome", "Welcome to your pet shop! {0} to build, {1} to interact, {2} to close up.", "Ласкаво просимо до вашої зоокрамниці! {0} — будувати, {1} — взаємодіяти, {2} — зачинити на ніч.");
            Add("save.saved", "Game saved.", "Гру збережено.");
            Add("save.autosaved", "Autosaved.", "Автозбереження виконано.");
            Add("save.failed", "Save FAILED — progress not written. Check disk space/permissions.", "Збереження НЕ ВДАЛОСЯ — прогрес не записано. Перевірте місце на диску та права доступу.");
            // {0} day, {1} balance.
            Add("save.loaded", "Save loaded — day {0}, €{1:N0}", "Збереження завантажено — день {0}, €{1:N0}");

            // Interaction prompts ({0} is always the interact key)
            Add("prompt.source.stockroom", "  ·  {0} in the stockroom", "  ·  на складі: {0}");
            Add("prompt.source.cash_and_carry", "  ·  stockroom empty, cash-and-carry prices", "  ·  склад порожній, ціни гуртівні");
            Add("prompt.restock_shelf", "[{0}]  Restock shelf{1}", "[{0}]  Поповнити полицю{1}");
            Add("prompt.restock_category", "[{0}]  Restock {1} shelf  ({2} units left){3}", "[{0}]  Поповнити полицю «{1}»  (лишилося {2} од.){3}");
            Add("prompt.pen_service", "[{0}]  Feed & clean the {1} pen  (€{2:N0})", "[{0}]  Нагодувати й прибрати вольєр «{1}»  (€{2:N0})");
            Add("prompt.pen_buy", "[{0}]  Buy a {1}  (€{2:N0})   ·  {3}/{4} stalls filled", "[{0}]  Купити: {1}  (€{2:N0})   ·  зайнято {3}/{4} місць");
            Add("prompt.pen_full", "[{0}]  {1} pen — full ({2}/{3})", "[{0}]  Вольєр «{1}» — повний ({2}/{3})");
            Add("prompt.serve", "[{0}]  Serve {1}  —  {2} item(s), €{3:N2}   ({4} waiting)", "[{0}]  Обслужити {1}  —  товарів: {2}, €{3:N2}   (чекають: {4})");
            Add("prompt.counter_empty", "[{0}]  Counter — no customers yet", "[{0}]  Прилавок — покупців поки немає");

            // Build mode
            Add("build.counter_no_room", "Leave room behind the counter for the cashier", "Залиште місце за прилавком для касира");
            Add("build.pen_in_shop", "Pens go in the yard", "Вольєри ставлять у дворі");
            Add("build.cant_build", "Can't build there.", "Тут будувати не можна.");
            Add("build.remove_tool", "Remove tool — click a piece to take it away. {0} to stop.", "Прибирання — клацніть предмет, щоб прибрати його. {0} — завершити.");
            Add("build.nothing_to_remove", "Nothing to remove there.", "Тут нічого прибирати.");
            Add("build.pen_has_pets", "Move the pets out before packing this pen away.", "Спершу переселіть тварин, а тоді складайте вольєр.");
            Add("build.aim_to_remove", "Aim at a piece or the floor to remove it.", "Наведіть на предмет або підлогу, щоб прибрати.");
            Add("build.cannot_remove", "That piece can't be removed right now.", "Цей предмет зараз не можна прибрати.");
            Add("build.no_money", "Not enough money — {0} costs €{1:N0}.", "Не вистачає грошей — «{0}» коштує €{1:N0}.");
            Add("build.pen_species", "Pen species: {0} — {1} to change", "Вид для вольєра: {0} — {1}, щоб змінити");
            Add("build.removed", "Removed.", "Прибрано.");
            Add("build.packed", "Packed {0} back into your furniture inventory.", "«{0}» повернуто до запасу меблів.");
            Add("build.sold", "Removed {0} (+€{1:N0})", "Прибрано «{0}» (+€{1:N0})");
            Add("build.rotate_wheel", " / wheel", " / коліщатко");
            Add("build.rotate", "{0:0}° — {1} / Shift+{1}{2} to rotate", "{0:0}° — {1} / Shift+{1}{2}, щоб повернути");

            // Furniture catalogue (keys by catalogue id; English mirrors BuildCatalog.DisplayName)
            Add("furniture.shelf_small.name", "Small Shelf", "Мала полиця");
            Add("furniture.shelf_small.desc", "Holds up to two product lines.", "Вміщує до двох видів товару.");
            Add("furniture.shelf_large.name", "Large Shelf", "Велика полиця");
            Add("furniture.shelf_large.desc", "A wide shelf with room for more stock per line.", "Широка полиця, більше товару на кожен вид.");
            Add("furniture.pet_pen.name", "Pet Pen", "Вольєр");
            Add("furniture.pet_pen.desc", "Four individual stalls, one per pet, all of one species.", "Чотири окремі місця, по одному на тварину, усі одного виду.");
            Add("furniture.species_pen.name", "{0} Pen", "Вольєр: {0}");
            Add("furniture.species_pen.desc", "Four stalls for this species. Ships with a breeding pair of adults ({0}).", "Чотири місця для цього виду. Постачається з дорослою парою для розведення ({0}).");
            Add("furniture.counter.name", "Counter", "Прилавок");
            Add("furniture.counter.desc", "Where customers queue and pay. Needed to open the shop.", "Тут покупці стоять у черзі й платять. Потрібен, щоб відчинити крамницю.");
            Add("furniture.wall.name", "Wall", "Стіна");
            Add("furniture.wall.desc", "One cell of solid wall.", "Одна клітинка суцільної стіни.");
            Add("furniture.wall_window.name", "Window Wall", "Стіна з вікном");
            Add("furniture.wall_window.desc", "One cell of wall with a window.", "Одна клітинка стіни з вікном.");
            Add("furniture.wall_door.name", "Doorway", "Дверний проріз");
            Add("furniture.wall_door.desc", "One cell of wall with an open doorway.", "Одна клітинка стіни з відкритим проходом.");
            Add("furniture.fence.name", "Fence", "Паркан");
            Add("furniture.fence.desc", "One cell of low fence, for enclosures and the yard.", "Одна клітинка низького паркану для загонів і двору.");
            Add("furniture.decor_plant_pot.name", "Potted Plant", "Рослина в горщику");
            Add("furniture.decor_plant_pot.desc", "A leafy plant in a pot.", "Листяна рослина в горщику.");
            Add("furniture.decor_flowers.name", "Flowers", "Квіти");
            Add("furniture.decor_flowers.desc", "A clump of bright flowers.", "Кущик яскравих квітів.");
            Add("furniture.decor_bench.name", "Bench", "Лавка");
            Add("furniture.decor_bench.desc", "A wooden bench for weary customers.", "Дерев’яна лавка для втомлених покупців.");
            Add("furniture.decor_rug_round.name", "Round Rug", "Круглий килим");
            Add("furniture.decor_rug_round.desc", "A soft round rug.", "М’який круглий килим.");
            Add("furniture.decor_bookshelf.name", "Bookcase", "Книжкова шафа");
            Add("furniture.decor_bookshelf.desc", "A bookcase full of pet-care books.", "Шафа, повна книжок про догляд за тваринами.");
            Add("furniture.decor_box_closed.name", "Sealed Box", "Заклеєна коробка");
            Add("furniture.decor_box_closed.desc", "A taped-up cardboard box.", "Заклеєна скотчем картонна коробка.");
            Add("furniture.decor_box_open.name", "Open Box", "Відкрита коробка");
            Add("furniture.decor_box_open.desc", "An opened cardboard box.", "Відкрита картонна коробка.");
            Add("furniture.decor_dustbin.name", "Dustbin", "Смітник");
            Add("furniture.decor_dustbin.desc", "Keeps the floor tidy.", "Тримає підлогу в чистоті.");
            Add("furniture.decor_pallet.name", "Pallet", "Піддон");
            Add("furniture.decor_pallet.desc", "A wooden shipping pallet.", "Дерев’яний транспортний піддон.");
            Add("furniture.decor_feed_bag.name", "Feed Sack", "Мішок корму");
            Add("furniture.decor_feed_bag.desc", "A sack of pet feed, for show.", "Мішок корму для тварин, для краси.");
            Add("furniture.decor_fish_tank.name", "Fish Tank", "Акваріум");
            Add("furniture.decor_fish_tank.desc", "An aquarium on a stand.", "Акваріум на підставці.");
            Add("furniture.decor_notice_board.name", "Notice Board", "Дошка оголошень");
            Add("furniture.decor_notice_board.desc", "A board of community notices.", "Дошка з місцевими оголошеннями.");
            Add("furniture.decor_lead_rail.name", "Lead Rail", "Стійка з повідцями");
            Add("furniture.decor_lead_rail.desc", "A rail of dog leads on display.", "Стійка з повідцями для собак.");
        }
    }
}
