using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Products, sales, customers, assistants and shop events.</summary>
    public static class Commerce
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            // Product categories (running text and titles)
            Add("category.food", "food", "корм");
            Add("category.toy", "toys", "іграшки");
            Add("category.accessory", "accessories", "аксесуари");
            Add("category.medicine", "medicine", "ліки");
            Add("category.title.food", "Food", "Корм");
            Add("category.title.toy", "Toy", "Іграшки");
            Add("category.title.accessory", "Accessory", "Аксесуари");
            Add("category.title.medicine", "Medicine", "Ліки");

            // Products (ids from ItemDatabase.CreateDefault)
            Add("item.cat_food.name", "Cat Food", "Корм для котів");
            Add("item.dog_food.name", "Dog Food", "Корм для собак");
            Add("item.rabbit_pellets.name", "Rabbit Pellets", "Гранули для кроликів");
            Add("item.fish_flakes.name", "Fish Flakes", "Пластівці для риб");
            Add("item.dog_toy.name", "Squeaky Bone", "Писклива кістка");
            Add("item.cat_wand.name", "Feather Wand", "Вудочка з пірʼям");
            Add("item.chew_ball.name", "Chew Ball", "Жувальний мʼяч");
            Add("item.collar.name", "Leather Collar", "Шкіряний нашийник");
            Add("item.leash.name", "Nylon Leash", "Нейлоновий повідець");
            Add("item.water_bowl.name", "Water Bowl", "Миска для води");
            Add("item.bird_perch.name", "Bird Perch", "Жердинка для птахів");
            Add("item.flea_drops.name", "Flea Drops", "Краплі від бліх");
            Add("item.vitamins.name", "Pet Vitamins", "Вітаміни для тварин");
            Add("item.wound_gel.name", "Wound Gel", "Гель для ран");

            // Shelves and deliveries
            Add("shelf.describe.empty", "{0} shelf — empty", "Полиця «{0}» — порожня");
            Add("shelf.describe.title", "{0} shelf", "Полиця «{0}»");
            Add("crate.prompt.furniture", "[{0}]  Unpack the {1} into your furniture inventory", "[{0}]  Розпакувати «{1}» до запасу меблів");
            Add("crate.prompt.stock", "[{0}]  Collect {1} {2} units from the delivery", "[{0}]  Забрати з доставки {1} од. ({2})");

            // Assistants
            Add("assistant.no_counter", "Your cashiers have no counter to serve from — place a counter to open the till again.", "Вашим касирам немає за яким прилавком обслуговувати — поставте прилавок, щоб знову відкрити касу.");
            Add("assistant.served", "{0} served {1} — €{2:N2}", "{0} обслуговує {1} — €{2:N2}");
            Add("assistant.shelved", "{0} put {1} {2} on the shelf from the stockroom.", "{0} викладає на полицю {1} од. ({2}) зі складу.");
            Add("assistant.pen_unaffordable", "{0} could not afford €{1:N0} to look after the {2} pen.", "{0} не має €{1:N0} на догляд за вольєром «{2}».");
            Add("assistant.pen_serviced", "{0} fed and cleaned the {1} pen — €{2:N0}.", "{0} нагодував(-ла) і прибрав(-ла) вольєр «{1}» — €{2:N0}.");
            Add("autoreorder.ordered", "Auto-reorder: {0} {1} units ordered.", "Автозамовлення: замовлено {0} од. ({1}).");
            Add("autoreorder.cant_afford", "Auto-reorder: can't afford {0} without touching tonight's rent and wages.", "Автозамовлення: не вистачає на «{0}», не чіпаючи сьогоднішньої оренди та зарплат.");

            // Staff words
            Add("staff.role.cashier", "Cashier", "Касир");
            Add("staff.role.restocker", "Restocker", "Комірник");
            Add("staff.role.feeder", "Feeder", "Доглядач");
            Add("staff.skill.1", "green", "новачок");
            Add("staff.skill.2", "learning", "учень");
            Add("staff.skill.3", "capable", "тямущий");
            Add("staff.skill.4", "skilled", "вправний");
            Add("staff.skill.5", "expert", "експерт");
            Add("staff.speed.very_quick", "very quick", "дуже швидко");
            Add("staff.speed.quick", "quick", "швидко");
            Add("staff.speed.steady", "steady", "рівно");
            Add("staff.speed.slow", "slow", "повільно");

            // Customers
            Add("customer.type.regular", "Regular", "Звичайний");
            Add("customer.type.bargain_hunter", "Bargain hunter", "Мисливець за знижками");
            Add("customer.type.rare_collector", "Collector", "Колекціонер");
            Add("customer.type.parent_with_child", "Parent", "Батько з дитиною");
            Add("customer.wants_pet", "wants a pet", "хоче тваринку");
            Add("customer.bubble", "<size=80%><b>{0}</b>: {1}</size>", "<size=80%><b>{0}</b>: {1}</size>");
            Add("customer.waiting_to_pay", "waiting to pay", "чекає на оплату");
            Add("customer.gave_up", "gave up!", "набридло!");
            // {0} reputation lost, e.g. 3.5.
            Add("customer.rep_loss", "−{0:0.#} rep", "−{0:0.#} репутації");
            Add("customer.gave_up_notice", "{0} gave up waiting and walked out.", "{0} не дочекався(-лася) і пішов(-ла).");
            Add("customer.got", "got {0}", "взяв(-ла): {0}");
            Add("customer.too_expensive", "too expensive", "задорого");
            Add("customer.buying_pet", "buying a {0}", "купує: {0}");
            Add("customer.pet_too_pricey", "{0}? too pricey", "{0}? задорого");
            Add("customer.thanks", "thanks!", "дякую!");
            Add("customer.not_trading", "Place a counter and a shelf or pen to open for customers.", "Поставте прилавок і полицю або вольєр, щоб відкритися для покупців.");
            Add("customer.sold.one", "Sold {0} item for €{1:N2}", "Продано {0} товар за €{1:N2}");
            Add("customer.sold.other", "Sold {0} items for €{1:N2}", "Продано {0} товарів за €{1:N2}");
            uk["customer.sold.few"]  = "Продано {0} товари за €{1:N2}";
            uk["customer.sold.many"] = "Продано {0} товарів за €{1:N2}";

            // Shop events
            Add("event.name.supplier_sale", "Supplier sale", "Розпродаж у постачальника");
            Add("event.name.heatwave", "Heatwave", "Спека");
            Add("event.name.street_festival", "Street festival", "Вуличний фестиваль");
            Add("event.name.none", "No event", "Без подій");
            Add("event.announce.supplier_sale", "Supplier sale today — wholesale orders cost 25% less!", "Сьогодні розпродаж у постачальника — оптові замовлення на 25% дешевші!");
            Add("event.announce.heatwave", "Heatwave! Pens will need feed and bedding topped up more often.", "Спека! У вольєрах частіше закінчуватимуться корм і підстилка.");
            Add("event.announce.street_festival", "Street festival outside — expect a busy day!", "На вулиці фестиваль — буде гарячий день!");
            Add("event.ended", "{0} ended", "{0}: завершено");
            Add("event.days_left.one", "{1}: {0} day left", "{1}: залишився {0} день");
            Add("event.days_left.other", "{1}: {0} days left", "{1}: залишилося {0} днів");
            uk["event.days_left.few"]  = "{1}: залишилося {0} дні";
            uk["event.days_left.many"] = "{1}: залишилося {0} днів";
        }
    }
}
