using p3ppc.multipleweapons.Template.Configuration;
using System.ComponentModel;

namespace p3ppc.multipleweapons.Configuration
{
    public class Config : Configurable<Config>
    {
        [Category("General")]
        [DisplayName("Debug Logging")]
        [Description("Logs additional Multiple Weapons diagnostics.")]
        [DefaultValue(false)]
        public bool DebugEnabled { get; set; } = false;

        [Category("Field Attack Range")]
        [DisplayName("1H Sword / Rapier Acquisition Range")]
        [Description("Sets the 1H sword and rapier field attack range.")]
        [DefaultValue(360.0f)]
        public float OneHandedSwordRapierAcquisitionRange { get; set; } = 360.0f;

        [Category("Field Attack Range")]
        [DisplayName("2H Sword Acquisition Range")]
        [Description("Sets the two-handed sword field attack range.")]
        [DefaultValue(420.0f)]
        public float TwoHandedSwordAcquisitionRange { get; set; } = 420.0f;

        [Category("Field Attack Range")]
        [DisplayName("Axe Acquisition Range")]
        [Description("Sets the axe field attack range.")]
        [DefaultValue(330.0f)]
        public float AxeAcquisitionRange { get; set; } = 330.0f;

        [Category("Field Attack Range")]
        [DisplayName("Bow Acquisition Range")]
        [Description("Sets the bow field attack range.")]
        [DefaultValue(1080.0f)]
        public float BowAcquisitionRange { get; set; } = 1080.0f;

        [Category("Field Attack Range")]
        [DisplayName("Fists Acquisition Range")]
        [Description("Sets the fists field attack range.")]
        [DefaultValue(260.0f)]
        public float FistsAcquisitionRange { get; set; } = 260.0f;

        [Category("Field Attack Range")]
        [DisplayName("Spear / Naginata Acquisition Range")]
        [Description("Sets the spear and naginata field attack range.")]
        [DefaultValue(480.0f)]
        public float SpearNaginataAcquisitionRange { get; set; } = 480.0f;

        [Category("Field Attack Width")]
        [DisplayName("1H Sword / Rapier Attack Arc")]
        [Description("Sets the 1H sword and rapier attack arc.")]
        [DefaultValue(180.0f)]
        public float OneHandedSwordRapierAttackArcDegrees { get; set; } = 180.0f;

        [Category("Field Attack Width")]
        [DisplayName("2H Sword Attack Arc")]
        [Description("Sets the two-handed sword attack arc.")]
        [DefaultValue(210.0f)]
        public float TwoHandedSwordAttackArcDegrees { get; set; } = 210.0f;

        [Category("Field Attack Width")]
        [DisplayName("Axe Attack Arc")]
        [Description("Sets the axe attack arc.")]
        [DefaultValue(120.0f)]
        public float AxeAttackArcDegrees { get; set; } = 120.0f;

        [Category("Field Attack Width")]
        [DisplayName("Bow Attack Arc")]
        [Description("Sets the bow attack arc.")]
        [DefaultValue(45.0f)]
        public float BowAttackArcDegrees { get; set; } = 45.0f;

        [Category("Field Attack Width")]
        [DisplayName("Fists Attack Arc")]
        [Description("Sets the fists attack arc.")]
        [DefaultValue(90.0f)]
        public float FistsAttackArcDegrees { get; set; } = 90.0f;

        [Category("Field Attack Width")]
        [DisplayName("Spear / Naginata Attack Arc")]
        [Description("Sets the spear and naginata attack arc.")]
        [DefaultValue(180.0f)]
        public float SpearNaginataAttackArcDegrees { get; set; } = 180.0f;

        [Category("Field Attack Speed")]
        [DisplayName("1H Sword / Rapier Attack Speed")]
        [Description("Sets the 1H sword and rapier field attack speed.")]
        [DefaultValue(1.00)]
        public double OneHandedSwordRapierAttackSpeed { get; set; } = 1.00;

        [Category("Field Attack Speed")]
        [DisplayName("2H Sword Attack Speed")]
        [Description("Sets the field attack speed.")]
        [DefaultValue(1.00)]
        public double TwoHandedSwordAttackSpeed { get; set; } = 1.00;

        [Category("Field Attack Speed")]
        [DisplayName("Axe Attack Speed")]
        [Description("Sets the axe field attack speed.")]
        [DefaultValue(1.00)]
        public double AxeAttackSpeed { get; set; } = 1.00;

        [Category("Field Attack Speed")]
        [DisplayName("Bow Attack Speed")]
        [Description("Sets the field attack speed.")]
        [DefaultValue(1.00)]
        public double BowAttackSpeed { get; set; } = 1.00;

        [Category("Field Attack Speed")]
        [DisplayName("Fists Attack Speed")]
        [Description("Sets the fists field attack speed.")]
        [DefaultValue(1.00)]
        public double FistsAttackSpeed { get; set; } = 1.00;

        [Category("Field Attack Speed")]
        [DisplayName("Spear / Naginata Attack Speed")]
        [Description("Sets the spear and naginata field attack speed.")]
        [DefaultValue(1.00)]
        public double SpearNaginataAttackSpeed { get; set; } = 1.00;
    }

    public class ConfiguratorMixin : ConfiguratorMixinBase
    {
    }
}
