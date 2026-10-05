using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using IReloadedHooks = Reloaded.Hooks.Definitions.IReloadedHooks;
using static p3ppc.multipleweapons.Enums;

namespace p3ppc.multipleweapons
{

    internal unsafe class PoliceStationWeaponPatch
    {
        private IHook<BuildShopListDelegate> _buildShopListHook = null!;
        private IHook<CanMemberEquipItemDelegate> _canMemberEquipItemHook = null!;

        private IsPartyMemberAvailableDelegate _isPartyMemberAvailable = null!;
        private IsFemcDelegate _isFemc = null!;

        private WeaponData.ItemTblEntry** _itemEntries;

        private int _protagonistShopBuildDepth;

        internal void Hook(IReloadedHooks hooks)
        {

            Utils.SigScan("48 89 05 ?? ?? ?? ?? 44 89 C2", "Police Shop Item Table", address =>
            {
                _itemEntries = (WeaponData.ItemTblEntry**)Utils.GetGlobalAddress(address + 3);
            });

            Utils.SigScan("48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " + "0F B6 05 ?? ?? ?? ?? C1 E8 07", "Police Station Protagonist Check", address =>
            {
                _isFemc = hooks.CreateWrapper<IsFemcDelegate>(address, out _);
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 0F B7 F9 " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 8D 47 ??", "Party Member Availability", address =>
            {
                _isPartyMemberAvailable = hooks.CreateWrapper<IsPartyMemberAvailableDelegate>(address, out _);
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 0F B7 F9 0F B7 DA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? F6 05 ?? ?? ?? ?? 80", "Equipment Compatibility", address =>
            {
                _canMemberEquipItemHook = hooks.CreateHook<CanMemberEquipItemDelegate>(OnCanMemberEquipItem, address).Activate();
            });

            Utils.SigScan("40 56 57 41 54 41 55 41 56 48 83 EC 50", "Police Shop List", address =>
            {
                _buildShopListHook = hooks.CreateHook<BuildShopListDelegate>(OnBuildShopList, address).Activate();
            });
        }

        private nuint OnBuildShopList(nuint outputList, int shopCategory, ulong packedSelection)
        {
            ushort selectedMember = (ushort)(packedSelection & 0xFFFF);

            ushort selectedEquipmentTab = (ushort)((packedSelection >> 16) & 0x7FFF);

            bool internalProbeBuild = (packedSelection & 0x80000000UL) != 0;

            bool protagonistWeaponBuyList = !internalProbeBuild && selectedMember == (ushort)PartyMember.MaleProtag && selectedEquipmentTab == 0;

            if (protagonistWeaponBuyList)
            {
                _protagonistShopBuildDepth++;

                Utils.LogDebug("Building the protagonist weapon list.");
            }

            try
            {

                return _buildShopListHook.OriginalFunction(outputList, shopCategory, packedSelection);
            }
            finally
            {
                if (protagonistWeaponBuyList)
                {
                    _protagonistShopBuildDepth--;
                }
            }
        }

        private byte OnCanMemberEquipItem(short item, PartyMember member)
        {
            byte vanilla = _canMemberEquipItemHook.OriginalFunction(item, member);

            if (_protagonistShopBuildDepth <= 0 || member != PartyMember.MaleProtag)
            {
                return vanilla;
            }

            if (item <= 0 || item >= 0x200 || _itemEntries == null || *_itemEntries == null)
            {
                return vanilla;
            }

            WeaponData.WeaponIcon icon = (*_itemEntries)[item].Icon;

            bool femc = _isFemc != null && _isFemc() != 0;

            if (vanilla == 0)
            {
                return 0;
            }

            switch (icon)
            {

                case WeaponData.WeaponIcon.OneHSwordIcon:
                case WeaponData.WeaponIcon.RapierIcon:

                    return femc
                        ? OwnerAvailable(PartyMember.Mitsuru)
                        : (byte)1;

                case WeaponData.WeaponIcon.SpearIcon:
                case WeaponData.WeaponIcon.NaginataIcon:

                    return femc
                        ? (byte)1
                        : OwnerAvailable(PartyMember.Ken);

                case WeaponData.WeaponIcon.TwoHSwordIcon:
                    return OwnerAvailable(PartyMember.Junpei);

                case WeaponData.WeaponIcon.BowIcon:
                    return OwnerAvailable(PartyMember.Yukari);

                case WeaponData.WeaponIcon.AxeIcon:
                    return OwnerAvailable(PartyMember.Shinjiro);

                case WeaponData.WeaponIcon.FistsIcon:
                    return OwnerAvailable(PartyMember.Akihiko);

                case WeaponData.WeaponIcon.KnifeIcon:
                    return OwnerAvailable(PartyMember.Koromaru);

                case WeaponData.WeaponIcon.GunIcon:
                    return 0;

                default:
                    return vanilla;
            }
        }

        private byte OwnerAvailable(PartyMember owner)
        {
            if (_isPartyMemberAvailable == null)
            {
                return 0;
            }

            return _isPartyMemberAvailable((short)owner) != 0 ? (byte)1 : (byte)0;
        }

        [Function(CallingConventions.Microsoft)]
        private delegate nuint BuildShopListDelegate(nuint outputList, int shopCategory, ulong packedSelection);

        [Function(CallingConventions.Microsoft)]
        private delegate byte CanMemberEquipItemDelegate(short item, PartyMember member);

        [Function(CallingConventions.Microsoft)]
        private delegate nuint IsPartyMemberAvailableDelegate(short member);

        [Function(CallingConventions.Microsoft)]
        private delegate byte IsFemcDelegate();
    }
}
