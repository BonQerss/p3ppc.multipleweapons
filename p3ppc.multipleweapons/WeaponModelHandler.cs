using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using p3ppc.multipleweapons.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using static p3ppc.multipleweapons.Enums;

namespace p3ppc.multipleweapons
{
    internal unsafe class WeaponModelHandler
    {

        [ThreadStatic]

        private IHook<GetCharacterModelGMOPathDelegate> _getCharacterModelGMOPathHook;
        private GetCharacterEquipmentDelegate _getCharacterEquipment;
        private IHook<UpdateWeaponModelDelegate> _updateWeaponModelHook;
        private IHook<SetCharacterEquipmentDelegate> _setEquipmentHook;
        private IHook<ReloadModelTaskCleanupDelegate> _reloadModelTaskCleanupHook;

        private WeaponData.ItemTblEntry** _itemEntries;

        private uint _pendingWeaponRefreshCharacter;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate byte IsFemcDelegate();

        private IsFemcDelegate _isFemcFunc;

        [Function(CallingConventions.Microsoft)]
        private delegate void UpdateWeaponModelDelegate(uint character);

        [Function(CallingConventions.Microsoft)]
        private delegate void ReloadUnitModelDelegate(short character);

        private ReloadUnitModelDelegate _reloadUnitModel;

        private static readonly Dictionary<PartyMember, short> _oldWeaponMap = new Dictionary<PartyMember, short>();

        private IMemory _memory;
        private Config _configuration = null!;

        private float* _fieldAttackAcquisitionRange;

        private float* _fieldAttackArcDegrees;

        private float* _fieldAttackSpeedMultiplier;

        private IHook<StartModelMotionDelegate> _startModelMotionHook = null!;

        private readonly List<IAsmHook> _fieldAttackTimingHooks = new List<IAsmHook>();
        private nuint _fieldPlayerModelGlobalAddress;

        internal static bool* IsMaleProtagFists;
        internal static bool* IsFemaleProtagFists;

        internal static bool* IsMaleProtagBow;
        internal static bool* IsFemaleProtagBow;

        internal void Hook(IReloadedHooks hooks, IMemory memory, Config configuration)
        {
            _memory = memory;
            _configuration = configuration;

            _fieldAttackAcquisitionRange = (float*)memory.Allocate(sizeof(float));

            _fieldAttackArcDegrees = (float*)memory.Allocate(sizeof(float));

            _fieldAttackSpeedMultiplier = (float*)memory.Allocate(sizeof(float));

            *_fieldAttackAcquisitionRange = 360.0f;
            *_fieldAttackArcDegrees = 180.0f;
            *_fieldAttackSpeedMultiplier = 1.0f;

            IsMaleProtagFists = (bool*)memory.Allocate(1);
            IsFemaleProtagFists = (bool*)memory.Allocate(1);
            IsMaleProtagBow = (bool*)memory.Allocate(1);
            IsFemaleProtagBow = (bool*)memory.Allocate(1);

            *IsMaleProtagFists = false;
            *IsFemaleProtagFists = false;
            *IsMaleProtagBow = false;
            *IsFemaleProtagBow = false;

            Utils.SigScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 0F B7 D9 49 8B F8 48 8D 0D ?? ?? ?? ?? 0F B7 F2", "GetCharacterModelGMOPath", address =>
            {
                _getCharacterModelGMOPathHook = hooks.CreateHook<GetCharacterModelGMOPathDelegate>(GetCharacterModelGMOPath, address).Activate();
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 0F B7 F9 48 0F BF DA", "GetCharacterEquipment (weapon read)", address =>
            {
                _getCharacterEquipment = hooks.CreateWrapper<GetCharacterEquipmentDelegate>(address, out _);
                
                RefreshCurrentFieldAttackSettings();
            });

            Utils.SigScan("48 89 05 ?? ?? ?? ?? 44 89 C2", "ItemTblEntries Ptr", address =>
            {
                _itemEntries = (WeaponData.ItemTblEntry**)Utils.GetGlobalAddress(address + 3);
                
                RefreshCurrentFieldAttackSettings();
            });

            Utils.SigScan("48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 05 ?? ?? ?? ?? C1 E8 07", "IsFemc Func", address =>
            {
                _isFemcFunc = hooks.CreateWrapper<IsFemcDelegate>(address, out _);
            });

            Utils.SigScan("E8 ?? ?? ?? ?? 0F B7 CE E8 ?? ?? ?? ?? FE C8", "SetCharacterEquipment Ptr (weapon change)", address =>
            {
                nint funcAddress = Utils.ResolveRelativeCall(address);
                _setEquipmentHook = hooks.CreateHook<SetCharacterEquipmentDelegate>(OnSetCharacterEquipment, funcAddress).Activate();
            });

            Utils.SigScan("E8 ?? ?? ?? ?? 41 C7 06 0D 00 00 00 EB ??", "Update Weapon Model", address =>
            {
                nint funcAddress = Utils.ResolveRelativeCall(address);
                _updateWeaponModelHook = hooks.CreateHook<UpdateWeaponModelDelegate>(OnUpdateWeaponModel, funcAddress).Activate();
            });

            Utils.SigScan("E8 ?? ?? ?? ?? 41 C7 06 0E 00 00 00 EB ??", "ReloadUnitModel Ptr", address =>
            {
                nint funcAddress = Utils.ResolveRelativeCall(address);
                _reloadUnitModel = hooks.CreateWrapper<ReloadUnitModelDelegate>(funcAddress, out _);
            });

            Utils.SigScan("48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 " + "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " + "48 8B 7B ?? 48 8B 4F ??", "Field Model Cleanup", address =>
            {
                _reloadModelTaskCleanupHook = hooks.CreateHook<ReloadModelTaskCleanupDelegate>(OnReloadModelTaskCleanup, address).Activate();
            });

            Utils.SigScan("0F BF 02 48 8B 0D ?? ?? ?? ?? 66 0F 6E C8 0F 5B C9 E8 ?? ?? ?? ?? 41 83 BE 5C 01 00 00 08", "Field Attack Timing", address =>
            {
                nuint playerModelGlobal = Utils.GetGlobalAddress(address + 6);
                string[] asm =
                {
                    "use64",
                    "push rax",
                    "push rcx",
                    $"mov rax, 0x{playerModelGlobal:X}",
                    "mov rax, [rax]",
                    "test rax, rax",
                    "jz done",
                    "mov rax, [rax]",
                    "test rax, rax",
                    "jz done",
                    "cmp dword [rax + 0x2B8], 5",
                    "jl done",
                    "mov rcx, [rax + 0x2A0]",
                    "test rcx, rcx",
                    "jz done",
                    "mov rcx, [rcx + 0x20]",
                    "test rcx, rcx",
                    "jz done",
                    "movss xmm0, dword [rcx + 0x4C]",
                    "subss xmm0, dword [rcx + 0x48]",
                    "cvttss2si eax, xmm0",
                    "cmp eax, 1",
                    "jl done",
                    "cmp eax, 0x7FFF",
                    "jg done",
                    "mov word [rdx + 0x02], ax",
                    "label done",
                    "pop rcx",
                    "pop rax"
                };
                
                _fieldAttackTimingHooks.Add(hooks.CreateAsmHook(asm, address, AsmHookBehaviour.ExecuteFirst).Activate());
                _fieldPlayerModelGlobalAddress = playerModelGlobal;
            });

            Utils.SigScan("E8 ?? ?? ?? ?? 41 89 06 E8 ?? ?? ?? ?? 89 B0 A0 13 00 00", "Start Model Motion", address =>
            {
                InstallFieldAttackMotionSpeedHook(hooks, Utils.ResolveRelativeCall(address));
            });

            Utils.SigScan("48 8B C4 48 89 58 ?? 55 57 41 56", "Field Target Acquisition", address =>
            {
                InstallFieldAttackTargetAcquireHook(hooks, address);
            });

        }

        internal void UpdateConfiguration(Config configuration)
        {
            _configuration = configuration;
            RefreshCurrentFieldAttackSettings();
        }

        private void InstallFieldAttackTargetAcquireHook(IReloadedHooks hooks, nint address)
        {
            nuint rangeAddress = (nuint)_fieldAttackAcquisitionRange;

            nuint arcAddress = (nuint)_fieldAttackArcDegrees;

            string[] asm =
            {
                "use64",
                "push rax",

                $"mov rax, 0x{rangeAddress:X}",
                "movss xmm1, dword [rax]",

                $"mov rax, 0x{arcAddress:X}",
                "mov eax, dword [rax]",
                "mov r8d, eax",
                "movd xmm2, eax",

                "pop rax"
            };

            _fieldAttackTimingHooks.Add(hooks.CreateAsmHook(asm, address, AsmHookBehaviour.ExecuteFirst).Activate());
        }

        private void InstallFieldAttackMotionSpeedHook(IReloadedHooks hooks, nint address)
        {
            _startModelMotionHook = hooks.CreateHook<StartModelMotionDelegate>(OnStartModelMotion, address).Activate();
        }

        private nuint OnStartModelMotion(nuint modelManager, ushort layer, short motion, ushort transition, ushort flags)
        {
            nuint result = _startModelMotionHook.OriginalFunction(modelManager, layer, motion, transition, flags);

            nuint fieldPlayerModel = 0;
            if (_fieldPlayerModelGlobalAddress != 0)
            {
                fieldPlayerModel = *(nuint*)_fieldPlayerModelGlobalAddress;
            }

            bool isFieldPlayerModel = fieldPlayerModel != 0 && modelManager == fieldPlayerModel;
            if (modelManager == 0 || layer != 0 || _fieldAttackSpeedMultiplier == null || !isFieldPlayerModel)
            {
                return result;
            }

            float speed = motion == 4 ? *_fieldAttackSpeedMultiplier : 1.0f;
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                speed = 1.0f;
            }

            *(float*)(modelManager + 0x110) = Math.Clamp(speed, 0.25f, 3.0f);
            return result;
        }

        private void RefreshCurrentFieldAttackSettings()
        {
            if (_getCharacterEquipment == null ||
                _itemEntries == null ||
                *_itemEntries == null ||
                _fieldAttackAcquisitionRange == null ||
                _fieldAttackArcDegrees == null ||
                _fieldAttackSpeedMultiplier == null ||
                _configuration == null)
            {
                return;
            }

            short item = _getCharacterEquipment(PartyMember.MaleProtag, EquipmentType.Weapon);

            if (item <= 0 || item >= 512)
            {
                return;
            }

            ApplyFieldAttackSettings(item);
        }

        private void ApplyFieldAttackSettings(short item)
        {
            if (_fieldAttackAcquisitionRange == null ||
                _fieldAttackArcDegrees == null ||
                _fieldAttackSpeedMultiplier == null ||
                _configuration == null ||
                _itemEntries == null ||
                *_itemEntries == null ||
                item <= 0 ||
                item >= 512)
            {
                return;
            }

            WeaponData.ItemTblEntry entry = (*_itemEntries)[item];

            float targetRange = GetConfiguredFieldAttackAcquisitionRange(entry.Icon);

            float attackArc = GetConfiguredFieldAttackArcDegrees(entry.Icon);

            float attackSpeed = GetConfiguredFieldAttackSpeed(entry.Icon);

            if (float.IsNaN(targetRange) || float.IsInfinity(targetRange))
            {
                targetRange = 360.0f;
            }

            if (float.IsNaN(attackArc) || float.IsInfinity(attackArc))
            {
                attackArc = 180.0f;
            }

            if (float.IsNaN(attackSpeed) || float.IsInfinity(attackSpeed))
            {
                attackSpeed = 1.0f;
            }

            targetRange = Math.Clamp(targetRange, 1.0f, 10000.0f);

            attackArc = Math.Clamp(attackArc, 1.0f, 360.0f);

            attackSpeed = Math.Clamp(attackSpeed, 0.25f, 3.0f);

            *_fieldAttackAcquisitionRange = targetRange;

            *_fieldAttackArcDegrees = attackArc;

            *_fieldAttackSpeedMultiplier = attackSpeed;

            Utils.LogDebug($"Field attack {item}: range={targetRange:0.#}, arc={attackArc:0.#}, speed={attackSpeed:0.##}x.");
        }

        private float GetConfiguredFieldAttackAcquisitionRange(WeaponData.WeaponIcon icon)
        {
            return icon switch
            {
                WeaponData.WeaponIcon.OneHSwordIcon => _configuration.OneHandedSwordRapierAcquisitionRange,

                WeaponData.WeaponIcon.RapierIcon => _configuration.OneHandedSwordRapierAcquisitionRange,

                WeaponData.WeaponIcon.TwoHSwordIcon => _configuration.TwoHandedSwordAcquisitionRange,

                WeaponData.WeaponIcon.AxeIcon => _configuration.AxeAcquisitionRange,

                WeaponData.WeaponIcon.BowIcon => _configuration.BowAcquisitionRange,

                WeaponData.WeaponIcon.FistsIcon => _configuration.FistsAcquisitionRange,

                WeaponData.WeaponIcon.SpearIcon => _configuration.SpearNaginataAcquisitionRange,

                WeaponData.WeaponIcon.NaginataIcon => _configuration.SpearNaginataAcquisitionRange,

                _ => 360.0f
            };
        }

        private float GetConfiguredFieldAttackArcDegrees(WeaponData.WeaponIcon icon)
        {
            return icon switch
            {
                WeaponData.WeaponIcon.OneHSwordIcon => _configuration.OneHandedSwordRapierAttackArcDegrees,

                WeaponData.WeaponIcon.RapierIcon => _configuration.OneHandedSwordRapierAttackArcDegrees,

                WeaponData.WeaponIcon.TwoHSwordIcon => _configuration.TwoHandedSwordAttackArcDegrees,

                WeaponData.WeaponIcon.AxeIcon => _configuration.AxeAttackArcDegrees,

                WeaponData.WeaponIcon.BowIcon => _configuration.BowAttackArcDegrees,

                WeaponData.WeaponIcon.FistsIcon => _configuration.FistsAttackArcDegrees,

                WeaponData.WeaponIcon.SpearIcon => _configuration.SpearNaginataAttackArcDegrees,

                WeaponData.WeaponIcon.NaginataIcon => _configuration.SpearNaginataAttackArcDegrees,

                _ => 180.0f
            };
        }

        private float GetConfiguredFieldAttackSpeed(WeaponData.WeaponIcon icon)
        {
            return icon switch
            {
                WeaponData.WeaponIcon.OneHSwordIcon => (float)_configuration.OneHandedSwordRapierAttackSpeed,

                WeaponData.WeaponIcon.RapierIcon => (float)_configuration.OneHandedSwordRapierAttackSpeed,

                WeaponData.WeaponIcon.TwoHSwordIcon => (float)_configuration.TwoHandedSwordAttackSpeed,

                WeaponData.WeaponIcon.AxeIcon => (float)_configuration.AxeAttackSpeed,

                WeaponData.WeaponIcon.BowIcon => (float)_configuration.BowAttackSpeed,

                WeaponData.WeaponIcon.FistsIcon => (float)_configuration.FistsAttackSpeed,

                WeaponData.WeaponIcon.SpearIcon => (float)_configuration.SpearNaginataAttackSpeed,

                WeaponData.WeaponIcon.NaginataIcon => (float)_configuration.SpearNaginataAttackSpeed,

                _ => 1.0f
            };
        }

        private void OnSetCharacterEquipment(PartyMember character, EquipmentType type, short item)
        {
            bool protagonist = character == PartyMember.MaleProtag || character == PartyMember.FemaleProtag;

            if (type == EquipmentType.Weapon && protagonist)
            {

                if (_getCharacterEquipment != null)
                {
                    short oldItem = _getCharacterEquipment(character, EquipmentType.Weapon);

                    if (oldItem > 0 && oldItem < 512)
                    {
                        _oldWeaponMap[character] = oldItem;

                        Utils.LogDebug($"Previous weapon: {character}={oldItem}.");
                    }
                    else
                    {
                        Utils.LogDebug($"Previous weapon unavailable: {character}={oldItem}.");
                    }
                }

                WeaponPack newPack = GetWeaponPack(character, item);

                Utils.LogDebug($"Equip: {character}, item={item}, pack={newPack}.");

                SetFistsFlag(character, newPack == WeaponPack.Fists);

                SetBowFlag(character, newPack == WeaponPack.Bow);

                Utils.LogDebug($"Weapon flags updated for {character}.");

                ApplyFieldAttackSettings(item);
            }

            _setEquipmentHook.OriginalFunction(character, type, item);
        }

        private void OnUpdateWeaponModel(uint character)
        {
            Utils.LogDebug($"Refreshing the weapon model for {character}.");

            bool isProtagonist = character == 1 || character == 63;

            if (!isProtagonist)
            {
                _updateWeaponModelHook.OriginalFunction(character);
                return;
            }

            PartyMember member = character == 1 ? PartyMember.MaleProtag : PartyMember.FemaleProtag;

            short oldItem = 0;
            WeaponPack oldPack = WeaponPack.None;

            if (_oldWeaponMap.TryGetValue(member, out oldItem))
            {
                _oldWeaponMap.Remove(member);
                oldPack = GetWeaponPack(member, oldItem);
            }

            short newItem = 0;
            WeaponPack newPack = WeaponPack.None;

            if (_getCharacterEquipment != null)
            {
                newItem = _getCharacterEquipment(member, EquipmentType.Weapon);

                newPack = GetWeaponPack(member, newItem);
            }

            Utils.LogDebug($"Weapon change: {oldItem}/{oldPack}->{newItem}/{newPack}.");

            if (newItem > 0)
            {
                ApplyFieldAttackSettings(newItem);
            }

            SetFistsFlag(member, newPack == WeaponPack.Fists);

            SetBowFlag(member, newPack == WeaponPack.Bow);

            _updateWeaponModelHook.OriginalFunction(character);

            if (oldPack == WeaponPack.None || newPack == WeaponPack.None || oldPack == newPack)
            {
                Utils.LogDebug($"Body pack stayed on {newPack}; using normal weapon refresh.");
                return;
            }

            if (_reloadUnitModel == null || _reloadModelTaskCleanupHook == null)
            {
                Utils.LogError("The model reload hook is not ready, so the pack change was canceled.");
                return;
            }

            _pendingWeaponRefreshCharacter = character;

            Utils.LogDebug($"Body pack changed {oldPack}->{newPack}; reloading the field model.");

            _reloadUnitModel((short)character);
        }

        private void OnReloadModelTaskCleanup(nuint task)
        {

            nuint reloadState = task != 0 ? *(nuint*)(task + 0x48) : 0;

            short completedCharacter = 0;

            if (reloadState != 0)
            {
                completedCharacter = *(short*)(reloadState + 0x02);
            }

            _reloadModelTaskCleanupHook.OriginalFunction(task);

            if (_pendingWeaponRefreshCharacter == 0)
            {
                return;
            }

            if (completedCharacter <= 0 || (uint)completedCharacter != _pendingWeaponRefreshCharacter)
            {
                return;
            }

            uint character = _pendingWeaponRefreshCharacter;

            _pendingWeaponRefreshCharacter = 0;

            Utils.LogDebug($"Field model reloaded for {character}, so both weapon slots will refresh.");

            _updateWeaponModelHook.OriginalFunction(character);
        }

        private WeaponPack GetWeaponPack(PartyMember character, short item)
        {
            if (item <= 0 || item >= 512 || _itemEntries == null)
            {
                return WeaponPack.None;
            }

            WeaponData.ItemTblEntry entry = (*_itemEntries)[item];

            return WeaponData.GetWeaponPackForCharacter(character, entry);
        }

        private static void SetFistsFlag(PartyMember character, bool value)
        {
            if (character == PartyMember.MaleProtag)
            {
                if (IsMaleProtagFists != null)
                {
                    *IsMaleProtagFists = value;
                }
            }
            else if (character == PartyMember.FemaleProtag)
            {
                if (IsFemaleProtagFists != null)
                {
                    *IsFemaleProtagFists = value;
                }
            }
        }

        private static void SetBowFlag(PartyMember character, bool value)
        {
            if (character == PartyMember.MaleProtag && IsMaleProtagBow != null)
            {
                *IsMaleProtagBow = value;
            }
            else if (character == PartyMember.FemaleProtag && IsFemaleProtagBow != null)
            {
                *IsFemaleProtagBow = value;
            }
        }

        private int GetCharacterModelGMOPath(short param_1, PartyMember member, nuint outStr, nuint param_4)
        {
            if (_getCharacterEquipment == null || _itemEntries == null)
            {
                return _getCharacterModelGMOPathHook.OriginalFunction(param_1, member, outStr, param_4);
            }

            bool isProtagonist = member == PartyMember.MaleProtag || member == PartyMember.FemaleProtag;

            if (!isProtagonist)
            {
                return _getCharacterModelGMOPathHook.OriginalFunction(param_1, member, outStr, param_4);
            }

            PartyMember pathMember = member;
            if (pathMember == PartyMember.MaleProtag && _isFemcFunc != null && _isFemcFunc() != 0)
            {
                pathMember = PartyMember.FemaleProtag;
            }

            short weaponItem = _getCharacterEquipment(member, EquipmentType.Weapon);
            WeaponPack pack = GetWeaponPack(pathMember, weaponItem);

            SetFistsFlag(pathMember, pack == WeaponPack.Fists);

            SetBowFlag(pathMember, pack == WeaponPack.Bow);

            if (pack == WeaponPack.None)
            {
                return _getCharacterModelGMOPathHook.OriginalFunction(param_1, member, outStr, param_4);
            }

            int charId = pathMember switch
            {
                PartyMember.MaleProtag => 1,
                PartyMember.FemaleProtag => 63,
                _ => (int)pathMember
            };

            if (pack == WeaponPack.Fists)
            {

                if (param_1 == 1)
                {
                    string bodyPath = $"model/pack/bc{charId:D3}_wp5.GMO";
                    Utils.LogDebug($"Fists model: {bodyPath}.");
                    _memory.WriteRaw(outStr, Encoding.ASCII.GetBytes($"{bodyPath}\0"));
                    return 1;
                }
                else
                {

                    return _getCharacterModelGMOPathHook.OriginalFunction(param_1, member, outStr, param_4);
                }
            }

            if (param_1 == 1)
            {
                string path = $"model/pack/bc{charId:D3}_wp{(int)pack}.GMO";
                Utils.LogDebug($"Body model: {path}.");
                _memory.WriteRaw(outStr, Encoding.ASCII.GetBytes($"{path}\0"));
                return 1;
            }
            else
            {

                return _getCharacterModelGMOPathHook.OriginalFunction(param_1, member, outStr, param_4);
            }
        }

        [Function(CallingConventions.Microsoft)]
        private delegate nuint StartModelMotionDelegate(nuint modelManager, ushort layer, short motion, ushort transition, ushort flags);

        [Function(CallingConventions.Microsoft)]
        private delegate void ReloadModelTaskCleanupDelegate(nuint task);

        [Function(CallingConventions.Microsoft)]
        private delegate int GetCharacterModelGMOPathDelegate(short param_1, PartyMember member, nuint outStr, nuint param_4);

        [Function(CallingConventions.Microsoft)]
        private delegate short GetCharacterEquipmentDelegate(PartyMember character, EquipmentType type);

        [Function(CallingConventions.Microsoft)]
        private delegate void SetCharacterEquipmentDelegate(PartyMember character, EquipmentType type, short item);
    }
}
