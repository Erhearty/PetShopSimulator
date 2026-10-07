using System.Collections.Generic;

namespace PetShop.Localization.Strings
{
    /// <summary>Pets: species, coats, pens, breeding and shows.</summary>
    public static class Pets
    {
        /// <summary>Adds this table's English and Ukrainian text.</summary>
        public static void Register(Dictionary<string, string> en, Dictionary<string, string> uk)
        {
            void Add(string key, string e, string u) { en[key] = e; uk[key] = u; }

            Add("species.cat", "Cat", "Кіт");
            Add("species.dog", "Dog", "Пес");
            Add("species.fox", "Fox", "Лис");
            Add("species.chicken", "Chicken", "Курка");
            Add("species.penguin", "Penguin", "Пінгвін");
            Add("species.deer", "Deer", "Олень");
            Add("species.horse", "Horse", "Кінь");
            Add("species.tiger", "Tiger", "Тигр");
            Add("species.rabbit", "Rabbit", "Кролик");
            Add("species.hamster", "Hamster", "Хом’як");
            Add("species.parrot", "Parrot", "Папуга");
            Add("species.fish", "Fish", "Рибка");

            Add("rarity.common", "Common", "Звичайний");
            Add("rarity.uncommon", "Uncommon", "Незвичайний");
            Add("rarity.rare", "Rare", "Рідкісний");
            Add("rarity.legendary", "Legendary", "Легендарний");

            // Coat colours: display only; CoatColours keeps the English names as identifiers.
            Add("coat.white", "White", "Білий");
            Add("coat.cream", "Cream", "Кремовий");
            Add("coat.golden", "Golden", "Золотистий");
            Add("coat.ginger", "Ginger", "Рудий");
            Add("coat.brown", "Brown", "Коричневий");
            Add("coat.chocolate", "Chocolate", "Шоколадний");
            Add("coat.grey", "Grey", "Сірий");
            Add("coat.black", "Black", "Чорний");
            Add("coat.red", "Red", "Червоний");
            Add("coat.blue", "Blue", "Синій");
            Add("coat.green", "Green", "Зелений");
            Add("coat.yellow", "Yellow", "Жовтий");

            Add("growth.baby", "Baby", "маля");
            Add("growth.juvenile", "Juvenile", "підліток");
            Add("growth.adult", "Adult", "доросла особина");

            // {0} rarity (upper case), {1} growth stage, {2} species, {3} " (name)" or empty.
            Add("pet.display_name", "[{0}] {1} {2}{3}", "[{0}] {2}, {1}{3}");

            Add("pet.condition.thriving", "thriving", "процвітає");
            Add("pet.condition.well", "well", "добре");
            Add("pet.condition.poorly", "poorly", "нездужає");
            Add("pet.condition.suffering", "suffering", "страждає");

            // Breeding
            Add("breed.pick_two_adults", "Pick two adults of the same species.", "Виберіть двох дорослих тварин одного виду.");
            Add("breed.pick_different", "Pick two different animals.", "Виберіть дві різні тварини.");
            Add("breed.same_species", "They have to be the same species.", "Тварини мають бути одного виду.");
            Add("breed.both_adult", "Both parents have to be fully grown.", "Обоє батьків мають бути дорослими.");
            Add("breed.expected",
                "Expected: a baby {0}, {1} or better ({2:0}% chance of a step up; {3:0}% when both share a tier)\n" +
                "temperament {4:0.00}   ·   energy {5:0.00}   ·   friendliness {6:0.00}\n" +
                "list price around € {7:N0} before rarity and growth",
                "Очікується: маля виду «{0}», рідкість «{1}» або вища (шанс підвищення {2:0}%; {3:0}%, якщо в батьків однакова рідкість)\n" +
                "темперамент {4:0.00}   ·   енергійність {5:0.00}   ·   дружелюбність {6:0.00}\n" +
                "ціна близько € {7:N0} без урахування рідкості та віку");

            // Pens: the panel description and the gate sign.
            Add("pen.describe.empty", "{0} pen — empty", "Вольєр «{0}» — порожній");
            Add("pen.describe.header", "{0} pen  ({1}/{2})", "Вольєр «{0}»  ({1}/{2})");
            Add("pen.describe.upkeep", "  Feed {0:0}%   ·   Bedding {1:0}%", "  Корм {0:0}%   ·   Підстилка {1:0}%");
            Add("pen.describe.service", "   ·   servicing costs €{0:N2}", "   ·   догляд коштує €{0:N2}");
            Add("pen.describe.row", "  {0,-34}{1,-11}€{2:0.00}", "  {0,-34}{1,-11}€{2:0.00}");
            Add("pen.describe.may_breed", "  Two adults — they may breed tonight.", "  Дві дорослі тварини — уночі може з’явитися потомство.");
            Add("pen.describe.needs_adults", "  Needs two adults to breed.", "  Для розведення потрібні дві дорослі тварини.");
            Add("pen.describe.full", "  Pen is full.", "  Вольєр заповнений.");
            Add("pen.sign.empty", "{0} pen\n<size=75%>empty</size>", "Вольєр «{0}»\n<size=75%>порожній</size>");
            // {0} species, {1} asking price, {2} residents, {3} capacity, {4} rarity, {5} pen.sign.paired or empty.
            Add("pen.sign.stocked", "{0}   € {1:0.00}\n<size=75%>{2} / {3}  ·  {4}{5}</size>",
                                    "{0}   € {1:0.00}\n<size=75%>{2} / {3}  ·  {4}{5}</size>");
            Add("pen.sign.paired", "  ·  paired tonight", "  ·  пара на ніч");

            // Progression tiers and milestones
            Add("tier.corner_shop", "Corner Shop", "Крамничка за рогом");
            Add("tier.local_favourite", "Local Favourite", "Улюбленець району");
            Add("tier.trusted_name", "Trusted Name", "Надійна марка");
            Add("tier.town_landmark", "Town Landmark", "Окраса міста");
            Add("milestone.headline", "Milestone: {0} — {1}", "Новий рівень: {0} — {1}");
            Add("milestone.unlock.tigers", "tiger pens and the whole yard are unlocked!", "відкрито вольєри для тигрів і все подвір’я!");
            // {0} the build-mode key that cycles the pen species.
            Add("milestone.unlock.horses", "horse pens are now available ({0} in build mode)!", "тепер доступні вольєри для коней ({0} у режимі будівництва)!");
            Add("milestone.unlock.back_strip", "the back of the lot is yours to build on!", "тепер можна забудовувати задню частину ділянки!");

            // Inspector: {0} grade letter, {1} reputation change, {2} signed cash text.
            Add("inspection.summary", "Inspector's visit: grade {0} — reputation {1:+0;-0}, cash {2}",
                                      "Візит інспектора: оцінка {0} — репутація {1:+0;-0}, гроші {2}");
        }
    }
}
