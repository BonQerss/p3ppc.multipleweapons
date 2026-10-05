using p3ppc.multipleweapons.Configuration;
using p3ppc.multipleweapons.Template;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace p3ppc.multipleweapons
{

    public unsafe class Mod : ModBase
    {
        private Config _configuration;
        private WeaponModelHandler _weaponModelHandler = null!;
        private FollowUpPatch _followUpPatch = null!;
        private TwoHandedWeaponPatch _twoHandedWeaponPatch = null!;
        private WeaponLinkOwnerPatch _weaponLinkOwnerPatch = null!;
        private PoliceStationWeaponPatch _policeStationWeaponPatch = null!;

        public Mod(ModContext context)
        {
            _configuration = context.Configuration;

            if (!Utils.Initialise(context.Logger, _configuration, context.ModLoader))
            {
                return;
            }

            IReloadedHooks hooks = context.Hooks ?? throw new InvalidOperationException("Reloaded.Hooks is unavailable.");
            IMemory memory = Memory.Instance;

            _weaponModelHandler = new WeaponModelHandler();
            _weaponModelHandler.Hook(hooks, memory, _configuration);

            _twoHandedWeaponPatch = new TwoHandedWeaponPatch();
            _twoHandedWeaponPatch.Hook(hooks);

            _weaponLinkOwnerPatch = new WeaponLinkOwnerPatch();
            _weaponLinkOwnerPatch.Hook(hooks);

            _followUpPatch = new FollowUpPatch();
            _followUpPatch.Hook(hooks, memory);

            _policeStationWeaponPatch = new PoliceStationWeaponPatch();
            _policeStationWeaponPatch.Hook(hooks);
        }

        public override void ConfigurationUpdated(Config configuration)
        {
            _configuration = configuration;

            Utils.UpdateConfiguration(configuration);
            _weaponModelHandler?.UpdateConfiguration(configuration);
            Utils.LogDebug("Configuration updated.");
        }

#pragma warning disable CS8618
        public Mod() { }
#pragma warning restore CS8618
    }
}
