using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>The in-game Guide.</summary>
    public static class Guide
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Guide panel chrome
            Add("guide.heading", "Guide", "Довідник");
            Add("guide.prev", "Previous", "Попередня");
            Add("guide.next", "Next", "Наступна");
            // {0} current page, {1} page count.
            Add("guide.page", "Page {0} of {1}", "Сторінка {0} з {1}");

            // Shop book Guide page. {0} the guide key.
            Add("guide.book.intro", "Press {0} at any time to open it. It covers:",
                                    "Натисніть {0} будь-коли, щоб відкрити його. Тут описано:");
            Add("guide.book.open", "Open the guide", "Відкрити довідник");

            // {0} the interact key.
            Add("guide.serving.title", "Serving at the counter", "Обслуговування за прилавком");
            Add("guide.serving.body",
                "Customers who want to buy something queue at the till. Stand at the counter and press " +
                "{0} to serve whoever is at the front of the line. If nobody is waiting, " +
                "you are told so.\n\nEvery customer only waits so long. Serve the queue before they give up " +
                "and walk out, or your reputation suffers. A cashier you hire works the till from behind the " +
                "counter, but you can always serve as well to clear the line faster.",
                "Покупці, які хочуть щось придбати, стають у чергу до каси. Станьте біля прилавка й натисніть " +
                "{0}, щоб обслужити першого в черзі. Якщо ніхто не чекає, вам про це скажуть.\n\n" +
                "Кожен покупець чекає лише певний час. Обслужіть чергу, поки люди не втратили терпіння й " +
                "не пішли, інакше постраждає ваша репутація. Найнятий касир працює за касою з-за прилавка, " +
                "але ви завжди можете обслуговувати й самі, щоб черга рухалася швидше.");

            // {0} the interact key, {1} the Shop book key, {2} units per pallet.
            Add("guide.restocking.title", "Restocking shelves", "Поповнення полиць");
            Add("guide.restocking.body",
                "Walk up to a shelf and press {0} to fill it. Units come out of the " +
                "stockroom first. If the stockroom runs short, the rest is bought at cash-and-carry prices, " +
                "which cost a lot more.\n\nThe cheap way is to order ahead: open the Shop book with " +
                "{1}, go to Stock and order a pallet of {2} units for an aisle. " +
                "Auto-reorder can place those orders for you when the stockroom runs low.",
                "Підійдіть до полиці й натисніть {0}, щоб її заповнити. Товар спершу береться зі складу. " +
                "Якщо на складі бракує, решту докуповують за цінами гуртівні, а це значно дорожче.\n\n" +
                "Дешевше замовляти наперед: відкрийте Книгу обліку клавішею {1}, перейдіть на сторінку " +
                "«Товари» й замовте палету з {2} од. для ряду. Автозамовлення може робити такі замовлення " +
                "за вас, коли на складі закінчується товар.");

            // {0} the interact key.
            Add("guide.deliveries.title", "Deliveries on the forecourt", "Доставки на майданчик");
            Add("guide.deliveries.body",
                "Your shop opens straight onto the street. Wholesale orders and furniture arrive by van later in " +
                "the day and are left on the forecourt in front of the shop. Walk to a pallet and press {0} to carry " +
                "it into the stockroom, ready to shelve. A furniture crate unpacks into your inventory bar " +
                "instead.\n\nAnything still on a van when you close lands overnight, so nothing you paid for is lost. " +
                "The Stock page shows what is on the van and roughly when it arrives.",
                "Ваша крамниця виходить просто на вулицю. Замовлення з гуртівні та меблі привозить фургон " +
                "пізніше того ж дня й залишає на майданчику перед крамницею. Підійдіть до палети й натисніть " +
                "{0}, щоб віднести її на склад, звідки товар можна викладати на полиці. Ящик із меблями " +
                "натомість розпаковується у вашу панель інвентарю.\n\nУсе, що ще в дорозі на момент закриття, " +
                "прибуде вночі, тож нічого оплаченого не пропаде. Сторінка «Товари» показує, що їде у фургоні " +
                "і приблизно коли його привезуть.");

            // {0} the Shop book key, {1} the interact key, {2} the rotate keys, {3} the remove key.
            Add("guide.furniture.title", "Ordering and placing furniture", "Замовлення та розміщення меблів");
            Add("guide.furniture.body",
                "Open the Shop book with {0} and go to Build. Furniture is paid for when " +
                "you order it and arrives as a crate on the forecourt; unpack it with " +
                "{1}.\n\nPieces you own sit in the inventory bar. Press 1-9 to pick one up, " +
                "click to place it, and rotate with {2}. Middle-click or " +
                "{3} removes a piece, and Esc cancels.",
                "Відкрийте Книгу обліку клавішею {0} і перейдіть на сторінку «Будівництво». Меблі " +
                "оплачуються під час замовлення й прибувають ящиком на майданчик; розпакуйте його " +
                "клавішею {1}.\n\nВаші меблі лежать на панелі інвентарю. Натисніть 1-9, щоб узяти предмет, " +
                "клацніть, щоб поставити його, і обертайте клавішами {2}. Клацання середньою кнопкою миші " +
                "або {3} прибирає предмет, а Esc скасовує дію.");

            // {0} the build-view key; {1}–{4} the Wall, Window wall, Doorway and Fence tool keys; {5} the Remove tool key.
            Add("guide.walls.title", "Walls and structure", "Стіни та конструкції");
            Add("guide.walls.body",
                "Press {0} for the top-down build view. Its tool strip has " +
                "Wall ({1}), Window wall ({2}), " +
                "Doorway ({3}) and Fence ({4}). Walls are not " +
                "delivered: each piece is charged when you place it.\n\nThe Remove tool " +
                "({5}) takes pieces away again, including the shop's own room walls, " +
                "so you can open up the floor.\n\nWalls and fences can be built anywhere in the yard, not just " +
                "on the unlocked lot, so you can fence off the yard or put up outbuildings. The shop's back door " +
                "leads out into the yard behind it; keep a doorway there if you want to walk out to your pens. " +
                "Right-drag to orbit the view; Esc leaves it.",
                "Натисніть {0}, щоб перейти до режиму будівництва з видом згори. На панелі інструментів є " +
                "«Стіна» ({1}), «Стіна з вікном» ({2}), «Дверний отвір» ({3}) і «Паркан» ({4}). Стіни не " +
                "доставляють: кожен елемент оплачується, щойно ви його ставите.\n\nІнструмент «Прибрати» " +
                "({5}) знову забирає елементи, зокрема й стіни самої крамниці, тож ви можете розширити " +
                "простір.\n\nСтіни й паркани можна зводити будь-де на подвір’ї, а не лише на відкритій " +
                "ділянці, тож ви можете обгородити подвір’я чи звести господарські будівлі. Задні двері " +
                "крамниці ведуть на подвір’я позаду неї; залиште там дверний отвір, якщо хочете виходити " +
                "до своїх вольєрів. Тягніть правою кнопкою миші, щоб обертати огляд; Esc — вийти з режиму.");

            // {0} the interact key.
            Add("guide.animals.title", "Pens, buying, feeding and breeding", "Вольєри, купівля, годування та розведення");
            Add("guide.animals.body",
                "Press {0} at a pen. If the animals need feeding or fresh bedding, that " +
                "comes first and costs a small fee. Otherwise, if there is room, you buy a young animal from " +
                "the breeder.\n\nTwo adults in a pen with space to spare may breed overnight. Plan pairings in " +
                "the breeding planner, follow bloodlines in the family tree and enter animals in the showcase, " +
                "all from the Animals page of the Shop book.",
                "Натисніть {0} біля вольєра. Якщо тваринам потрібен корм чи свіжа підстилка, спершу " +
                "подбаєте про це за невелику плату. Інакше, якщо є місце, ви купите молоду тварину в " +
                "заводчика.\n\nДві дорослі тварини у вольєрі з вільним місцем можуть дати потомство вночі. " +
                "Плануйте пари в планувальнику розведення, стежте за лініями в родоводі й записуйте тварин " +
                "на виставку — усе це зі сторінки «Тварини» в Книзі обліку.");

            // {0} the Shop book key.
            Add("guide.staff.title", "Staff", "Персонал");
            Add("guide.staff.body",
                "Assistants work the till on their own, slower than you do. A cashier needs a counter: you can " +
                "only hire one once the shop has a counter, and they serve from behind it. Each one is paid a " +
                "daily wage, added to tonight's bill.\n\nOpen the staff board from the Staff page of the Shop book " +
                "({0}) to hire from the applicants looking for work; the list changes every " +
                "morning. Letting someone go costs you some reputation.",
                "Помічники самі працюють за касою, хоч і повільніше за вас. Касирові потрібен прилавок: " +
                "найняти касира можна лише тоді, коли в крамниці є прилавок, і він обслуговує з-за нього. " +
                "Кожному щодня виплачують зарплату, яка додається до вечірнього рахунку.\n\nВідкрийте дошку " +
                "персоналу на сторінці «Персонал» у Книзі обліку ({0}), щоб найняти когось із тих, хто шукає " +
                "роботу; список оновлюється щоранку. Звільнення працівника коштує вам частини репутації.");

            Add("guide.reputation.title", "Reputation and patience", "Репутація та терпіння");
            Add("guide.reputation.body",
                "Reputation runs from 0 to 100. Customers who leave with a full basket raise it. It drops when " +
                "someone gives up waiting in the queue, finds the shelves empty, cannot find the animal they " +
                "wanted, or when you fire staff.\n\nPrices matter too: dearer shelves make shoppers less likely " +
                "to buy. Reputation unlocks new tiers for the shop.",
                "Репутація вимірюється від 0 до 100. Її підвищують покупці, які йдуть із повним кошиком. " +
                "Вона падає, коли хтось утомлюється чекати в черзі, бачить порожні полиці, не знаходить " +
                "потрібної тварини або коли ви звільняєте працівника.\n\nЦіни теж важать: що дорожчі полиці, " +
                "то рідше покупці щось беруть. Репутація відкриває для крамниці нові рівні.");

            // {0} the end-day key.
            Add("guide.day.title", "The day and closing early", "День і раннє закриття");
            Add("guide.day.body",
                "Each day runs on a clock. Near closing time the doors stop letting new customers in. Press " +
                "{0} to close up early whenever you like.\n\nOvernight, animals may breed, " +
                "deliveries still on the road land, and rent and wages are paid. If the bills take your balance " +
                "below zero, the shop closes for good, so keep an eye on tonight's bill in the Shop book.",
                "Кожен день іде за годинником. Ближче до закриття нових покупців уже не впускають. Натисніть " +
                "{0}, щоб зачинитися раніше, коли забажаєте.\n\nУночі тварини можуть дати потомство, " +
                "прибувають доставки, що були в дорозі, і сплачуються оренда та зарплати. Якщо після " +
                "рахунків баланс стане від’ємним, крамниця закриється назавжди, тож стежте за вечірнім " +
                "рахунком у Книзі обліку.");

            // {0} the quest journal key.
            Add("guide.quests.title", "Quests and the journal", "Завдання та журнал");
            Add("guide.quests.body",
                "Press {0} to open the quest journal. It lists what you are working " +
                "on and how far along you are. The quest tracker on screen keeps the current goal in view " +
                "while you play.",
                "Натисніть {0}, щоб відкрити журнал завдань. У ньому видно, над чим ви працюєте і наскільки " +
                "просунулися. Трекер завдань на екрані тримає поточну мету перед очима, поки ви граєте.");

            // {0},{1},{3},{5} tier names; {2},{4},{6} the reputation each tier needs.
            Add("guide.tiers.title", "Tiers and unlocks", "Рівні та нові можливості");
            Add("guide.tiers.body",
                "Your shop climbs tiers as its reputation grows, and a tier once reached is never lost.\n\n" +
                "{0}: where every shop starts.\n" +
                "{1} (reputation {2:0}): the back strip of the lot opens up.\n" +
                "{3} (reputation {4:0}): horse pens.\n" +
                "{5} (reputation {6:0}): tiger pens and the whole yard.",
                "Зі зростанням репутації крамниця піднімається на нові рівні, а досягнутий рівень уже " +
                "не втрачається.\n\n" +
                "{0}: з цього починає кожна крамниця.\n" +
                "{1} (репутація {2:0}): відкривається задня смуга ділянки.\n" +
                "{3} (репутація {4:0}): вольєри для коней.\n" +
                "{5} (репутація {6:0}): вольєри для тигрів і все подвір’я.");

            // {0} the quick-save key, {1} the number of save slots.
            Add("guide.saving.title", "Saving", "Збереження");
            Add("guide.saving.body",
                "Press {0} to save at any time, or use Save game in the pause menu (Esc). " +
                "With Autosave on (in Settings) the game also saves itself each morning.\n\nThere are " +
                "{1} save slots; pick one on the title screen to continue it or start a new shop. " +
                "Save & quit in the pause menu only quits once the save has worked.",
                "Натисніть {0}, щоб зберегтися будь-коли, або скористайтеся пунктом «Зберегти гру» в меню " +
                "паузи (Esc). Якщо в налаштуваннях увімкнено автозбереження, гра також зберігається " +
                "щоранку.\n\nСлотів збереження: {1}; виберіть один на титульному екрані, щоб продовжити " +
                "гру або почати нову крамницю. «Зберегти й вийти» в меню паузи виходить із гри лише після " +
                "успішного збереження.");
        }
    }
}
