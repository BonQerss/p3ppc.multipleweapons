using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;

namespace p3ppc.multipleweapons
{

    internal unsafe class WeaponLinkOwnerPatch
    {
        private readonly List<IAsmHook> _hooks = new();

        private const int MaleProtag = 1;
        private const int FemaleProtagEnum = 99;
        private const int FemaleProtagFieldId = 63;
        private const int Yukari = 2;

        internal void Hook(IReloadedHooks hooks)
        {
            if (WeaponModelHandler.IsMaleProtagBow == null || WeaponModelHandler.IsFemaleProtagBow == null)
            {
                Utils.LogError("Bow model flags are not ready.");
                return;
            }

            nuint maleBowFlag = (nuint)WeaponModelHandler.IsMaleProtagBow;

            nuint femaleBowFlag = (nuint)WeaponModelHandler.IsFemaleProtagBow;

            Utils.SigScan("44 0F B6 8F ?? ?? ?? ?? 31 D2", "Bow linked owner slot 0", address =>
            {
                _hooks.Add(hooks.CreateAsmHook(BuildOwnerOverrideAsm(maleBowFlag, femaleBowFlag), address, AsmHookBehaviour.ExecuteAfter).Activate());
            });

            Utils.SigScan("44 0F B6 8F ?? ?? ?? ?? BA 01 00 00 00", "Bow linked owner slot 1", address =>
            {
                _hooks.Add(hooks.CreateAsmHook(BuildOwnerOverrideAsm(maleBowFlag, femaleBowFlag), address, AsmHookBehaviour.ExecuteAfter).Activate());
            });
        }

        private static string[] BuildOwnerOverrideAsm(nuint maleBowFlag, nuint femaleBowFlag)
        {
            return new[]
            {
                "use64",
                "push rax",
                $"cmp r9d, {MaleProtag}",
                "je checkMaleBow",
                $"cmp r9d, {FemaleProtagEnum}",
                "je checkFemaleBow",
                $"cmp r9d, {FemaleProtagFieldId}",
                "je checkFemaleBow",
                "jmp done",
                "label checkMaleBow",
                $"mov rax, 0x{maleBowFlag:X}",
                "cmp byte [rax], 0",
                "je done",
                $"mov r9d, {Yukari}",
                "jmp done",
                "label checkFemaleBow",
                $"mov rax, 0x{femaleBowFlag:X}",
                "cmp byte [rax], 0",
                "je done",
                $"mov r9d, {Yukari}",
                "label done",
                "pop rax"
            };
        }

    }
}
