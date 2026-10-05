using System;
using System.Runtime.InteropServices;
using static p3ppc.multipleweapons.Enums;

namespace p3ppc.multipleweapons
{
    internal static class WeaponData
    {

        [Flags]
        internal enum WeaponIcon : ushort
        {
            DummyIcon = 0,
            TwoHSwordIcon = 1,
            OneHSwordIcon = 2,
            BowIcon = 4,
            SpearIcon = 8,
            AxeIcon = 16,
            FistsIcon = 32,
            GunIcon = 64,
            KnifeIcon = 128,
            NaginataIcon = 256,
            RapierIcon = 512,
        }

        [StructLayout(LayoutKind.Explicit, Size = 56)]
        internal struct ItemTblEntry
        {
            [FieldOffset(4)]
            internal WeaponIcon Icon;
        }

        internal static WeaponPack GetWeaponPackForCharacter(PartyMember pathMember, ItemTblEntry entry)
        {

            switch (entry.Icon)
            {
                case WeaponIcon.TwoHSwordIcon:
                    return WeaponPack.TwoHandedSword;
                case WeaponIcon.OneHSwordIcon:
                case WeaponIcon.RapierIcon:
                    return WeaponPack.OneHandedSwordOrRapier;
                case WeaponIcon.BowIcon:
                    return WeaponPack.Bow;
                case WeaponIcon.SpearIcon:
                case WeaponIcon.NaginataIcon:
                    return WeaponPack.NaginataOrSpear;
                case WeaponIcon.AxeIcon:
                    return WeaponPack.Axe;
                case WeaponIcon.FistsIcon:
                    return WeaponPack.Fists;
                case WeaponIcon.GunIcon:

                    return WeaponPack.None;
                case WeaponIcon.KnifeIcon:
                    return WeaponPack.KoromaruKnife;
                default:
                    return WeaponPack.None;
            }
        }

        internal static bool CanFollowUp(WeaponPack pack)
        {

            return pack != WeaponPack.Bow;
        }


    }
}
