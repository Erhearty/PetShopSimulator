using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Title, pause, settings, HUD and hotkey help text.</summary>
    public static class Menus
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Settings
            Add("settings.title", "Settings", "Налаштування");
            Add("settings.capture_prompt", "Press a key… ({0} to cancel)", "Натисніть клавішу… ({0} — скасувати)");
            Add("settings.reset", "Reset to defaults", "Скинути до типових");
            Add("settings.controls", "Controls", "Керування");
            Add("settings.general", "General", "Загальні");
            Add("settings.rebind", "Rebind", "Змінити");
            Add("settings.reduce_motion", "Reduce motion: {0}", "Менше анімації: {0}");
            Add("settings.reduce_motion.on", "Reduce motion on.", "Менше анімації: увімкнено.");
            Add("settings.reduce_motion.off", "Reduce motion off.", "Менше анімації: вимкнено.");
            Add("settings.tracker", "Show quest tracker: {0}", "Трекер завдань: {0}");
            Add("settings.tracker.on", "Quest tracker on.", "Трекер завдань увімкнено.");
            Add("settings.tracker.off", "Quest tracker off.", "Трекер завдань вимкнено.");
            Add("settings.autosave", "Autosave each morning: {0}", "Автозбереження щоранку: {0}");
            Add("settings.autosave.on", "Autosave each morning on.", "Автозбереження щоранку увімкнено.");
            Add("settings.autosave.off", "Autosave each morning off.", "Автозбереження щоранку вимкнено.");
            Add("settings.language", "Language: {0}", "Мова: {0}");
            Add("settings.language.changed", "Language: {0}.", "Мова: {0}.");
            Add("settings.rebind_cancelled", "Rebind cancelled.", "Зміну клавіші скасовано.");
            Add("settings.bound", "{0} is now {1}.", "{0} тепер на {1}.");
            Add("settings.bound_moved", "{0} moved to {1}.", "{0} перенесено на {1}.");
            Add("settings.controls_reset", "Controls reset to defaults.", "Керування скинуто до типового.");

            // Action names (settings rebind list)
            Add("action.move_forward", "Move forward", "Рух уперед");
            Add("action.move_back", "Move back", "Рух назад");
            Add("action.move_left", "Move left", "Рух ліворуч");
            Add("action.move_right", "Move right", "Рух праворуч");
            Add("action.jump", "Jump", "Стрибок");
            Add("action.interact", "Interact", "Взаємодія");
            Add("action.end_day", "Close up for the day", "Зачинити на сьогодні");
            Add("action.quick_save", "Quick save", "Швидке збереження");
            Add("action.ledger", "Shop book", "Книга обліку");
            Add("action.build_mode", "Build mode", "Будівництво");
            Add("action.build_rotate", "Rotate (build)", "Обертати (будівництво)");
            Add("action.build_remove", "Remove (build)", "Прибрати (будівництво)");
            Add("action.guide", "Guide", "Довідник");

            // Title screen
            Add("title.kicker", "A COSY MANAGEMENT SIM", "ЗАТИШНИЙ СИМУЛЯТОР КРАМНИЦІ");
            Add("title.name", "Paws &\nWhiskers", "Лапки й\nвусики");
            Add("title.blurb",
                "Run the pet shop on the corner.\nKeep the shelves full, raise the animals,\nand make rent.",
                "Керуйте зоокрамницею на розі.\nТримайте полиці повними, доглядайте тварин\nі платіть оренду.");
            Add("title.continue", "Continue", "Продовжити");
            Add("title.new_shop", "New shop", "Нова крамниця");
            Add("title.overwrite", "Overwrite?", "Перезаписати?");
            Add("title.quit", "Quit", "Вийти");
            Add("title.skip_tutorial", "Skip tutorial: {0}", "Без навчання: {0}");
            Add("title.slot.empty", "Slot {0} — empty", "Слот {0} — порожній");
            Add("title.slot.saved", "Slot {0} — Day {1} · €{2:N0} · saved {3}", "Слот {0} — день {1} · €{2:N0} · збережено {3}");

            // Pause menu
            Add("pause.title", "Paused", "Пауза");
            Add("pause.resume", "Resume", "Продовжити");
            Add("pause.save", "Save game", "Зберегти гру");
            Add("pause.saved", "Saved.", "Збережено.");
            Add("pause.save_failed", "Save failed — see log.", "Не вдалося зберегти — див. журнал.");
            Add("pause.music", "Music: {0}", "Музика: {0}");
            Add("pause.abandon", "Abandon shop", "Покинути крамницю");
            Add("pause.abandon_confirm", "Press again to abandon", "Натисніть ще раз, щоб покинути");
            Add("pause.abandon_warning", "Unsaved progress will be lost. {0} to cancel.", "Незбережений прогрес буде втрачено. {0} — скасувати.");
            Add("pause.save_quit", "Save and quit", "Зберегти й вийти");
            Add("pause.quit_failed", "Save failed — not quitting.", "Не вдалося зберегти — вихід скасовано.");
            Add("pause.esc_resume", "{0} to resume", "{0} — продовжити");
            Add("pause.status", "Slot {0} · Day {1} · € {2:N0}", "Слот {0} · день {1} · € {2:N0}");

            // Game over
            Add("gameover.title", "The shop has closed", "Крамниця зачинилася");
            Add("gameover.restart", "Start over", "Почати знову");
            Add("gameover.body", "{0}\n\nBetter luck next time.", "{0}\n\nНаступного разу пощастить більше.");

            // Hotkey cheat-sheet
            Add("hotkey.title", "<b>Controls</b>", "<b>Керування</b>");
            Add("hotkey.move", "{0}  move  ·  {1}  jump", "{0}  рух  ·  {1}  стрибок");
            Add("hotkey.look", "Mouse  look", "Миша  огляд");
            Add("hotkey.interact", "{0}  interact / restock / unpack", "{0}  взаємодія / поповнити / розпакувати");
            Add("hotkey.build_view", "{0}  build view", "{0}  режим будівництва");
            Add("hotkey.place_slot", "{0}  place from the inventory bar", "{0}  поставити з панелі інвентарю");
            Add("hotkey.place", "{0} place  ·  {1} cancel", "{0} поставити  ·  {1} скасувати");
            Add("hotkey.rotate_keys", "{0} / Shift+{0} / wheel", "{0} / Shift+{0} / коліщатко");
            Add("hotkey.rotate", "{0}  rotate", "{0}  обертати");
            Add("hotkey.remove", "Middle-click / {0}  remove", "Середня кнопка / {0}  прибрати");
            Add("hotkey.build_tools",
                "Build view: {0} wall · {1} window wall · {2} doorway · {3} fence · {4} remove tool · {5} orbit",
                "Будівництво: {0} стіна · {1} стіна з вікном · {2} дверний отвір · {3} паркан · {4} прибирання · {5} огляд");
            Add("hotkey.end_day", "{0}  close up early", "{0}  зачинити раніше");
            Add("hotkey.serve", "{0} at the counter  serve the queue", "{0} біля прилавка  обслужити чергу");
            Add("hotkey.ledger", "{0}  Shop book", "{0}  Книга обліку");
            Add("hotkey.journal", "{0}  quest journal", "{0}  журнал завдань");
            Add("hotkey.guide", "{0}  guide", "{0}  довідник");
            Add("hotkey.pause", "{0}  pause  ·  {1} save", "{0}  пауза  ·  {1} зберегти");
            Add("hotkey.title_hint",
                "{0} move  ·  {1} orbit  ·  {2} interact  ·  {3} Shop book  ·  {4} build view  ·  {5} rotate  ·  {6} journal",
                "{0} рух  ·  {1} огляд  ·  {2} взаємодія  ·  {3} Книга обліку  ·  {4} будівництво  ·  {5} обертати  ·  {6} журнал");

            // HUD
            Add("hud.today", "today  +€ {0:N0}  /  −€ {1:N0}   =  {2}", "сьогодні  +€ {0:N0}  /  −€ {1:N0}   =  {2}");
            Add("hud.rent", "Rent tonight  € {0:N0}", "Оренда ввечері  € {0:N0}");
            Add("hud.reputation", "Reputation", "Репутація");
            Add("hud.shoppers", "shoppers  {0}", "покупців  {0}");
            Add("hud.queue", "till: {0} waiting  €{1:N0}", "каса: у черзі {0}  €{1:N0}");
            Add("hud.thanks", "Thanks!", "Дякую!");
            Add("hud.build_hint.remove", "Remove tool — {0} / middle-click / {1} remove · {2} cancel",
                "Прибирання — {0} / середня кнопка / {1} прибрати · {2} скасувати");
            Add("hud.build_hint.place", "Placing {0} — {1} place · middle-click / {2} remove · {3} cancel",
                "Розміщення: {0} — {1} поставити · середня кнопка / {2} прибрати · {3} скасувати");
            Add("hud.alert.delivery", "A delivery is waiting on the forecourt — press {0} at it to take it in.",
                "На майданчику чекає доставка — натисніть {0} біля неї, щоб забрати.");
            Add("hud.alert.deliveries.one", "{0} delivery is waiting on the forecourt.", "{0} доставка чекає на майданчику.");
            Add("hud.alert.deliveries.other", "{0} deliveries are waiting on the forecourt.", "{0} доставок чекають на майданчику.");
            uk["hud.alert.deliveries.few"]  = "{0} доставки чекають на майданчику.";
            uk["hud.alert.deliveries.many"] = "{0} доставок чекають на майданчику.";
            Add("hud.alert.queue_one", "Someone is waiting at the till — press {0} behind the counter to serve them.",
                "Біля каси чекає покупець — натисніть {0} за прилавком, щоб обслужити.");
            Add("hud.alert.queue.one", "{0} person is waiting at the till.", "{0} людина чекає біля каси.");
            Add("hud.alert.queue.other", "{0} people are waiting at the till.", "{0} людей чекають біля каси.");
            uk["hud.alert.queue.few"]  = "{0} людини чекають біля каси.";
            uk["hud.alert.queue.many"] = "{0} людей чекають біля каси.";
            Add("hud.alert.feed_one", "A pen needs feeding — press {0} at it.", "Вольєр треба нагодувати — натисніть {0} біля нього.");
            Add("hud.alert.feed.one", "{0} pen needs feeding and mucking out.", "{0} вольєр треба нагодувати й прибрати.");
            Add("hud.alert.feed.other", "{0} pens need feeding and mucking out.", "{0} вольєрів треба нагодувати й прибрати.");
            uk["hud.alert.feed.few"]  = "{0} вольєри треба нагодувати й прибрати.";
            uk["hud.alert.feed.many"] = "{0} вольєрів треба нагодувати й прибрати.";
            Add("hud.alert.rent", "Rent tonight is € {0:N0} and you have € {1:N0} — sell something.",
                "Оренда сьогодні ввечері — € {0:N0}, а у вас € {1:N0} — продайте щось.");
            Add("hud.alert.shelf_one", "A shelf is empty — walk up to it and press {0} to restock.",
                "Полиця порожня — підійдіть до неї й натисніть {0}, щоб поповнити.");
            Add("hud.alert.shelves.one", "{0} shelf is empty — press {1} at it to restock.",
                "{0} полиця порожня — натисніть {1} біля неї, щоб поповнити.");
            Add("hud.alert.shelves.other", "{0} shelves are empty — press {1} at each one to restock.",
                "{0} полиць порожні — натисніть {1} біля кожної, щоб поповнити.");
            uk["hud.alert.shelves.few"]  = "{0} полиці порожні — натисніть {1} біля кожної, щоб поповнити.";
            uk["hud.alert.shelves.many"] = "{0} полиць порожні — натисніть {1} біля кожної, щоб поповнити.";
            Add("hud.alert.pens_empty.one", "{0} pen is empty — press {1} at it to buy from the breeder.",
                "{0} вольєр порожній — натисніть {1} біля нього, щоб купити у заводчика.");
            Add("hud.alert.pens_empty.other", "{0} pens are empty — press {1} at a pen to buy from the breeder.",
                "{0} вольєрів порожні — натисніть {1} біля вольєра, щоб купити у заводчика.");
            uk["hud.alert.pens_empty.few"]  = "{0} вольєри порожні — натисніть {1} біля вольєра, щоб купити у заводчика.";
            uk["hud.alert.pens_empty.many"] = "{0} вольєрів порожні — натисніть {1} біля вольєра, щоб купити у заводчика.";
            Add("hud.alert.low_rep", "Reputation is low; keep the shelves stocked to bring customers back.",
                "Репутація низька: тримайте полиці заповненими, щоб покупці поверталися.");

            // Inventory bar
            Add("inventory.stockroom", "Stockroom", "Склад");

            // Build toolbar
            Add("toolbar.remove", "Remove\n[{0}]", "Прибрати\n[{0}]");
            Add("toolbar.locked", "{0} is not unlocked yet.", "«{0}» ще не відкрито.");
            Add("toolbar.no_money", "Not enough money — {0} costs €{1:N0}.", "Не вистачає грошей — «{0}» коштує €{1:N0}.");

            // Day results
            Add("results.title", "End of day", "Кінець дня");
            Add("results.heading", "Day {0} — closing time", "День {0} — зачиняємося");
            Add("results.figures",
                "Customers served   <b>{0}</b>\n" +
                "Items sold         <b>{1}</b>\n" +
                "Revenue            <b>€ {2:N2}</b>\n" +
                "Rent              <color=#F27370>-€ {3:N2}</color>\n" +
                "Wages             <color=#F27370>-€ {4:N2}</color>\n" +
                "Reputation         <b>{5:0}</b> / 100\n" +
                "Balance            <b>€ {6:N2}</b>",
                "Обслужено покупців   <b>{0}</b>\n" +
                "Продано товарів      <b>{1}</b>\n" +
                "Виручка              <b>€ {2:N2}</b>\n" +
                "Оренда              <color=#F27370>-€ {3:N2}</color>\n" +
                "Зарплати            <color=#F27370>-€ {4:N2}</color>\n" +
                "Репутація            <b>{5:0}</b> / 100\n" +
                "Баланс               <b>€ {6:N2}</b>");
            Add("results.breakdown_title", "Where the money came from", "Звідки прийшли гроші");
            Add("results.bucket.animals", "Animals", "Тварини");
            Add("results.bucket.food", "Food", "Корм");
            Add("results.bucket.toys", "Toys", "Іграшки");
            Add("results.bucket.accessory", "Accessory", "Аксесуари");
            Add("results.bucket.medicine", "Medicine", "Ліки");
            Add("results.nothing_sold", "Nothing sold today.", "Сьогодні нічого не продано.");
            Add("results.profit", "Profit on the day:  + € {0:N2}", "Прибуток за день:  + € {0:N2}");
            Add("results.loss", "Loss on the day:  − € {0:N2}", "Збиток за день:  − € {0:N2}");
            Add("results.continue", "Open up tomorrow  ›", "Відчинитися завтра  ›");
            Add("results.advice.empty_shelves.one",
                "· <color=#F27370>{0} shelf is empty.</color> Press {1} at a shelf to refill — the whole shop would cost € {2:N0}.",
                "· <color=#F27370>{0} полиця порожня.</color> Натисніть {1} біля полиці, щоб поповнити, — уся крамниця коштуватиме € {2:N0}.");
            Add("results.advice.empty_shelves.other",
                "· <color=#F27370>{0} shelves are empty.</color> Press {1} at a shelf to refill — the whole shop would cost € {2:N0}.",
                "· <color=#F27370>{0} полиць порожні.</color> Натисніть {1} біля полиці, щоб поповнити, — уся крамниця коштуватиме € {2:N0}.");
            uk["results.advice.empty_shelves.few"] =
                "· <color=#F27370>{0} полиці порожні.</color> Натисніть {1} біля полиці, щоб поповнити, — уся крамниця коштуватиме € {2:N0}.";
            uk["results.advice.empty_shelves.many"] =
                "· <color=#F27370>{0} полиць порожні.</color> Натисніть {1} біля полиці, щоб поповнити, — уся крамниця коштуватиме € {2:N0}.";
            Add("results.advice.empty_pens.one",
                "· {0} pen stands empty. Press {1} at a pen to buy from the breeder.",
                "· {0} вольєр стоїть порожній. Натисніть {1} біля вольєра, щоб купити у заводчика.");
            Add("results.advice.empty_pens.other",
                "· {0} pens stand empty. Press {1} at a pen to buy from the breeder.",
                "· {0} вольєрів стоять порожні. Натисніть {1} біля вольєра, щоб купити у заводчика.");
            uk["results.advice.empty_pens.few"]  = "· {0} вольєри стоять порожні. Натисніть {1} біля вольєра, щоб купити у заводчика.";
            uk["results.advice.empty_pens.many"] = "· {0} вольєрів стоять порожні. Натисніть {1} біля вольєра, щоб купити у заводчика.";
            Add("results.advice.low_rep", "· Reputation is low, so few customers come. Keep stock on the shelves.",
                "· Репутація низька, тож покупців мало. Тримайте товар на полицях.");
            Add("results.advice.unfed.one",
                "· <color=#F27370>{0} pen went unfed.</color> Neglected animals lose condition and sell for far less.",
                "· <color=#F27370>{0} вольєр лишився без корму.</color> Занедбані тварини втрачають форму й продаються значно дешевше.");
            Add("results.advice.unfed.other",
                "· <color=#F27370>{0} pens went unfed.</color> Neglected animals lose condition and sell for far less.",
                "· <color=#F27370>{0} вольєрів лишилися без корму.</color> Занедбані тварини втрачають форму й продаються значно дешевше.");
            uk["results.advice.unfed.few"] =
                "· <color=#F27370>{0} вольєри лишилися без корму.</color> Занедбані тварини втрачають форму й продаються значно дешевше.";
            uk["results.advice.unfed.many"] =
                "· <color=#F27370>{0} вольєрів лишилися без корму.</color> Занедбані тварини втрачають форму й продаються значно дешевше.";
            Add("results.advice.loss", "· You lost money today. Rent rises to € {0:N0} tomorrow.",
                "· Сьогодні ви втратили гроші. Завтра оренда зросте до € {0:N0}.");
            Add("results.advice.loss_wages", "· You lost money today. Rent rises to € {0:N0} tomorrow, plus € {1:N0} in wages.",
                "· Сьогодні ви втратили гроші. Завтра оренда зросте до € {0:N0}, плюс € {1:N0} на зарплати.");
            Add("results.advice.cash_tight", "· <color=#E8C468>Cash is tight — do not overspend on building.</color>",
                "· <color=#E8C468>Грошей обмаль — не витрачайте забагато на будівництво.</color>");
            Add("results.advice.good", "· The shop is in good shape. Consider another shelf or pen to grow.",
                "· Крамниця в доброму стані. Подумайте про ще одну полицю чи вольєр, щоб рости.");
            Add("results.show.disqualified", "· Pet show: your entry was disqualified.",
                "· Виставка тварин: вашого учасника дискваліфіковано.");
            Add("results.show.placed", "· Pet show: placed #{0}, prize € {1:N0}.",
                "· Виставка тварин: місце #{0}, приз € {1:N0}.");
            Add("results.show.tomorrow", "· Pet show tomorrow — enter a {0} from the breeding panel.",
                "· Завтра виставка тварин — заявіть тварину виду «{0}» у панелі розведення.");

            // Layout migration notices
            Add("layout.notice_prefix", "Your shop was rebuilt at the front of the yard",
                "Вашу крамницю перебудовано біля входу у двір");
            Add("layout.refund_prefix", "No room was left for a pen pushed off the yard, so its pets were sold back",
                "Для вольєра, що опинився за межами двору, не знайшлося місця, тож його тварин продано назад");
            Add("layout.notice", "{0}: {1}", "{0}: {1}");
            Add("layout.refund", "{0}: {1} for €{2:N2}", "{0}: {1} за €{2:N2}");
            Add("layout.items_packed.one", "{0} item moved to your furniture inventory", "{0} предмет переміщено до інвентарю меблів");
            Add("layout.items_packed.other", "{0} items moved to your furniture inventory", "{0} предметів переміщено до інвентарю меблів");
            uk["layout.items_packed.few"]  = "{0} предмети переміщено до інвентарю меблів";
            uk["layout.items_packed.many"] = "{0} предметів переміщено до інвентарю меблів";
            Add("layout.pens_moved.one", "{0} pen moved", "{0} вольєр переміщено");
            Add("layout.pens_moved.other", "{0} pens moved", "{0} вольєрів переміщено");
            uk["layout.pens_moved.few"]  = "{0} вольєри переміщено";
            uk["layout.pens_moved.many"] = "{0} вольєрів переміщено";
            Add("layout.pets_rehomed.one", "{0} pet moved to another pen", "{0} тварину переселено до іншого вольєра");
            Add("layout.pets_rehomed.other", "{0} pets moved to another pen", "{0} тварин переселено до іншого вольєра");
            uk["layout.pets_rehomed.few"]  = "{0} тварини переселено до іншого вольєра";
            uk["layout.pets_rehomed.many"] = "{0} тварин переселено до іншого вольєра";
            Add("layout.pens_kept.one", "{0} pen left where it stood", "{0} вольєр залишено на місці");
            Add("layout.pens_kept.other", "{0} pens left where they stood", "{0} вольєрів залишено на місці");
            uk["layout.pens_kept.few"]  = "{0} вольєри залишено на місці";
            uk["layout.pens_kept.many"] = "{0} вольєрів залишено на місці";
        }
    }
}
