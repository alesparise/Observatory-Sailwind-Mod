using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
//poorly written by pr0skynesis (discord username)

namespace Observatory
{   /// <summary>
    /// NOTE: I do think Observatory breaks the water everywhere, making waves appear in protected waters...
    /// TODO: 
    /// • System to manage scenes with IslandHorizon    //DONE
    /// • Add Observatory as destinations for other island missions
    /// • replace continental shelf of Observatory with a round-ish one!
    /// • Lower poly count for terrain before import (all the flat area should not have that many triangles
    /// • Make inner dock deeper, outer dock shallower (to have more wave attenuation there)
    /// • Might have to update ProfitPercent to adapt to longerIndexes!?!?
    /// 
    /// CODE REORGANIZATION:
    /// IndexManager should be renamed to SaveManager and act as a centralized manager for all things affecting saves:
    ///     • SaveManager           //DONE
    ///     • ItemIndexManager      //DONE
    ///     • PortIndexManager      //DONE
    ///     • SaveCleaner           //DONE
    ///     
    /// KNOWN ISSUES:
    /// • When opening the trade book in Observatory, the Lagoon Bookmark will be selected. Manually switch to Aestrin. No straightforward fix.
    /// </summary>
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ObservatoryMain : BaseUnityPlugin
    {
        // Necessary plugin info
        public const string pluginGuid = "pr0skynesis.observatory";
        public const string pluginName = "Observatory";
        public const string pluginVersion = "1.0.0";
        public const string shortName = "pr0.observatory";

        //config file info

        public void Awake()
        {
            bool cleanSave = false;  //debug: replace this with config option

            Harmony harmony = new Harmony(pluginGuid);
            if (!cleanSave)
            {
                //Debug.LogWarning("Observatory: running default patches");
                //Add things from bundle
                MethodInfo original = AccessTools.Method(typeof(FloatingOriginManager), "Start");
                MethodInfo patch = AccessTools.Method(typeof(ObservatoryPatches), "AddAssets");
                harmony.Patch(original, new HarmonyMethod(patch));
                //Save modded item indexes
                MethodInfo original2 = AccessTools.Method(typeof(SaveLoadManager), "LoadModData");
                MethodInfo patch2 = AccessTools.Method(typeof(SaveManager), "SaveIndex");
                harmony.Patch(original2, new HarmonyMethod(patch2));
                //Runs the main Manager() method
                MethodInfo original3 = AccessTools.Method(typeof(SaveLoadManager), "LoadGame");
                MethodInfo patch3 = AccessTools.Method(typeof(SaveManager), "Manager");
                harmony.Patch(original3, new HarmonyMethod(patch3));
                //Save mod data on new game
                MethodInfo original4 = AccessTools.Method(typeof(StartMenu), "StartNewGame");
                MethodInfo patch4 = AccessTools.Method(typeof(SaveManager), "StartNewGamePatch");
                harmony.Patch(original4, new HarmonyMethod(patch4));
                //Moves the scenery with the island (when it shifts)
                MethodInfo original5 = AccessTools.Method(typeof(IslandSceneryScene), "Update");
                MethodInfo patch5 = AccessTools.Method(typeof(ModHorizon), "UpdatePatch");
                harmony.Patch(original5, new HarmonyMethod(patch5));

                //WIP IslandMarket / TraderBoat patches
                MethodInfo original6 = AccessTools.Method(typeof(IslandMarket), "Awake");
                MethodInfo patch6 = AccessTools.Method(typeof(PortIndexer), "MarketPatch");
                harmony.Patch(original6, new HarmonyMethod(patch6));

                MethodInfo original7 = AccessTools.Method(typeof(TraderBoat), "Start");
                MethodInfo patch7 = AccessTools.Method(typeof(PortIndexer), "TraderPatch");
                harmony.Patch(original7, new HarmonyMethod(patch7));

                //EconomyUI patch
                MethodInfo original8 = AccessTools.Method(typeof(EconomyUI), "OpenUI");
                MethodInfo patch8 = AccessTools.Method(typeof(PortIndexer), "EcoUIPatch");
                harmony.Patch(original8, new HarmonyMethod(patch8));
            }
            else
            {
                //Save cleaning Patch
                MethodInfo original9 = AccessTools.Method(typeof(SaveLoadManager), "LoadGame");
                MethodInfo patch9 = AccessTools.Method(typeof(SaveCleaner), "CleanSave");
                harmony.Patch(original9, new HarmonyMethod(patch9));
            }
            
            //debug
            MethodInfo debugOg = AccessTools.Method(typeof(GPButtonRopeWinch), "Update");
            MethodInfo debugPt = AccessTools.Method(typeof(ObservatoryPatches), "SaveablePrefabIndex");
            harmony.Patch(debugOg, new HarmonyMethod(debugPt));

            //Create config file in BepInEx\config\
        }
    }
    public class ObservatoryPatches
    {
        // variables 
        public static AssetBundle assetsBundle;     //for prefabs
        public static AssetBundle sceneBundle;      //for scenes
        public static string bundlePath;
        public static GameObject sextant;
        public static GameObject observatory;
        
        //debug var remove this
        public static bool printed;
        public static bool printed2;

        [HarmonyPrefix]
        public static void AddAssets(FloatingOriginManager __instance)
        {   //handles adding the new objects to the game (item and island)
            LoadFromBundle();
            Transform shiftingWorld = __instance.transform;
            MatLib.RegisterMaterials(shiftingWorld);

            //Add setxant
            AddSextantComponents();
            sextant = Object.Instantiate(sextant, shiftingWorld);
            sextant.transform.position = new Vector3(-48896.88f, 2.795f, -45943.67f);   //DEBUG: this is just temporary to have it appear in Neverdin

            //Add island
            AddIslandComponents(shiftingWorld);
            observatory = PortIndexer.InstatiateIsland(observatory, shiftingWorld);
        }
        // Helper methods
        private static void LoadFromBundle()
        {   // Load prefab from bundle

            bundlePath = Paths.PluginPath + "\\Observatory";
            assetsBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatory");
            sceneBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatoryscene");
            if (assetsBundle == null || sceneBundle == null)
            {   // Maybe the user downloaded from thunderstore...
                bundlePath = Paths.PluginPath + $"\\Pr0SkyNesis-Observatory-{ObservatoryMain.pluginVersion}";
                assetsBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatory");
                sceneBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatoryscene");
                if (assetsBundle == null || sceneBundle == null)
                {   // Maybe the user downloaded from thunderstore using the installer
                    bundlePath = Paths.PluginPath + $"\\Pr0SkyNesis-Observatory";
                    assetsBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatory");
                    sceneBundle = AssetBundle.LoadFromFile(bundlePath + "\\observatoryscene");
                    if (assetsBundle == null || sceneBundle == null)
                    {
                        Debug.LogError("Observatory: Bundle not loaded! Did you place it in the correct folder?");
                    }
                }
            }
            //LOAD ASSETS
            string sextantPath = "Assets/Sextant/sextant.prefab";
            sextant = assetsBundle.LoadAsset<GameObject>(sextantPath);

            string observatoryPath = "Assets/Observatory/Observatory MED.prefab";
            observatory = assetsBundle.LoadAsset<GameObject>(observatoryPath);
        }
        private static void AddSextantComponents()
        {   //add all necessary components to an item. It requires calling IndexManager.AssignAvailableIndex last

            sextant.AddComponent<ModItemSextant>();
            sextant.AddComponent<SaveablePrefab>();

            ItemIndexer.AssignAvailableIndex(sextant);
        }
        public static void AddIslandComponents(Transform sw)
        {   //adds any necessary component to Observatory Island. Also adds the island's destinations and the island as a destination
            observatory.AddComponent<ModHorizon>();

            //add possible missions destinations
            Port port = observatory.transform.Find("port " + observatory.name).GetComponent<Port>();
            Port[] destinations = port.GetDestinationPorts();

            Port fort = sw.Find("island 15 M (Fort)").Find("port M 15 Fort").GetComponent<Port>();
            Port sunspire = sw.Find("island 16 M (Sunspire)").Find("port M 16 Sunspire").GetComponent<Port>();
            Port malefic = sw.Find("island 17 M (Mount Malefic)").Find("port M 17 Mount Malefic").GetComponent<Port>();
            Port eastwind = sw.Find("island 19 M (Eastwind)").Find("port M 19 (Eastwind)").GetComponent<Port>();
            Port siren = sw.Find("island 21 M (Siren Song)").Find("port M 18 Siren Song").GetComponent<Port>();

            destinations[0] = fort;
            destinations[1] = sunspire;
            destinations[2] = malefic;
            destinations[3] = eastwind;
            destinations[4] = siren;
            
            PortIndexer.AddModdedPort(observatory);
            PortIndexer.InitialisePI();
        }
        // DEBUG methods
        [HarmonyPrefix]
        private static void SaveablePrefabIndex(GPButtonRopeWinch __instance)
        {
            if (!__instance.IsLookedAt() || printed) { return; }

            foreach (TraderBoat traderBoat in TraderBoat.traderBoats)
            {
                Debug.LogWarning("PI: traderBoat: " + traderBoat.name);
            }
            Debug.LogWarning($"Observatory: position is {FloatingOriginManager.instance.GetGlobeCoords(observatory.transform)}");

            printed = true;
        }
    }
}