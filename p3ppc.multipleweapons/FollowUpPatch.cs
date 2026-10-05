using System;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using static p3ppc.multipleweapons.Enums;

namespace p3ppc.multipleweapons
{

    internal unsafe class FollowUpPatch
    {

        private const byte MitsuruStyle = 0;
        private const byte JunpeiStyle = 1;
        private const byte ShinjiroStyle = 2;
        private const byte AkihikoStyle = 3;
        private const byte YukariStyle = 4;
        private const byte AigisStyle = 5;
        private const byte KenStyle = 6;
        private const byte KoromaruStyle = 7;
        private const byte FemcStyle = 8;

        private const int ModelTblRecordSize = 0x114;
        private const int ModelTblAnimationTableOffset = 0x24;
        private const int ModelTblAnimationEntrySize = 10;
        private const int ModelTblAnimationCount = 24;
        private const int AttackTimingShadowSize = ModelTblAnimationEntrySize * ModelTblAnimationCount;

        private const int MakotoModelTblIndex = 1;
        private const int YukariModelTblIndex = 2;
        private const int AigisModelTblIndex = 3;
        private const int MitsuruModelTblIndex = 4;
        private const int JunpeiModelTblIndex = 5;
        private const int AkihikoModelTblIndex = 7;
        private const int KenModelTblIndex = 8;
        private const int ShinjiroModelTblIndex = 9;
        private const int KoromaruModelTblIndex = 10;
        private const int FemcModelTblIndex = 11;

        private const ushort AttackMotion = 4;
        private const ushort CriticalAttackMotion = 5;
        private const ushort AttackMotion6 = 6;
        private GetCharacterEquipmentDelegate _getCharacterEquipment = null!;
        private WeaponData.ItemTblEntry** _itemEntries;
        private IsFemcDelegate _isFemc = null!;
        private PartyMotionMapDelegate _partyMotionMap = null!;

        private IHook<GetAttackStyleDelegate> _attackStyleHook = null!;
        private IHook<RangedPositioningDelegate> _rangedPositioningHook = null!;
        private IHook<NormalAttackSetupDelegate> _normalAttackSetupHook = null!;

        private IHook<WeaponAttackOutcomeDelegate> _weaponAttackOutcomeHook = null!;
        private IHook<AttackCountForResultDelegate> _attackCountForResultHook = null!;
        private IHook<WeaponAttackHitCountDelegate> _weaponAttackHitCountHook = null!;
        private IHook<MissTripCheckDelegate> _missTripCheckHook = null!;
        private IsWeaponAttackSkillDelegate _isWeaponAttackSkill = null!;

        private IHook<BattleUnitInitDelegate> _battleUnitInitHook = null!;
        private IHook<AttackControlDelegate> _attackControlAHook = null!;
        private IHook<AttackControlDelegate> _attackControlBHook = null!;
        private IHook<MitsuruAttackPositionDelegate> _mitsuruAttackPositionHook = null!;

        private IHook<AttackCameraProfileDelegate> _attackCameraProfileHook = null!;
        private IHook<AttackCameraUpdateDelegate> _attackCameraUpdateHook = null!;

        private nuint _attackTimingShadow;

        private nuint _modelTblBase;

        private short _lastLoggedWeapon = -1;
        private short _lastLoggedTimingWeapon = -1;
        private short _lastLoggedCameraWeapon = -1;
        private short _lastLoggedBowWeapon = -1;

        private readonly struct OwnerProfile
        {
            internal readonly ushort MemberId;
            internal readonly int ModelTblIndex;
            internal readonly byte AttackStyle;
            internal readonly string Name;

            internal OwnerProfile(ushort memberId, int modelTblIndex, byte attackStyle, string name)
            {
                MemberId = memberId;
                ModelTblIndex = modelTblIndex;
                AttackStyle = attackStyle;
                Name = name;
            }
        }

        internal void Hook(IReloadedHooks hooks, IMemory memory)
        {
            _attackTimingShadow = (nuint)memory.Allocate(AttackTimingShadowSize);

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 0F B7 F9 48 0F BF DA", "GetCharacterEquipment (weapon-owner battle profile)", address =>
            {
                _getCharacterEquipment = hooks.CreateWrapper<GetCharacterEquipmentDelegate>(address, out _);
            });

            Utils.SigScan("48 89 05 ?? ?? ?? ?? 44 89 C2", "ItemTblEntries Ptr (weapon-owner battle profile)", address =>
            {
                _itemEntries = (WeaponData.ItemTblEntry**)Utils.GetGlobalAddress(address + 3);
            });

            Utils.SigScan("48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " + "0F B6 05 ?? ?? ?? ?? C1 E8 07", "Protagonist Route Check", address =>
            {
                _isFemc = hooks.CreateWrapper<IsFemcDelegate>(address, out _);
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CB 0F B7 FA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 0F B6 83 ?? ?? ?? ??", "Party Motion Mapper", address =>
            {
                _partyMotionMap = hooks.CreateWrapper<PartyMotionMapDelegate>(address, out _);
            });

            Utils.SigScan("40 53 48 83 EC 20 48 89 CB 48 8D 0D ?? ?? ?? ?? " + "E8 ?? ?? ?? ?? F6 03 04 0F 85 ?? ?? ?? ??", "Attack Style Classifier", address =>
            {
                _attackStyleHook = hooks.CreateHook<GetAttackStyleDelegate>(OnGetAttackStyle, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 " + "48 8B F9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 4F ??", "Normal Attack Setup", address =>
            {
                _normalAttackSetupHook = hooks.CreateHook<NormalAttackSetupDelegate>(OnNormalAttackSetup, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CF " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 4F ??", "Ranged Positioning", address =>
            {
                _rangedPositioningHook = hooks.CreateHook<RangedPositioningDelegate>(OnRangedPositioning, address).Activate();
            });

            Utils.SigScan("40 53 48 83 EC 20 0F B7 D9 48 8D 0D ?? ?? ?? ?? " + "E8 ?? ?? ?? ?? B8 D0 01 00 00 66 39 C3", "Weapon Attack Skill Check", address =>
            {
                _isWeaponAttackSkill = hooks.CreateWrapper<IsWeaponAttackSkillDelegate>(address, out _);
            });

            Utils.SigScan("40 53 55 57 41 56 48 83 EC 48", "Weapon Attack Outcome", address =>
            {
                _weaponAttackOutcomeHook = hooks.CreateHook<WeaponAttackOutcomeDelegate>(OnWeaponAttackOutcome, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 8B F9 0F B7 DA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? F6 07 04", "Attack Result Count", address =>
            {
                _attackCountForResultHook = hooks.CreateHook<AttackCountForResultDelegate>(OnAttackCountForResult, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 " + "48 8B F9 41 0F B7 D8", "Weapon Attack Hit Count", address =>
            {
                _weaponAttackHitCountHook = hooks.CreateHook<WeaponAttackHitCountDelegate>(OnWeaponAttackHitCount, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? " + "57 48 83 EC 20 48 89 CF 41 0F B7 D9", "Bow Miss Check", address =>
            {
                _missTripCheckHook = hooks.CreateHook<MissTripCheckDelegate>(OnMissTripCheck, address).Activate();
                
                Utils.LogDebug("Bow no-trip hook enabled.");
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CB 48 63 FA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 8B ?? ?? ?? ??", "Battle Model Table Init", address =>
            {
                _battleUnitInitHook = hooks.CreateHook<BattleUnitInitDelegate>(OnBattleUnitInit, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CB 0F B7 FA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 D7", "Attack Control A", address =>
            {
                _attackControlAHook = hooks.CreateHook<AttackControlDelegate>(OnAttackControlA, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CF 0F B7 DA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 D3", "Attack Control B", address =>
            {
                _attackControlBHook = hooks.CreateHook<AttackControlDelegate>(OnAttackControlB, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 89 CB 0F B7 FA " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? 01", "Mitsuru Attack Positioning", address =>
            {
                _mitsuruAttackPositionHook = hooks.CreateHook<MitsuruAttackPositionDelegate>(OnMitsuruAttackPosition, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 " + "48 89 CE 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 96 ?? ?? ?? ??", "Attack Camera Profile", address =>
            {
                _attackCameraProfileHook = hooks.CreateHook<AttackCameraProfileDelegate>(OnAttackCameraProfile, address).Activate();
            });

            Utils.SigScan("40 53 48 83 EC 30 48 8B D9 48 8D 0D ?? ?? ?? ?? " + "E8 ?? ?? ?? ?? 48 8B 83 ?? ?? ?? ?? 48 85 C0", "Attack Camera Update", address =>
            {
                _attackCameraUpdateHook = hooks.CreateHook<AttackCameraUpdateDelegate>(OnAttackCameraUpdate, address).Activate();
            });

            Utils.LogDebug("Weapon-owner battle profiles enabled.");
        }

        private bool TryGetOwnerProfile(out short weaponItem, out WeaponData.ItemTblEntry entry, out OwnerProfile profile)
        {
            weaponItem = 0;
            entry = default;
            profile = default;

            if (_getCharacterEquipment == null || _itemEntries == null || *_itemEntries == null)
            {
                return false;
            }

            weaponItem = _getCharacterEquipment(PartyMember.MaleProtag, EquipmentType.Weapon);

            if (weaponItem <= 0 || weaponItem >= 512)
            {
                return false;
            }

            entry = (*_itemEntries)[weaponItem];

            bool femcActive = _isFemc != null && _isFemc() != 0;

            switch (entry.Icon)
            {

                case WeaponData.WeaponIcon.OneHSwordIcon:
                case WeaponData.WeaponIcon.RapierIcon:
                    if (!femcActive)
                    {
                        return false;
                    }

                    profile = new OwnerProfile((ushort)PartyMember.Mitsuru, MitsuruModelTblIndex, MitsuruStyle, "Mitsuru/1H Sword-Rapier (FemC only)");
                    return true;

                case WeaponData.WeaponIcon.TwoHSwordIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Junpei, JunpeiModelTblIndex, JunpeiStyle, "Junpei/2H Sword");
                    return true;

                case WeaponData.WeaponIcon.AxeIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Shinjiro, ShinjiroModelTblIndex, ShinjiroStyle, "Shinjiro/Axe");
                    return true;

                case WeaponData.WeaponIcon.FistsIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Akihiko, AkihikoModelTblIndex, AkihikoStyle, "Akihiko/Fists");
                    return true;

                case WeaponData.WeaponIcon.BowIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Yukari, YukariModelTblIndex, YukariStyle, "Yukari/Bow");
                    return true;

                case WeaponData.WeaponIcon.GunIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Aigis, AigisModelTblIndex, AigisStyle, "Aigis/Gun");
                    return true;

                case WeaponData.WeaponIcon.SpearIcon:
                    if (femcActive)
                    {
                        return false;
                    }

                    profile = new OwnerProfile((ushort)PartyMember.Ken, KenModelTblIndex, KenStyle, "Ken/Spear (Makoto only)");
                    return true;

                case WeaponData.WeaponIcon.KnifeIcon:
                    profile = new OwnerProfile((ushort)PartyMember.Koromaru, KoromaruModelTblIndex, KoromaruStyle, "Koromaru/Knife");
                    return true;

                case WeaponData.WeaponIcon.NaginataIcon:
                    if (femcActive)
                    {

                        return false;
                    }

                    profile = new OwnerProfile((ushort)PartyMember.Ken, KenModelTblIndex, KenStyle, "Ken/Naginata-Spear (Makoto only)");
                    return true;

                default:
                    return false;
            }
        }

        private byte OnGetAttackStyle(nuint partyMemberInfo)
        {
            if (!IsRealProtagonistInfo(partyMemberInfo))
            {
                return _attackStyleHook.OriginalFunction(partyMemberInfo);
            }

            if (!TryGetOwnerProfile(out short weaponItem, out WeaponData.ItemTblEntry entry, out OwnerProfile profile))
            {
                return _attackStyleHook.OriginalFunction(partyMemberInfo);
            }

            if (weaponItem != _lastLoggedWeapon)
            {
                _lastLoggedWeapon = weaponItem;

                Utils.LogDebug($"Attack profile: item={weaponItem}, owner={profile.Name}.");
            }

            return profile.AttackStyle;
        }

        private void OnNormalAttackSetup(nuint battleAction)
        {

            if (TryTemporarilySpoofActionActorOwner(battleAction, out nuint actor, out ushort savedMember))
            {
                try
                {
                    _normalAttackSetupHook.OriginalFunction(battleAction);
                }
                finally
                {
                    *(ushort*)(actor + 0xA4) = savedMember;
                }
            }
            else
            {
                _normalAttackSetupHook.OriginalFunction(battleAction);
            }

        }

        private uint OnRangedPositioning(nuint battleAction)
        {
            if (!TryGetRealProtagonistActorFromAction(battleAction, out _))
            {
                return _rangedPositioningHook.OriginalFunction(battleAction);
            }

            if (!TryGetOwnerProfile(out short weaponItem, out _, out OwnerProfile profile) || profile.MemberId != (ushort)PartyMember.Yukari)
            {
                return _rangedPositioningHook.OriginalFunction(battleAction);
            }

            if (_lastLoggedBowWeapon != weaponItem)
            {
                _lastLoggedBowWeapon = weaponItem;

                Utils.LogDebug($"Bow {weaponItem} uses Yukari's ranged path.");
            }

            return 1;
        }

        private ulong OnWeaponAttackOutcome(nuint attackerInfo, nuint targetInfo, ushort skillId, short param4)
        {
            ulong result = _weaponAttackOutcomeHook.OriginalFunction(attackerInfo, targetInfo, skillId, param4);

            if (result != 8 || !IsProtagonistBow(attackerInfo))
            {
                return result;
            }

            if (_isWeaponAttackSkill == null || _isWeaponAttackSkill(skillId) == 0)
            {
                return result;
            }

            Utils.LogDebug("Bow basic attack forced to one hit.");

            return 1;
        }

        private byte OnAttackCountForResult(nuint partyMemberInfo, ushort attackResult)
        {
            if (IsProtagonistBow(partyMemberInfo))
            {

                return 1;
            }

            return _attackCountForResultHook.OriginalFunction(partyMemberInfo, attackResult);
        }

        private byte OnWeaponAttackHitCount(nuint partyMemberInfo, ushort skillId, ushort attackResult)
        {
            if (IsProtagonistBow(partyMemberInfo) && _isWeaponAttackSkill != null && _isWeaponAttackSkill(skillId) != 0)
            {

                return 1;
            }

            return _weaponAttackHitCountHook.OriginalFunction(partyMemberInfo, skillId, attackResult);
        }

        private bool IsProtagonistBow(nuint partyMemberInfo)
        {
            if (!IsRealProtagonistInfo(partyMemberInfo))
            {
                return false;
            }

            return TryGetOwnerProfile(out _, out _, out OwnerProfile profile) &&
                   profile.MemberId == (ushort)PartyMember.Yukari;
        }

        private ulong OnMissTripCheck(nuint attackerInfo, nuint resultData, ushort skillId, short param4)
        {
            if (IsProtagonistBow(attackerInfo))
            {return 0;
            }

            return _missTripCheckHook.OriginalFunction(attackerInfo, resultData, skillId, param4);
        }

        private void OnBattleUnitInit(nuint actor, uint memberId)
        {
            _battleUnitInitHook.OriginalFunction(actor, memberId);

            if (actor == 0 || *(byte*)(actor + 0xA2) != 0 || *(ushort*)(actor + 0xA4) != (ushort)PartyMember.MaleProtag)
            {
                return;
            }

            ApplyOwnerAttackTiming(actor);
        }

        private void ApplyOwnerAttackTiming(nuint actor)
        {
            if (_attackTimingShadow == 0 || _isFemc == null || _partyMotionMap == null || !TryGetOwnerProfile(out short weaponItem, out _, out OwnerProfile profile))
            {
                return;
            }

            nuint currentTiming = *(nuint*)(actor + 0xC48);

            if (currentTiming == 0)
            {
                return;
            }

            int currentRecord = _isFemc() != 0 ? FemcModelTblIndex : MakotoModelTblIndex;

            _modelTblBase = currentTiming - (nuint)( currentRecord * ModelTblRecordSize + ModelTblAnimationTableOffset);

            nuint ownerTiming = _modelTblBase + (nuint)( profile.ModelTblIndex * ModelTblRecordSize + ModelTblAnimationTableOffset);

            Buffer.MemoryCopy((void*)currentTiming, (void*)_attackTimingShadow, AttackTimingShadowSize, AttackTimingShadowSize);

            for (ushort logical = AttackMotion; logical <= AttackMotion6; logical++)
            {
                byte rawMotion = (byte)_partyMotionMap(actor, logical);

                if (rawMotion >= ModelTblAnimationCount)
                {
                    continue;
                }

                nuint source = ownerTiming + (nuint)(rawMotion * ModelTblAnimationEntrySize);

                nuint destination = _attackTimingShadow + (nuint)(rawMotion * ModelTblAnimationEntrySize);

                Buffer.MemoryCopy((void*)source, (void*)destination, ModelTblAnimationEntrySize, ModelTblAnimationEntrySize);
            }

            *(nuint*)(actor + 0xC48) = _attackTimingShadow;

            *(ushort*)(actor + 0xC2C) = ModelTblAnimationCount;

            if (_lastLoggedTimingWeapon != weaponItem)
            {
                _lastLoggedTimingWeapon = weaponItem;

                Utils.LogDebug($"Copied {profile.Name} attack timing for motions 4, 5, and 6.");
            }
        }

        private ushort OnAttackControlA(nuint actor, ushort logicalMotion)
        {
            if (!IsAttackLogicalMotion(logicalMotion) || !IsRealProtagonistActor(actor) || _modelTblBase == 0 || !TryGetOwnerProfile(out _, out _, out OwnerProfile profile))
            {
                return _attackControlAHook.OriginalFunction(actor, logicalMotion);
            }

            if (profile.MemberId == (ushort)PartyMember.Mitsuru)
            {

                ushort savedMember = *(ushort*)(actor + 0xA4);
                *(ushort*)(actor + 0xA4) = (ushort)PartyMember.Mitsuru;

                try
                {
                    return _attackControlAHook.OriginalFunction(actor, logicalMotion);
                }
                finally
                {
                    *(ushort*)(actor + 0xA4) = savedMember;
                }
            }

            int subIndex = logicalMotion - AttackMotion;

            return *(ushort*)(_modelTblBase + (nuint)(profile.ModelTblIndex * ModelTblRecordSize + 0x18 + subIndex * 4));
        }

        private ushort OnAttackControlB(nuint actor, ushort logicalMotion)
        {
            if (!IsAttackLogicalMotion(logicalMotion) || !IsRealProtagonistActor(actor) || _modelTblBase == 0 || !TryGetOwnerProfile(out _, out _, out OwnerProfile profile))
            {
                return _attackControlBHook.OriginalFunction(actor, logicalMotion);
            }

            int subIndex = logicalMotion - AttackMotion;

            return *(ushort*)(_modelTblBase + (nuint)(profile.ModelTblIndex * ModelTblRecordSize + 0x1A + subIndex * 4));
        }

        private nuint OnMitsuruAttackPosition(nuint actor, ushort attackIndex)
        {
            if (IsRealProtagonistActor(actor) && TryGetOwnerProfile(out _, out _, out OwnerProfile profile) && profile.MemberId == (ushort)PartyMember.Mitsuru)
            {

                ushort savedMember = *(ushort*)(actor + 0xA4);
                *(ushort*)(actor + 0xA4) = (ushort)PartyMember.Mitsuru;

                try
                {
                    return _mitsuruAttackPositionHook.OriginalFunction(actor, attackIndex);
                }
                finally
                {
                    *(ushort*)(actor + 0xA4) = savedMember;
                }
            }

            return _mitsuruAttackPositionHook.OriginalFunction(actor, attackIndex);
        }

        private void OnAttackCameraProfile(nuint cameraTask, nuint param2, nuint param3)
        {
            if (TryTemporarilySpoofCameraOwner(cameraTask, out nuint actor, out ushort savedMember, out short weaponItem, out OwnerProfile profile))
            {
                try
                {
                    _attackCameraProfileHook.OriginalFunction(cameraTask, param2, param3);
                }
                finally
                {
                    *(ushort*)(actor + 0xA4) = savedMember;
                }

                LogCameraSpoof( weaponItem, profile);

                return;
            }

            _attackCameraProfileHook.OriginalFunction(cameraTask, param2, param3);
        }

        private void OnAttackCameraUpdate(nuint cameraTask)
        {
            if (TryTemporarilySpoofCameraOwner(cameraTask, out nuint actor, out ushort savedMember, out short weaponItem, out OwnerProfile profile))
            {
                try
                {
                    _attackCameraUpdateHook.OriginalFunction(cameraTask);
                }
                finally
                {
                    *(ushort*)(actor + 0xA4) = savedMember;
                }

                LogCameraSpoof( weaponItem, profile);

                return;
            }

            _attackCameraUpdateHook.OriginalFunction(cameraTask);
        }

        private void LogCameraSpoof(short weaponItem, OwnerProfile profile)
        {
            if (_lastLoggedCameraWeapon == weaponItem)
            {
                return;
            }

            _lastLoggedCameraWeapon = weaponItem;

            Utils.LogDebug($"Attack camera: item={weaponItem}, owner={profile.Name}.");
        }

        private bool TryTemporarilySpoofActionActorOwner(nuint battleAction, out nuint actor, out ushort savedMember)
        {
            actor = 0;
            savedMember = 0;

            if (!TryGetRealProtagonistActorFromAction(battleAction, out actor))
            {
                return false;
            }

            if (!TryGetOwnerProfile(out _, out _, out OwnerProfile profile))
            {
                return false;
            }

            if (profile.MemberId == (ushort)PartyMember.MaleProtag)
            {
                return false;
            }

            savedMember = *(ushort*)(actor + 0xA4);

            *(ushort*)(actor + 0xA4) = profile.MemberId;

            return true;
        }

        private bool TryTemporarilySpoofCameraOwner(nuint cameraTask, out nuint actor, out ushort savedMember, out short weaponItem, out OwnerProfile profile)
        {
            actor = 0;
            savedMember = 0;
            weaponItem = 0;
            profile = default;

            if (cameraTask == 0)
            {
                return false;
            }

            nuint action = *(nuint*)(cameraTask + 0xE8);

            if (!TryGetRealProtagonistActorFromAction(action, out actor))
            {
                return false;
            }

            if (!TryGetOwnerProfile(out weaponItem, out _, out profile))
            {
                return false;
            }

            if (profile.MemberId == (ushort)PartyMember.MaleProtag)
            {
                return false;
            }

            savedMember = *(ushort*)(actor + 0xA4);

            *(ushort*)(actor + 0xA4) = profile.MemberId;

            return true;
        }

        private static bool IsAttackLogicalMotion(ushort logicalMotion)
        {
            return logicalMotion >= AttackMotion && logicalMotion <= AttackMotion6;
        }

        private static bool IsRealProtagonistInfo(nuint partyMemberInfo)
        {
            return partyMemberInfo != 0 && *(ushort*)(partyMemberInfo + 0x02) == (ushort)PartyMember.MaleProtag;
        }

        private static bool IsRealProtagonistActor(nuint actor)
        {
            if (actor == 0 || *(byte*)(actor + 0xA2) != 0)
            {
                return false;
            }

            nuint info = *(nuint*)(actor + 0xCE0);

            return IsRealProtagonistInfo(info);
        }

        private static bool TryGetRealProtagonistActorFromAction(nuint battleAction, out nuint actor)
        {
            actor = 0;

            if (battleAction == 0)
            {
                return false;
            }

            actor = *(nuint*)(battleAction + 0x38);

            return IsRealProtagonistActor(actor);
        }

        [Function(CallingConventions.Microsoft)]
        private delegate ulong WeaponAttackOutcomeDelegate(nuint attackerInfo, nuint targetInfo, ushort skillId, short param4);

        [Function(CallingConventions.Microsoft)]
        private delegate byte AttackCountForResultDelegate(nuint partyMemberInfo, ushort attackResult);

        [Function(CallingConventions.Microsoft)]
        private delegate byte WeaponAttackHitCountDelegate(nuint partyMemberInfo, ushort skillId, ushort attackResult);

        [Function(CallingConventions.Microsoft)]
        private delegate byte IsWeaponAttackSkillDelegate(ushort skillId);

        [Function(CallingConventions.Microsoft)]
        private delegate ulong MissTripCheckDelegate(nuint attackerInfo, nuint resultData, ushort skillId, short param4);

        [Function(CallingConventions.Microsoft)]
        private delegate byte GetAttackStyleDelegate(nuint partyMemberInfo);

        [Function(CallingConventions.Microsoft)]
        private delegate short GetCharacterEquipmentDelegate(PartyMember character, EquipmentType type);

        [Function(CallingConventions.Microsoft)]
        private delegate byte IsFemcDelegate();

        [Function(CallingConventions.Microsoft)]
        private delegate void NormalAttackSetupDelegate(nuint battleAction);

        [Function(CallingConventions.Microsoft)]
        private delegate uint RangedPositioningDelegate(nuint battleAction);

        [Function(CallingConventions.Microsoft)]
        private delegate ulong PartyMotionMapDelegate(nuint actor, ushort logicalMotion);

        [Function(CallingConventions.Microsoft)]
        private delegate void BattleUnitInitDelegate(nuint actor, uint memberId);

        [Function(CallingConventions.Microsoft)]
        private delegate ushort AttackControlDelegate(nuint actor, ushort logicalMotion);

        [Function(CallingConventions.Microsoft)]
        private delegate nuint MitsuruAttackPositionDelegate(nuint actor, ushort attackIndex);

        [Function(CallingConventions.Microsoft)]
        private delegate void AttackCameraProfileDelegate(nuint cameraTask, nuint param2, nuint param3);

        [Function(CallingConventions.Microsoft)]
        private delegate void AttackCameraUpdateDelegate(nuint cameraTask);
    }
}
