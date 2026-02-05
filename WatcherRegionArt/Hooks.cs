using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Menu;
using System.Reflection;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace WatcherRegionArt
{
    public class Hooks
    {
        private static Dictionary<string, Menu.MenuScene.SceneID> sceneIDLookup;
        private static Dictionary<Menu.MenuScene.SceneID, string> sceneToRegion;
        public static void Apply()
        {
            // Return new Watcher landscape scene id 
            On.Region.GetRegionLandscapeScene += Region_GetRegionLandscapeScene;
            // Pull assets for corresponding scene id
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            // Remove vanilla Watcher regions from choice menu
            On.Menu.FastTravelScreen.SpawnChoiceMenu += FastTravelScreen_SpawnChoiceMenu;
            // Move food meter to center
            On.Menu.SleepAndDeathScreen.FoodMeterXPos += SleepAndDeathScreen_FoodMeterXPos;
            // Set ripple to maximum available on passage
            On.SaveState.ApplyCustomEndGame += SaveState_ApplyCustomEndGame;


            try
            {
                // Skip Watcher forbid check when adding endgame tokens and subsequently spawning passage button
                IL.Menu.SleepAndDeathScreen.GetDataFromGame += SleepAndDeathScreen_GetDataFromGame;
                // Skip Watcher forbid check when adding passage button. Move passage button above food meter
                IL.Menu.SleepAndDeathScreen.AddPassageButton += SleepAndDeathScreen_AddPassageButton;
                // Lie about warp point mode map so that game doesn't set mapdata to null for passaging
                IL.Menu.FastTravelScreen.ctor += FastTravelScreen_ctor;
            }
            catch (Exception e)
            {
                Plugin.logger.LogInfo(e);
            }
        }

        private static MenuScene.SceneID Region_GetRegionLandscapeScene(On.Region.orig_GetRegionLandscapeScene orig, string regionAcro)
        {
            Menu.MenuScene.SceneID origReturn = orig.Invoke(regionAcro);

            if (origReturn != Menu.MenuScene.SceneID.Empty)
                return origReturn;

            if (sceneIDLookup == null)
                BuildLandscapeLookup();

            if (sceneIDLookup.TryGetValue(regionAcro, out var scene))
                return scene;

            return origReturn;
        }

        private static void BuildLandscapeLookup()
        {
            sceneIDLookup = new Dictionary<string, Menu.MenuScene.SceneID>();

            var fields = typeof(WatcherRegionArt.Enums.SceneID).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                    field.Name.StartsWith("Landscape_"))
                {
                    string regionCode = field.Name.Substring("Landscape_".Length);
                    var value = (Menu.MenuScene.SceneID)field.GetValue(null);

                    if (value != null)
                        sceneIDLookup[regionCode] = value;
                }
            }
        }

        private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, Menu.MenuScene self)
        {
            orig.Invoke(self);

            if (self.sceneID == null)
                return;

            if (sceneToRegion == null)
                BuildSceneRegionMap();

            if (!sceneToRegion.TryGetValue(self.sceneID, out string region))
                return;

            Plugin.logger.LogInfo("Building scene: " + region);
            string folder = $"Scenes{Path.DirectorySeparatorChar}Landscape - {region}";
            string flatName = $"Landscape - {region} - Flat";
            string shadowName = $"Title_{region}_Shadow";
            string titleName = $"Title_{region}";

            self.sceneFolder = folder;

            self.AddIllustration(
                new MenuIllustration(self.menu, self, folder, flatName, new Vector2(683f, 384f), false, true));

            self.AddIllustration(
                new MenuIllustration(self.menu, self, "", shadowName, new Vector2(0.01f, 0.01f), true, false));

            if (self.menu.ID == ProcessManager.ProcessID.FastTravelScreen || self.menu.ID == ProcessManager.ProcessID.RegionsOverviewScreen)
            {
                self.AddIllustration(
                    new MenuIllustration(self.menu, self, "", shadowName, new Vector2(0.01f, 0.01f), true, false));

                self.AddIllustration(
                    new MenuIllustration(self.menu, self, "", titleName, new Vector2(0.01f, 0.01f), true, false));

                self.flatIllustrations[self.flatIllustrations.Count - 1].sprite.shader = self.menu.manager.rainWorld.Shaders["MenuText"];
            }
        }

        private static void BuildSceneRegionMap()
        {
            sceneToRegion = new Dictionary<Menu.MenuScene.SceneID, string>();

            var fields = typeof(WatcherRegionArt.Enums.SceneID).GetFields(
                BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Menu.MenuScene.SceneID) &&
                    field.Name.StartsWith("Landscape_"))
                {
                    var value = (Menu.MenuScene.SceneID)field.GetValue(null);
                    if (value != null)
                    {
                        string region = field.Name.Substring("Landscape_".Length);
                        sceneToRegion[value] = region;
                    }
                }
            }
        }

        private static void FastTravelScreen_SpawnChoiceMenu(On.Menu.FastTravelScreen.orig_SpawnChoiceMenu orig, FastTravelScreen self)
        {
            if (self.activeMenuSlugcat == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                if (self.IsFastTravelScreen)
                {
                    for (int i = self.accessibleRegions.Count - 1; 0 <= i; i--)
                    {
                        if (Region.IsWatcherVanillaRegion(self.manager.rainWorld.progression.regionNames[self.accessibleRegions[i]]))
                        {
                            self.accessibleRegions.Remove(self.accessibleRegions[i]);
                        }
                    }
                }
            }
            orig(self);
        }

        private static float SleepAndDeathScreen_FoodMeterXPos(On.Menu.SleepAndDeathScreen.orig_FoodMeterXPos orig, SleepAndDeathScreen self, float down)
        {
            if (self.UsesWarpMap)
            {
                return RWCustom.Custom.LerpMap(self.manager.rainWorld.options.ScreenSize.x, 1024f, 1366f, self.manager.rainWorld.options.ScreenSize.x / 2f - 110f, 540f);
            }
            return orig(self, down);
        }

        private static void SaveState_ApplyCustomEndGame(On.SaveState.orig_ApplyCustomEndGame orig, SaveState self, RainWorldGame game, bool addFiveCycles)
        {
            if (self.saveStateNumber == Watcher.WatcherEnums.SlugcatStatsName.Watcher)
            {
                self.deathPersistentSaveData.rippleLevel = self.deathPersistentSaveData.maximumRippleLevel;
            }
            //             FNAF wowie!!1!!111
            orig(self, game, addFiveCycles);
        }

        private static void SleepAndDeathScreen_GetDataFromGame(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                i => i.MatchLdloc(3),
                i => i.MatchBrtrue(out _)))
            {
                c.Remove();
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Plugin.logger.LogError("SleepAndDeathScreen_GetDataFromGame FAILURE " + il);
        }

        private static void SleepAndDeathScreen_AddPassageButton(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdsfld(typeof(ModManager), "Watcher"),
                x => x.MatchBrfalse(out _)
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Plugin.logger.LogError("SleepAndDeathScreen_AddPassageButton watcher check skip FAILURE " + il);
        }

        private static void FastTravelScreen_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchCall("Menu.FastTravelScreen", "get_WarpPointModeActive")))
            {
                c.EmitDelegate<Func<FastTravelScreen, FastTravelScreen>>((self) =>
                {
                    if (self.IsFastTravelScreen)
                    {
                        self.warpPointModeAvailable = false;
                    }
                    return self;
                });
            }
            else Plugin.logger.LogError("FastTravelScreen_ctor passage FAIULRE " + il);
        }
    }
}
