using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Management panels: Tab menu, staff, reorder, catalog, breeding, quests.</summary>
    public static class Panels
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Plural forms: English .one/.other, Ukrainian .one/.few/.many.
            void Plural(string key, string enOne, string enOther, string ukOne, string ukFew, string ukMany)
            {
                en[key + ".one"]   = enOne;
                en[key + ".other"] = enOther;
                uk[key + ".one"]   = ukOne;
                uk[key + ".few"]   = ukFew;
                uk[key + ".many"]  = ukMany;
            }

            // Shop book (Tab): chrome and page tabs
            Add("tabs.title", "Shop book", "Книга обліку");
            Add("tabs.nav_hint", "Up/Down, Tab or 1–6 to switch pages. Esc closes.",
                                 "Вгору/Вниз, Tab або 1–6 — перемкнути сторінку. Esc — закрити.");
            Add("tabs.overview", "Overview", "Огляд");
            Add("tabs.overview.purpose", "How the shop is doing today, and what needs you next.",
                                         "Як сьогодні справи в крамниці і що потребує вашої уваги.");
            Add("tabs.stock", "Stock", "Товари");
            Add("tabs.stock.purpose", "What is on the shelves and in the stockroom, and ordering more.",
                                      "Що є на полицях і на складі, а також замовлення.");
            Add("tabs.animals", "Animals", "Тварини");
            Add("tabs.animals.purpose", "Your pens, how the animals are cared for, and breeding.",
                                        "Ваші вольєри, догляд за тваринами та розведення.");
            Add("tabs.staff", "Staff", "Персонал");
            Add("tabs.staff.purpose", "Your assistants, their wages, and hiring.",
                                      "Ваші помічники, їхні зарплати та наймання.");
            Add("tabs.build", "Build", "Будівництво");
            Add("tabs.build.purpose", "Order furniture, place what you own, and change the walls.",
                                      "Замовляйте меблі, розставляйте придбане та змінюйте стіни.");
            Add("tabs.guide", "Guide", "Довідник");
            Add("tabs.guide.purpose", "How every part of running the shop works.",
                                      "Як влаштована кожна частина роботи крамниці.");

            // Build page tools
            Add("tabs.build.open_view", "Open build view", "Режим будівництва");
            Add("tabs.build.wall", "Wall", "Стіна");
            Add("tabs.build.window_wall", "Window wall", "Стіна з вікном");
            Add("tabs.build.doorway", "Doorway", "Дверний отвір");
            Add("tabs.build.fence", "Fence", "Паркан");
            Add("tabs.build.remove", "Remove", "Прибрати");

            // Overview page
            Add("overview.balance", "Balance  € {0:N2}", "Баланс  € {0:N2}");
            Add("overview.reputation", "Reputation  {0:0}/100", "Репутація  {0:0}/100");
            Add("overview.bill", "Tonight's bill  € {0:N2}  (rent and wages)", "Вечірній рахунок  € {0:N2}  (оренда і зарплати)");
            Add("overview.todo", "To do", "Справи");
            Add("overview.nothing_urgent", "Nothing urgent — the shop is running smoothly",
                                           "Нічого термінового — крамниця працює як годинник");
            // {0} customers queuing, {1} the interact key.
            Plural("overview.todo.queue",
                   "Customers waiting at the till — {0} customer is queuing; serve with {1} at the counter",
                   "Customers waiting at the till — {0} customers are queuing; serve with {1} at the counter",
                   "Біля каси чекають покупці — у черзі {0} покупець; обслужіть клавішею {1} біля прилавка",
                   "Біля каси чекають покупці — у черзі {0} покупці; обслужіть клавішею {1} біля прилавка",
                   "Біля каси чекають покупці — у черзі {0} покупців; обслужіть клавішею {1} біля прилавка");
            Add("overview.todo.bill", "Tonight's bill (€ {0:N0}) is more than you have — sell more before closing",
                                      "Вечірній рахунок (€ {0:N0}) більший, ніж у вас є, — продайте більше до закриття");
            // {0} category, {1} the interact key.
            Add("overview.todo.shelf_restock", "Shelf {0} empty — restock ({1})", "Полиця «{0}» порожня — поповніть ({1})");
            Add("overview.todo.shelf_order", "Shelf {0} empty and the stockroom is out — order on the Stock page",
                                             "Полиця «{0}» порожня, і на складі нічого немає — замовте на сторінці «Товари»");
            // {0} species, {1} the interact key, {2} service cost.
            Add("overview.todo.pen_feed", "{0} pen needs feeding — {1} at the pen (€ {2:N2})",
                                          "Вольєр «{0}» треба нагодувати — {1} біля вольєра (€ {2:N2})");
            Add("overview.todo.pen_clean", "{0} pen needs cleaning — {1} at the pen (€ {2:N2})",
                                           "Вольєр «{0}» треба прибрати — {1} біля вольєра (€ {2:N2})");
            Add("overview.todo.pen_empty", "{0} pen is empty — buy an animal with {1} at the pen",
                                           "Вольєр «{0}» порожній — купіть тварину клавішею {1} біля вольєра");

            // Animals page
            Add("animals.link.breeding", "Plan tonight's breeding", "Спланувати розведення");
            Add("animals.link.family_tree", "Family tree", "Родовід");
            Add("animals.link.showcase", "Showcase", "Виставка");
            Add("animals.no_animals", "No animals yet — buy one at a pen first.", "Тварин ще немає — спершу купіть одну біля вольєра.");
            Add("animals.no_pens", "No pens yet. Order one on the Build page.",
                                   "Вольєрів ще немає. Замовте вольєр на сторінці «Будівництво».");
            Add("animals.col.pen", "Pen", "Вольєр");
            Add("animals.col.animals", "Animals", "Тварини");
            Add("animals.col.feed", "Feed", "Корм");
            Add("animals.col.bedding", "Bedding", "Підстилка");
            Add("animals.col.note", "Note", "Примітка");
            Add("animals.prices", "New animals cost", "Ціни на нових тварин");
            Add("animals.note.empty", "Empty — press {0} at the pen to buy one",
                                      "Порожньо — натисніть {0} біля вольєра, щоб купити тварину");
            Add("animals.note.needs_care", "Needs care — {0} at the pen, € {1:N2}", "Потрібен догляд — {0} біля вольєра, € {1:N2}");
            Add("animals.note.may_breed", "May breed tonight", "Уночі можливе потомство");
            Add("animals.note.needs_adults", "Needs two adults to breed", "Для розведення потрібні дві дорослі тварини");
            Add("animals.note.full", "Full — no room for young", "Заповнено — немає місця для малят");

            // Stock page
            // {0} units per order, {1} category.
            Add("stock.order", "Order {0} {1}", "Замовити {1} ×{0}");
            Add("stock.prices_down", "Prices  −10%", "Ціни  −10%");
            Add("stock.prices_up", "Prices  +10%", "Ціни  +10%");
            Add("stock.prices_line",
                "Prices are at <b>{0:0}%</b> of list; shoppers are <b>{1:0}%</b> as likely to buy as at list price.",
                "Ціни становлять <b>{0:0}%</b> від прейскуранта; покупці купують з імовірністю <b>{1:0}%</b> від тієї, що за прейскурантною ціною.");
            Add("stock.no_shelves", "No shelves yet. Order one on the Build page.",
                                    "Полиць ще немає. Замовте полицю на сторінці «Будівництво».");
            Add("stock.col.shelf", "Shelf", "Полиця");
            Add("stock.col.on_shelf", "On shelf", "На полиці");
            Add("stock.col.in_stockroom", "In stockroom", "На складі");
            Add("stock.col.refill_cost", "Refill cost", "Ціна поповнення");
            Add("stock.col.category", "Category", "Категорія");
            Add("stock.col.order_of", "Order of {0}", "Партія з {0}");
            Add("stock.col.at_shelf", "At the shelf", "Біля полиці");
            Plural("stock.units", "{0} unit", "{0} units", "{0} шт.", "{0} шт.", "{0} шт.");
            Add("stock.refill_hint", "Walk up to a shelf and press {0} to refill it from the stockroom.",
                                     "Підійдіть до полиці й натисніть {0}, щоб поповнити її зі складу.");
            // {0} units, {1} category, {2} seconds.
            Add("stock.on_van", "On the van: {0} {1} — arriving in about {2} s",
                                "У фургоні: {0} од. ({1}) — прибуде приблизно за {2} с");

            // Staff page of the book and the staff board
            Add("staffpanel.open_board", "Open staff board — hire and fire", "Дошка персоналу — найм і звільнення");
            // {0} assistants, {1} wages.
            Plural("staffpanel.payroll_summary",
                   "{0} assistant on the payroll — € {1:N0} in wages tonight.",
                   "{0} assistants on the payroll — € {1:N0} in wages tonight.",
                   "{0} помічник у штаті — € {1:N0} зарплати сьогодні ввечері.",
                   "{0} помічники у штаті — € {1:N0} зарплати сьогодні ввечері.",
                   "{0} помічників у штаті — € {1:N0} зарплати сьогодні ввечері.");
            Add("staffpanel.wage_day", "€ {0:N0} a day", "€ {0:N0} на день");
            Add("staffpanel.service_every", "one customer every {0:0.#} s", "один покупець кожні {0:0.#} с");
            Add("staffpanel.nobody_till", "Nobody on the till — you must serve every customer yourself.",
                                          "На касі нікого — вам доведеться обслуговувати кожного покупця самостійно.");
            Add("staffpanel.assistants_note",
                "Assistants work the till on their own, slower than you; you can still help clear the queue.",
                "Помічники працюють на касі самі, повільніше за вас; ви все одно можете допомогти розібрати чергу.");
            Add("staffpanel.bill",
                "Tonight's bill: rent € {0:N2} + wages € {1:N2} = <b>€ {2:N2}</b>, against € {3:N2} in hand.",
                "Вечірній рахунок: оренда € {0:N2} + зарплати € {1:N2} = <b>€ {2:N2}</b>, а в касі € {3:N2}.");
            Add("staffpanel.title", "Staff", "Персонал");
            Add("staffpanel.applicants", "Looking for work today", "Сьогодні шукають роботу");
            Add("staffpanel.on_payroll", "On the payroll", "У штаті");
            // {0} role counts, {1} wages, {2} balance, {3} free places.
            Add("staffpanel.summary", "{0}  ·  wages € {1:N0} tonight  ·  balance € {2:N0}  ·  room for {3} more",
                                      "{0}  ·  зарплати € {1:N0} сьогодні  ·  баланс € {2:N0}  ·  вільних місць: {3}");
            Plural("staffpanel.role_count.cashier", "{0} cashier", "{0} cashiers", "{0} касир", "{0} касири", "{0} касирів");
            Plural("staffpanel.role_count.restocker", "{0} restocker", "{0} restockers", "{0} комірник", "{0} комірники", "{0} комірників");
            Plural("staffpanel.role_count.feeder", "{0} feeder", "{0} feeders", "{0} доглядач", "{0} доглядачі", "{0} доглядачів");
            Add("staffpanel.no_candidates", "Nobody is looking for work today — try again tomorrow.",
                                            "Сьогодні ніхто не шукає роботи — спробуйте завтра.");
            // {0} speed word, {1} seconds per customer.
            Add("staffpanel.speed", "{0}  —  one customer every {1:0.#} s", "{0}  —  один покупець кожні {1:0.#} с");
            Add("staffpanel.terms", "€ {0:N0} a day\n€ {1:N0} to sign", "€ {0:N0} на день\n€ {1:N0} за підписання");
            Add("staffpanel.hire", "Hire", "Найняти");
            Add("staffpanel.reason.no_counter", "Place a counter first", "Спершу поставте прилавок");
            Add("staffpanel.reason.no_room", "No room", "Немає місць");
            Add("staffpanel.no_staff", "Nobody on the payroll — you will have to serve every customer yourself.",
                                       "У штаті нікого — вам доведеться обслуговувати кожного покупця самостійно.");
            Add("staffpanel.make_role", "Make {0}", "Перевести: {0}");
            Add("staffpanel.let_go", "Let go", "Звільнити");

            // Auto-reorder
            Add("reorder.title", "Auto-reorder", "Автозамовлення");
            Add("reorder.open", "Auto-reorder…", "Автозамовлення…");
            Add("reorder.col.aisle", "Aisle", "Ряд");
            Add("reorder.col.auto", "Auto", "Авто");
            Add("reorder.col.threshold", "Order when below", "Замовити, коли менше");
            Add("reorder.col.units", "Order size", "Розмір замовлення");
            Add("reorder.col.cost", "Cost per order", "Ціна замовлення");
            Add("reorder.on", "On", "Увімк.");
            Add("reorder.off", "Off", "Вимк.");

            // Pet show
            Add("showcase.title", "Pet show", "Виставка тварин");
            Add("showcase.withdraw", "Withdraw", "Зняти з участі");
            Add("showcase.cannot_enter", "Cannot enter the show (fee €{0:N0}).", "Не вдалося записатися на виставку (внесок €{0:N0}).");
            Add("showcase.tonight", "tonight", "сьогодні ввечері");
            Plural("showcase.in_days", "in {0} day", "in {0} days", "через {0} день", "через {0} дні", "через {0} днів");
            // {0} show day, {1} when, {2} species, {3} coat, {4} entry fee, {5} your entry.
            Add("showcase.info",
                "Next show: day {0} ({1})\n" +
                "Judges want: <b>{2}</b> with a <b>{3}</b> coat\n" +
                "Entry fee: €{4:N0} (not refunded if you withdraw)\n" +
                "Your entry: {5}\n" +
                "The theme species is drawn from the pets you own at closing time.",
                "Наступна виставка: день {0} ({1})\n" +
                "Судді чекають: <b>{2}</b>, забарвлення «<b>{3}</b>»\n" +
                "Внесок: €{4:N0} (не повертається, якщо зняти тварину з участі)\n" +
                "Ваш учасник: {5}\n" +
                "Вид для теми обирають серед тварин, які є у вас на момент закриття.");
            Add("showcase.no_adults", "No adult {0} to enter.", "Немає дорослих тварин виду «{0}» для участі.");
            Add("showcase.estimate", "est. #{0}, prize €{1:N0}", "прогноз: #{0}, приз €{1:N0}");
            Add("showcase.enter", "Enter", "Записати");
            Add("showcase.none", "none", "немає");
            Add("showcase.last.none", "Last show: no result yet.", "Остання виставка: результатів ще немає.");
            Add("showcase.last.disqualified", "Last show: disqualified.", "Остання виставка: дискваліфікація.");
            Add("showcase.last.placed", "Last show: placed #{0}, prize €{1:N0}.", "Остання виставка: місце #{0}, приз €{1:N0}.");

            // Breeding
            Add("breeding.title", "Breeding", "Розведення");
            Add("breeding.tonight_pairing", "Tonight's pairing", "Пара на сьогоднішню ніч");
            Add("breeding.pair", "Pair for tonight", "Звести на ніч");
            Add("breeding.clear", "Clear pairing", "Скасувати пару");
            Add("breeding.paired_notice", "{0} and {1} are paired for tonight.", "{0} і {1} — пара на сьогоднішню ніч.");
            Add("breeding.no_pens", "no pens with two adults", "немає вольєрів із двома дорослими");
            Add("breeding.no_pens_help",
                "Breeding needs two fully grown animals in the same pen.\nBuy another adult at a pen, or wait for a baby to grow up.",
                "Для розведення потрібні дві дорослі тварини в одному вольєрі.\nКупіть ще одну дорослу тварину біля вольєра або зачекайте, поки підросте маля.");
            Add("breeding.pen_label", "{0} pen  ({1} of {2})", "Вольєр «{0}»  ({1} з {2})");
            Add("breeding.paired_tonight", "Paired tonight:", "Пара на ніч:");
            Add("breeding.selected", "Selected:", "Вибрано:");
            Add("breeding.pen_full", "The pen is full — no room for a baby.", "Вольєр заповнений — немає місця для маляти.");
            Plural("breeding.grown_in", "grown in {0} day", "grown in {0} days",
                   "виросте через {0} день", "виросте через {0} дні", "виросте через {0} днів");
            Add("breeding.tree", "Tree", "Родовід");
            Add("breeding.aim_for", "Aim for: {0}", "Мета: {0}");
            Add("breeding.any_coat", "Any", "будь-яке");
            Add("breeding.min_rarity", "min rarity {0}", "мін. рідкість: {0}");
            Add("breeding.any_rarity", "Any", "будь-яка");
            Add("breeding.target_chance", "Chance of target: {0}", "Шанс досягти мети: {0}");
            Add("breeding.best", "best {0}", "найкраще {0}");

            // Family tree
            Add("family.title", "Family tree", "Родовід");
            Add("family.title_named", "Family tree — {0}", "Родовід — {0}");
            Add("family.back", "Back", "Назад");
            Add("family.hint", "Click an ancestor to centre the tree on them.", "Клацніть предка, щоб поставити його в центр родоводу.");
            Add("family.unknown", "unknown", "невідомо");
            Add("family.sold", "sold", "продано");
            Add("family.generation", "gen {0}", "покоління {0}");

            // Furniture catalogue (Build page)
            Add("catalog.tab.furniture", "Furniture", "Меблі");
            Add("catalog.tab.structure", "Structure", "Конструкції");
            Add("catalog.tab.decoration", "Decoration", "Декор");
            Add("catalog.order", "Order", "Замовити");
            Add("catalog.place", "Place", "Поставити");
            Add("catalog.prev", "‹ Prev", "‹ Назад");
            Add("catalog.next", "Next ›", "Далі ›");
            Add("catalog.page", "Page {0} / {1}", "Сторінка {0} / {1}");
            Add("catalog.balance", "Balance € {0:N0}", "Баланс € {0:N0}");
            Add("catalog.counts", "owned {0}\non order {1}", "у запасі {0}\nзамовлено {1}");
            Add("catalog.unlocks_at", "unlocks at {0}", "відкривається на рівні «{0}»");
            Add("catalog.ordered", "Ordered a {0} — it arrives on the forecourt later today.",
                                   "Замовлено «{0}» — доставка на майданчик пізніше сьогодні.");
            Add("catalog.no_shop", "Cannot order a {0} — there is no shop to pay for it.",
                                   "Не вдається замовити «{0}» — немає крамниці, яка б за це заплатила.");
            Add("catalog.no_money", "Not enough money for a {0} (€ {1:N0}).", "Не вистачає грошей на «{0}» (€ {1:N0}).");
            Add("catalog.none_owned", "You have no {0} — order one first.", "У вас немає «{0}» — спершу замовте.");
            Add("catalog.pickup_failed", "Could not pick up the {0}.", "Не вдалося взяти «{0}».");

            // Quest journal and tracker
            Add("journal.title", "Quest journal", "Журнал завдань");
            // {0} the journal key.
            Add("journal.footer", "Up/Down: chapter  ·  PageUp/PageDown or wheel: scroll  ·  {0} or Esc: close",
                                  "Вгору/Вниз: розділ  ·  PageUp/PageDown або коліщатко: прокрутка  ·  {0} або Esc: закрити");
            Add("journal.unavailable", "Quests are not available.", "Завдання недоступні.");
            Add("journal.locked_notice", "Locked: finish the previous chapter to unlock this one.",
                                         "Закрито: завершіть попередній розділ, щоб відкрити цей.");
            Add("journal.chapter.tutorial", "Tutorial", "Навчання");
            Add("journal.chapter.early", "Early game", "Початок гри");
            Add("journal.chapter.mid", "Mid game", "Середина гри");
            Add("journal.chapter.end", "End game", "Фінал гри");
            Add("journal.state.locked", "locked", "закрито");
            Add("journal.state.upcoming", "up next", "далі");
            Add("journal.state.active", "active", "активне");
            Add("journal.state.done", "done", "виконано");
            Add("journal.reward", "Reward: €{0:N0}", "Нагорода: €{0:N0}");
            Add("journal.progress.complete", "Progress: complete", "Прогрес: виконано");
            Add("journal.progress.locked", "Progress: locked", "Прогрес: закрито");
            Add("journal.progress.counter", "Progress: {0}", "Прогрес: {0}");
            Add("journal.progress.pending", "Progress: not done yet", "Прогрес: ще не виконано");
            Add("tracker.all_done", "All quests complete", "Усі завдання виконано");
            // {0} step number, {1} step count, {2} title.
            Add("tracker.tutorial", "Tutorial {0}/{1}: {2}", "Навчання {0}/{1}: {2}");
            // {0} quests not shown, {1} the journal key.
            Add("tracker.more", "+{0} more ({1})", "+{0} ще ({1})");
        }
    }
}
