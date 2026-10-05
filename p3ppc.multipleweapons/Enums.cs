using System;

namespace p3ppc.multipleweapons
{
    public class Enums
    {

        public enum EquipmentType
        {
            Weapon = 0,
            Body = 1,
            Feet = 2,
            Accessory = 3,
            Outfit = 4,
        }

        public enum PartyMember
        {
            MaleProtag = 1,
            Yukari = 2,
            Aigis = 3,
            Mitsuru = 4,
            Junpei = 5,
            Fuuka = 6,
            Akihiko = 7,
            Ken = 8,
            Shinjiro = 9,
            Koromaru = 10,
            FemaleProtag = 99,
        }

        public enum WeaponPack : byte
        {
            OneHandedSwordOrRapier = 0,
            TwoHandedSword = 1,
            Axe = 2,
            Bow = 3,
            NaginataOrSpear = 4,
            Fists = 5,
            KoromaruKnife = 6,
            None = 0xFF,
        }
    }
}
