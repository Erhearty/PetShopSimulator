using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Quests and progression milestones.</summary>
    public static class Quests
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        /// <remarks>
        /// Quest text is keyed <c>quest.&lt;id&gt;.title</c> / <c>quest.&lt;id&gt;.desc</c>. Placeholders are
        /// positional: first the quest's text arguments (<c>QuestDefinition.TextArgs</c>,
        /// <c>{0}</c>…), then one per key the English instruction names, in the order the English
        /// instruction first names them (<c>QuestDefinition.KeyActions</c>). Each
        /// description's comment lists what its placeholders hold.
        /// </remarks>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            Add("quest.complete", "Quest complete: {0} — +€{1:N0}", "Завдання виконано: {0} — +€{1:N0}");

            // Counted goals: {0} is the count.
            Add("quest.count.shelves.one", "{0} shelf", "{0} полицю");
            Add("quest.count.shelves.other", "{0} shelves", "{0} полиць");
            uk["quest.count.shelves.few"]  = "{0} полиці";
            uk["quest.count.shelves.many"] = "{0} полиць";
            Add("quest.count.species.one", "{0} species", "{0} вид тварин");
            Add("quest.count.species.other", "{0} different species", "{0} різних видів тварин");
            uk["quest.count.species.few"]  = "{0} різні види тварин";
            uk["quest.count.species.many"] = "{0} різних видів тварин";
            Add("quest.count.staff.one", "{0} staff member", "{0} працівника");
            Add("quest.count.staff.other", "{0} staff", "{0} працівників");
            uk["quest.count.staff.few"]  = "{0} працівників";
            uk["quest.count.staff.many"] = "{0} працівників";

            // ── Tutorial ──────────────────────────────────────────────
            Add("quest.tut_open_catalogue.title", "Open the Build tab", "Відкрийте вкладку «Будівництво»");
            // {0} Ledger key.
            Add("quest.tut_open_catalogue.desc",
                "Your shop is empty. Press {0} to open the ledger and click its Build tab to see the furniture.",
                "Ваш магазин порожній. Натисніть {0}, щоб відкрити книгу обліку, і перейдіть на вкладку «Будівництво», щоб побачити меблі.");
            Add("quest.tut_order_counter.title", "Order a counter", "Замовте прилавок");
            Add("quest.tut_order_counter.desc",
                "In the Build tab, find the Counter row and click Order. It arrives as a crate on the forecourt.",
                "На вкладці «Будівництво» знайдіть рядок «Прилавок» і натисніть «Замовити». Його привезуть у ящику на майданчик перед магазином.");
            Add("quest.tut_collect_crate.title", "Collect the crate", "Заберіть ящик");
            // {0} Interact key.
            Add("quest.tut_collect_crate.desc",
                "Walk to the delivery crate on the forecourt and press {0} to unpack it.",
                "Підійдіть до ящика з доставкою на майданчику перед магазином і натисніть {0}, щоб розпакувати його.");
            Add("quest.tut_place_counter.title", "Place the counter", "Поставте прилавок");
            // {0} Ledger key, {1} BuildRotate key, {2} BuildMode key.
            Add("quest.tut_place_counter.desc",
                "Open the ledger's Build tab (press {0}), click Place on the Counter row, then left-click the floor to set it down. {1} rotates; {2} leaves the build view.",
                "Відкрийте вкладку «Будівництво» в книзі обліку (натисніть {0}), натисніть «Розмістити» в рядку «Прилавок», а потім клацніть лівою кнопкою по підлозі, щоб поставити його. {1} — повернути; {2} — вийти з режиму будівництва.");
            Add("quest.tut_order_shelf.title", "Order a shelf", "Замовте полицю");
            // {0} Ledger key.
            Add("quest.tut_order_shelf.desc",
                "Open the ledger's Build tab (press {0}) and click Order on the Small Shelf row.",
                "Відкрийте вкладку «Будівництво» в книзі обліку (натисніть {0}) і натисніть «Замовити» в рядку «Мала полиця».");
            Add("quest.tut_place_shelf.title", "Place the shelf", "Поставте полицю");
            // {0} Interact key, {1} Ledger key.
            Add("quest.tut_place_shelf.desc",
                "Unpack the shelf's crate with {0}, then open the ledger's Build tab (press {1}), click Place on the shelf row and left-click the floor.",
                "Розпакуйте ящик із полицею клавішею {0}, потім відкрийте вкладку «Будівництво» в книзі обліку (натисніть {1}), натисніть «Розмістити» в рядку полиці й клацніть лівою кнопкою по підлозі.");
            Add("quest.tut_order_stock.title", "Order stock", "Замовте товар");
            // {0} Ledger key, {1} Interact key.
            Add("quest.tut_order_stock.desc",
                "Press {0} to open the ledger, open its Catalogue tab and click one of the Order buttons. Restock a shelf with {1}.",
                "Натисніть {0}, щоб відкрити книгу обліку, перейдіть на вкладку «Каталог» і натисніть одну з кнопок «Замовити». Поповніть полицю клавішею {1}.");
            Add("quest.tut_first_sale.title", "Serve a customer", "Обслужіть покупця");
            // {0} Interact key.
            Add("quest.tut_first_sale.desc",
                "Your shop is open during the day. When a customer queues, stand at the counter and press {0} to serve them.",
                "Удень ваш магазин відчинений. Коли покупець стане в чергу, підійдіть до прилавка й натисніть {0}, щоб його обслужити.");
            Add("quest.tut_close_day.title", "Close the day", "Завершіть день");
            // {0} EndDay key.
            Add("quest.tut_close_day.desc",
                "Press {0} to close up and see the day's results.",
                "Натисніть {0}, щоб зачинити магазин і переглянути підсумки дня.");

            // ── Early ─────────────────────────────────────────────────
            Add("quest.early_three_shelves.title", "Own {0}", "Поставте {0}");
            // {0} shelf count (unused here), {1} Ledger key, {2} Interact key.
            Add("quest.early_three_shelves.desc",
                "Order shelves in the ledger's Build tab (press {1}), unpack each crate with {2} and place them.",
                "Замовте полиці на вкладці «Будівництво» в книзі обліку (натисніть {1}), розпакуйте кожен ящик клавішею {2} і розставте їх.");
            Add("quest.early_first_pen.title", "Open a pet corner", "Відкрийте куточок тварин");
            // {0} Ledger key, {1} Interact key.
            Add("quest.early_first_pen.desc",
                "Order a pen in the ledger's Build tab (press {0}) — each species has its own — unpack it with {1} and place it in the yard. It comes with a breeding pair.",
                "Замовте вольєр на вкладці «Будівництво» в книзі обліку (натисніть {0}) — для кожного виду свій, — розпакуйте його клавішею {1} і поставте на подвір’ї. До нього додається пара для розведення.");
            Add("quest.early_first_pet_sale.title", "Sell a pet", "Продайте тварину");
            // {0} Interact key.
            Add("quest.early_first_pet_sale.desc",
                "Walk up to a pen and press {0} to buy an animal for it. Customers will buy it from the pen.",
                "Підійдіть до вольєра й натисніть {0}, щоб купити для нього тварину. Покупці куплять її просто з вольєра.");
            Add("quest.early_first_staff.title", "Hire your first worker", "Найміть першого працівника");
            // {0} Ledger key.
            Add("quest.early_first_staff.desc",
                "Press {0}, open the Manage tab, click 'Staff board  —  hire and fire' and hire an applicant.",
                "Натисніть {0}, перейдіть на вкладку «Керування», натисніть «Дошка персоналу  —  найм і звільнення» й найміть кандидата.");
            Add("quest.early_auto_reorder.title", "Switch on auto-reorder", "Увімкніть автозамовлення");
            // {0} Ledger key.
            Add("quest.early_auto_reorder.desc",
                "Press {0}, open the Catalogue tab, click 'Auto-reorder…' and switch on at least one aisle.",
                "Натисніть {0}, перейдіть на вкладку «Каталог», натисніть «Автозамовлення…» й увімкніть принаймні один ряд.");
            Add("quest.early_revenue.title", "Take €{0:N0} in sales", "Наторгуйте на €{0:N0}");
            // {0} revenue target (unused here), {1} Interact key.
            Add("quest.early_revenue.desc",
                "Keep shelves stocked ({1} at a shelf) and serve customers at the counter ({1}).",
                "Тримайте полиці заповненими ({1} біля полиці) й обслуговуйте покупців за прилавком ({1}).");
            // {0} tier name, {1} reputation needed, {2} Ledger key.
            Add("quest.early_local_favourite.title", "Become a {0}", "Здобудьте статус «{0}»");
            Add("quest.early_local_favourite.desc",
                "Raise reputation to {1:0}: keep shelves full and pets fed. Check it in the ledger ({2}).",
                "Підніміть репутацію до {1:0}: тримайте полиці повними, а тварин — нагодованими. Перевірити її можна в книзі обліку ({2}).");

            // ── Mid ───────────────────────────────────────────────────
            Add("quest.mid_back_strip.title", "Build on the back strip", "Забудуйте задню ділянку");
            // {0} Ledger key.
            Add("quest.mid_back_strip.desc",
                "Open the ledger's Build tab (press {0}) and place any furniture on the newly opened strip behind the shop.",
                "Відкрийте вкладку «Будівництво» в книзі обліку (натисніть {0}) і поставте будь-які меблі на щойно відкритій ділянці за магазином.");
            Add("quest.mid_pass_inspection.title", "Pass an inspection", "Пройдіть перевірку");
            // {0} the interval, e.g. "7 days"; {1} Interact key.
            Add("quest.mid_pass_inspection.desc",
                "The inspector calls every {0}. Feed and clean pens ({1} at a pen) and keep shelves stocked to earn an A or B.",
                "Інспектор приходить кожні {0}. Годуйте тварин і прибирайте у вольєрах ({1} біля вольєра) та тримайте полиці заповненими, щоб отримати A або B.");
            Add("quest.mid_breed.title", "Breed a pet", "Отримайте потомство");
            // {0} Ledger key, {1} EndDay key.
            Add("quest.mid_breed.desc",
                "Press {0}, open the Animals tab, click 'Plan tonight's breeding' to pair two animals, then close the day ({1}).",
                "Натисніть {0}, перейдіть на вкладку «Тварини», натисніть «Спланувати парування на ніч», щоб звести дві тварини, а тоді завершіть день ({1}).");
            Add("quest.mid_species.title", "Sell {0}", "Продайте {0}");
            // {0} species count (unused here), {1} Interact key.
            Add("quest.mid_species.desc",
                "Place pens for different species (each has its own pen in the Build tab) and stock them with {1}.",
                "Поставте вольєри для різних видів (у кожного свій вольєр на вкладці «Будівництво») і заселіть їх клавішею {1}.");
            Add("quest.mid_staff.title", "Employ {0}", "Найміть {0}");
            // {0} staff count (unused here), {1} Ledger key.
            Add("quest.mid_staff.desc",
                "Press {1}, open the Manage tab and click 'Staff board  —  hire and fire' to hire another worker.",
                "Натисніть {1}, перейдіть на вкладку «Керування» й натисніть «Дошка персоналу  —  найм і звільнення», щоб найняти ще одного працівника.");
            Add("quest.mid_day_profit.title", "Make €{0:N0} profit in a day", "Заробіть €{0:N0} прибутку за день");
            // {0} profit target (unused here), {1} Ledger key.
            Add("quest.mid_day_profit.desc",
                "Order stock ahead in the ledger ({1}) — it is cheaper than restocking from the cash-and-carry.",
                "Замовляйте товар наперед у книзі обліку ({1}) — це дешевше, ніж докуповувати його на гуртівні.");
            // {0} tier name, {1} reputation needed, {2} Ledger key.
            Add("quest.mid_trusted_name.title", "Become a {0}", "Здобудьте статус «{0}»");
            Add("quest.mid_trusted_name.desc",
                "Raise reputation to {1:0}. Good inspections help; check it in the ledger ({2}).",
                "Підніміть репутацію до {1:0}. Допоможуть добрі оцінки інспектора; перевірити її можна в книзі обліку ({2}).");

            // ── End ───────────────────────────────────────────────────
            Add("quest.end_sell_horse.title", "Sell a horse", "Продайте коня");
            // {0} Ledger key.
            Add("quest.end_sell_horse.desc",
                "Order a horse pen in the ledger's Build tab (press {0}), place it in the yard and sell one of its horses.",
                "Замовте вольєр для коней на вкладці «Будівництво» в книзі обліку (натисніть {0}), поставте його на подвір’ї й продайте одного з коней.");
            Add("quest.end_grade_a.title", "Earn an A from the inspector", "Отримайте від інспектора оцінку A");
            // {0} Interact key.
            Add("quest.end_grade_a.desc",
                "Before an inspection day, feed and clean every pen ({0}) and fill every shelf.",
                "Перед днем перевірки нагодуйте тварин і приберіть у кожному вольєрі ({0}) та заповніть кожну полицю.");
            Add("quest.end_revenue.title", "Take €{0:N0} in sales", "Наторгуйте на €{0:N0}");
            // {0} revenue target (unused here), {1} Ledger key.
            Add("quest.end_revenue.desc",
                "Grow the shop: more shelves and pens, staff to serve, and auto-reorder in the ledger ({1}).",
                "Розвивайте магазин: більше полиць і вольєрів, персонал для обслуговування та автозамовлення в книзі обліку ({1}).");
            // {0} tier name, {1} reputation needed, {2} Ledger key.
            Add("quest.end_town_landmark.title", "Become a {0}", "Здобудьте статус «{0}»");
            Add("quest.end_town_landmark.desc",
                "Raise reputation to {1:0}. Check it in the ledger ({2}).",
                "Підніміть репутацію до {1:0}. Перевірити її можна в книзі обліку ({2}).");
            Add("quest.end_full_yard.title", "Build in the yard", "Забудуйте подвір’я");
            // {0} tier name, {1} Ledger key.
            Add("quest.end_full_yard.desc",
                "Open the ledger's Build tab (press {1}) and place furniture in the whole yard, unlocked at {0}.",
                "Відкрийте вкладку «Будівництво» в книзі обліку (натисніть {1}) і розставте меблі по всьому подвір’ю — воно відкривається на рівні «{0}».");
            Add("quest.end_sell_tiger.title", "Sell a tiger", "Продайте тигра");
            // {0} Ledger key.
            Add("quest.end_sell_tiger.desc",
                "Order a tiger pen in the ledger's Build tab (press {0}), place it in the yard and sell one of its tigers.",
                "Замовте вольєр для тигрів на вкладці «Будівництво» в книзі обліку (натисніть {0}), поставте його на подвір’ї й продайте одного з тигрів.");
        }
    }
}
