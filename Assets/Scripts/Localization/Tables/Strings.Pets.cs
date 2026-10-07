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
        }
    }
}
