using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using System.Collections.Generic;
using static p3ppc.multipleweapons.Enums;

namespace p3ppc.multipleweapons
{

    internal unsafe class TwoHandedWeaponPatch
    {
        private readonly List<IAsmHook> _hooks = new();

        private const int MaleProtag = 1;
        private const int FemaleProtag = 99;

        private const int AkihikoWeaponLoaderId = 7;
        internal void Hook(IReloadedHooks hooks)
        {
            if (WeaponModelHandler.IsMaleProtagFists == null || WeaponModelHandler.IsFemaleProtagFists == null)
            {
                Utils.LogError("Fists model flags are not ready.");
                return;
            }

            nuint maleFistsFlag = (nuint)WeaponModelHandler.IsMaleProtagFists;

            nuint femaleFistsFlag = (nuint)WeaponModelHandler.IsFemaleProtagFists;

            Utils.SigScan("8D 4E FD F6 C1 FB", "Fists Left/Right Weapon Loader", address =>
            {
                string[] asm =
                {
                    "use64",
                    
                    "push rax",
                    
                    "movzx eax, word [rdi + 0xE6]",
                    
                    $"cmp eax, {MaleProtag}",
                    "je checkMaleFists",
                    
                    $"cmp eax, {FemaleProtag}",
                    "je checkFemaleFists",
                    
                    "jmp restore",
                    
                    "label checkMaleFists",
                    
                    $"mov rax, 0x{maleFistsFlag:X}",
                    "cmp byte [rax], 0",
                    "je restore",
                    
                    $"mov esi, {AkihikoWeaponLoaderId}",
                    
                    "jmp restore",
                    
                    "label checkFemaleFists",
                    
                    $"mov rax, 0x{femaleFistsFlag:X}",
                    "cmp byte [rax], 0",
                    "je restore",
                    
                    $"mov esi, {AkihikoWeaponLoaderId}",
                    
                    "label restore",
                    "pop rax"
                };
                
                _hooks.Add(hooks.CreateAsmHook(asm, address, AsmHookBehaviour.ExecuteFirst).Activate());
            });
        }
    }
}
