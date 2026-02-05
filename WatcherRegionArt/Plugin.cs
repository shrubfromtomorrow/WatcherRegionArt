using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace WatcherRegionArt
{
    [BepInPlugin(ID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ID = "shrub.watcherregionart";
        public const string NAME = "Watcher Region Art";
        public const string VERSION = "1.0.0";

        internal static ManualLogSource logger;
        public static Plugin instance;

        public static bool AppliedEnums;

        public void OnEnable()
        {
            instance = this;
            logger = Logger;
            On.RainWorld.OnModsInit += OnModsInit;
        }

        private static void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld game)
        {
            orig(game);

            if (AppliedEnums) return;

            AppliedEnums = true;
            Enums.SceneID.RegisterValues();
            Hooks.Apply();
        }
    }
}
