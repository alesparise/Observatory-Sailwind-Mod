using HarmonyLib;
using PsychoticLab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Observatory
{   /// <summary>
    /// • Manage assigning PortIndexes to islands dynamically. 
    /// • Updates vanilla saves to work with the mod. 
    /// • Updates saves that have different port indexes
    /// NOTE: About missions, if the distance between origin and destination is more than 200 (miles I assume) missions will count as 'world'
    /// </summary>
    public class PortIndexer
    {
        public static int vanillaPorts;    //NOTE: if another mod adds port before this one, this value might count them, but this should not cause issues, right?
        public static int moddedPorts;
        private static int goods;           //this should be the number of goods in the vanilla game
        private const int whCapacity = 80;  //this is 80
        
        private static bool updatedRegions;
        private static bool initRegions;

        private static List<Port> modPorts = new List<Port>();

        public static Dictionary<string, int> loadedIndexMap = new Dictionary<string, int>();
        private static Dictionary<string, int> indexMap = new Dictionary<string, int>();

        private static string[] aaIslands = { "island 1 A (gold rock)", "island 2 A (AlNilem)", "island 3 A (Neverdin)", "island 4 A (fish island)", "island 7 A (alchemist's island)", "island 8 A (academy)", "island 20 A (Oasis)" };
        private static string[] emIslands = { "island 9 E (dragon cliffs)", "island 10 E (sanctuary)", "island 11 E (crab beach)", "island 12 E (New Port)", "island 13 E (Sage Hills)", "island 22 E (Serpent Isle)" };
        private static string[] aeIslands = { "island 15 M (Fort)", "island 16 M (Sunspire)", "island 17 M (Mount Malefic)", "island 18 M (HappyBay)", "island 19 M (Eastwind)", "island 21 M (Siren Song)" };
        private static string[] laIslands = { "island 26 Lagoon Temple", "island 27 Lagoon Shipyard", "island 28 Lagoon Senna", "island 29 Lagoon Onna" };

        public const string modDataKey = ObservatoryMain.shortName + ".islands";   //pr0.observatory.islands

        public static SaveContainer Manager(SaveContainer saveContainer)
        {   //this is what gets called from SaveManager.Manager()
            loadedIndexMap = SaveManager.LoadModData(saveContainer, modDataKey);
            SaveManager.ValidateIndexes(loadedIndexMap, indexMap);
            saveContainer = UpdateSave(saveContainer);
            
            return saveContainer;
        }
        public static void InitialisePI()
        {   //Initialises the PortIndexer by resizing all arrays and assigning indexes
            AssignIndexes();
            ResizeArrays();
        }
        private static void ResizeArrays()
        {   //resize the Port.ports and the knownPrices array
            //NOTE: knownPrices for the moddedPorts gets resized in MarketPatch()

            Array.Resize(ref Port.ports, vanillaPorts + moddedPorts);
            
            IslandMarket[] markets = Object.FindObjectsOfType<IslandMarket>();  //inefficient, but only runs once
            foreach (IslandMarket market in markets)
            {
                Array.Resize(ref market.knownPrices, vanillaPorts + moddedPorts);
                for (int i = 0; i < moddedPorts; i++)
                {
                    market.knownPrices[vanillaPorts + i] = new PriceReport();
                }
            }
            Debug.LogWarning("Observatory/PI: resized Port.ports and knownPrices arrays");
        }
        private static void GetVanillaPorts()
        {   //gets the number of 'vanilla' ports
            vanillaPorts = Object.FindObjectOfType<IslandMarket>().knownPrices.Length;
        }
        private static void AssignIndexes()
        {   //assings an available port index to the port
            int i = 0;  //use 0 as index because we are adding them to the top regardless...
            foreach (Port port in modPorts)
            {
                port.portIndex = vanillaPorts + i;
                indexMap[port.GetPortName()] = port.portIndex;  //indexMap is a dic like this 'Observatory:30'  //NOTE: portName is a field in the Port component, it's not the name of the port object!
                i++;
            }
        }
        public static void AddModdedPort(GameObject island)
        {   //assigns an available index to the modded port
            
            if (vanillaPorts == 0)
            {   //this is where we should get vanillaPorts
                GetVanillaPorts();
            }
            moddedPorts++;
            Port p = island.transform.Find("port " + island.name).GetComponent<Port>();
            modPorts.Add(p);
            Debug.LogWarning("Observatory/PI: added a modded port");
            //NOTE: right now modPorts contains the prefab ports, not the one that will be in the instantiated island. These ports are not functional and cannot be used for references later. They are updated in InstantiateIsland
        }
        public static GameObject InstatiateIsland(GameObject island, Transform shiftingWorld)
        {   // This is only needed if the island has a Port, otherwise just instantiate normally
            string islandName = island.name;    //the pre instantiated name does not contain (Clone)
            island = Object.Instantiate(island, shiftingWorld);
            Transform t = island.transform;
            Port p = t.Find("port " + islandName).GetComponent<Port>();
            modPorts[p.portIndex - vanillaPorts] = p;
            Port.ports[p.portIndex] = p;
            t.Find("port dude").Find("Modular NPC").GetComponent<CharacterCustomizer>().mat = MatLib.dudeMatM;   //this is the material for Medi npcs, can add checks for different npcs perhaps by renaming 'port dude' into something like 'port dude M/A/E' and applying the correct material based on that
            AddAsDestination(shiftingWorld, p, GetRegion(island.name));

            Debug.LogWarning("Observatory/PI: instantiated island " + island.name + ", with portIndex " + p.portIndex);

            return island;
        }
        private static string GetRegion(string name)
        {   //get the traders from a given region. This is based on the island/port name. Add ALA, EME, MED or LAG for the four regions. Add nothing for the long route traders. (this can be expanded with more regions)
            //NOTE: make sure the island name contains one of those if needed, BUT also make sure to not accidentally
            //name the island with something like that! e.g. an island called 'MEDORIA' would trigger the 'MED' check!
            //string.Contains() is case sensitive so an island called 'Medoria' would NOT trigger the check!
            if (name.Contains("ALA"))
            {   //Al Ankh traders
                return "(A)";
            }
            else if (name.Contains("EME"))
            {   //Emerald traders
                return "(E)";
            }
            else if (name.Contains("MED"))
            {   //Aestrin traders
                return "(M)";
            }
            else if (name.Contains("LAG"))
            {   //Lagoon traders
                return "(L)";
            }
            else
            {   //long circuit traders, the one moving between archipelagoes
                Debug.LogError("PortIndexer: assigning traders from LONG circuits, if this is not intended, there is an error in the name of the island");
                return "(long)";
            }
        }
        private static void AddAsDestination(Transform sw, Port island, string region)
        {   //adds the island as destination for each vanilla island (based on the regions)
            
            if (region == "(A)")
            {   //al ankh
                foreach (string isl in aaIslands)
                {
                    Port port = sw.Find(isl).GetComponentInChildren<Port>();
                    IslandMissionOffice imo = port.GetComponent<IslandMissionOffice>();
                    FieldInfo destInfo = AccessTools.Field(typeof(Port), "destinationPorts");
                    FieldInfo destInfo2 = AccessTools.Field(typeof(IslandMissionOffice), "destinationPorts");
                    Port[] dest = port.GetDestinationPorts();
                    Array.Resize(ref dest, dest.Length + 1);
                    dest[dest.Length - 1] = island;
                    destInfo.SetValue(port, dest);
                    destInfo2.SetValue(imo, dest);
                }
            }
            else if (region == "(E)")
            {   //emerald
                foreach (string isl in emIslands)
                {
                    Port port = sw.Find(isl).GetComponentInChildren<Port>();
                    IslandMissionOffice imo = port.GetComponent<IslandMissionOffice>();
                    FieldInfo destInfo = AccessTools.Field(typeof(Port), "destinationPorts");
                    FieldInfo destInfo2 = AccessTools.Field(typeof(IslandMissionOffice), "destinationPorts");
                    Port[] dest = port.GetDestinationPorts();
                    Array.Resize(ref dest, dest.Length + 1);
                    dest[dest.Length - 1] = island;
                    destInfo.SetValue(port, dest);
                    destInfo2.SetValue(imo, dest);
                }
            }
            else if (region == "(M)")
            {   //aestrin
                foreach (string isl in aeIslands)
                {
                    Port port = sw.Find(isl).GetComponentInChildren<Port>();
                    IslandMissionOffice imo = port.GetComponent<IslandMissionOffice>();
                    FieldInfo destInfo = AccessTools.Field(typeof(Port), "destinationPorts");
                    FieldInfo destInfo2 = AccessTools.Field(typeof(IslandMissionOffice), "destinationPorts");
                    Port[] dest = port.GetDestinationPorts();
                    Array.Resize(ref dest, dest.Length + 1);
                    dest[dest.Length - 1] = island;
                    destInfo.SetValue(port, dest);
                    destInfo2.SetValue(imo, dest);
                }
            }
            else if (region == "(L)")
            {   //lagoon
                foreach (string isl in laIslands)
                {
                    Port port = sw.Find(isl).GetComponentInChildren<Port>();
                    IslandMissionOffice imo = port.GetComponent<IslandMissionOffice>();
                    FieldInfo destInfo = AccessTools.Field(typeof(Port), "destinationPorts");
                    FieldInfo destInfo2 = AccessTools.Field(typeof(IslandMissionOffice), "destinationPorts");
                    Port[] dest = port.GetDestinationPorts();
                    Array.Resize(ref dest, dest.Length + 1);
                    dest[dest.Length - 1] = island;
                    destInfo.SetValue(port, dest);
                    destInfo2.SetValue(imo, dest);
                }
            }
        }
        public static SaveContainer UpdateSave(SaveContainer saveContainer)
        {   //update the save if necessary. It's necessary if loaded data is not valid OR if there is no data
            
            //↓Case where the user is trying to load a vanilla save (or a save with other modded ports, or if RL adds new ports)
            goods = saveContainer.marketsSupply[0].Length;      //get how many goods are there from the first port (currently it's 51)
            if (saveContainer.marketsSupply.Length < vanillaPorts + moddedPorts)
            {   //resize the marketSupply array and adds the modded ports

                Array.Resize(ref saveContainer.marketsSupply, vanillaPorts + moddedPorts);
                Array.Resize(ref saveContainer.marketsKnownPrices, vanillaPorts + moddedPorts);
                Array.Resize(ref saveContainer.missionWarehouses, vanillaPorts + moddedPorts);
                Array.Resize(ref saveContainer.portDemands, vanillaPorts + moddedPorts);
                Array.Resize(ref saveContainer.playerKnownPrices, vanillaPorts + moddedPorts);

                for (int i = 0; i < saveContainer.marketsSupply.Length; i++)
                {   
                    //MARKETS SUPPLY
                    if (saveContainer.marketsSupply[i] == null && indexMap.ContainsValue(i))
                    {   //this means marketSupply[i] is a modded port and we need to initialise the array (it's the first time)
                        saveContainer.marketsSupply[i] = new float[goods];
                    }
                    //MARKETS KNOWN PRICES
                    if (saveContainer.marketsKnownPrices[i] != null)
                    {   //also resize each price report to match the thing
                        Array.Resize(ref saveContainer.marketsKnownPrices[i], vanillaPorts + moddedPorts);
                        for (int j = 0; j < saveContainer.marketsKnownPrices[i].Length; j++)
                        {
                            if (saveContainer.marketsKnownPrices[i][j] == null && indexMap.ContainsValue(j))
                            {   //initialises the price report for a modded port (j index)
                                saveContainer.marketsKnownPrices[i][j] = new PriceReport();
                            }
                        }
                    }
                    else if (saveContainer.marketsKnownPrices[i] == null && indexMap.ContainsValue(i))
                    {   //this means marketsKnownPrices[i] is a modded port and we need to initialise the array (it's the first time)
                        saveContainer.marketsKnownPrices[i] = new PriceReport[vanillaPorts + moddedPorts];
                        for (int j = 0; j < saveContainer.marketsKnownPrices[i].Length; j++)
                        {   //cycle through the marketsKnownPrices[i] array and initialise the reports for non null ports                         if (saveContainer.marketsKnownPrices[0][j] != null)
                            {   //this checks for [0][j] for a good reason. [0] is a vanilla port and this ensures we only initialise the price reports for the non null ports (e.g. we skip port 7)
                                saveContainer.marketsKnownPrices[i][j] = new PriceReport();
                            }
                        }
                    }
                    //MISSION WAREHOUSES
                    if (saveContainer.missionWarehouses[i] == null && indexMap.ContainsValue(i))
                    {   //this means marketSupply[i] is a modded port and we need to initialise the array (it's the first time)
                        saveContainer.missionWarehouses[i] = new int[whCapacity];
                    }
                    //PORT DEMANDS
                    if (saveContainer.portDemands[i] == null && indexMap.ContainsValue(i))
                    {   //this means marketSupply[i] is a modded port and we need to initialise the array (it's the first time)
                        //saveContainer.portDemands[i] = new int[whCapacity];
                        //this was disabled. Why? DEBUG: test it and re-disable if needed
                        //DEBUG: this seems to be unnecessary
                        //REMOVE: should be safe to remove
                    }
                    //PLAYER KNOWN PRICES
                    if (saveContainer.playerKnownPrices[i] == null && indexMap.ContainsValue(i))
                    {
                        saveContainer.playerKnownPrices[i] = new PriceReport();
                    }
                }
            }
            for (int i = 0; i < saveContainer.traderBoatData.Length; i++)
            {   //go through all the saved traderBoatData and update the carriedPriceReports if they are too short
                if (saveContainer.traderBoatData[i].carriedPriceReports.Length < vanillaPorts + moddedPorts) 
                {
                    Array.Resize(ref saveContainer.traderBoatData[i].carriedPriceReports, vanillaPorts + moddedPorts);
                    saveContainer.traderBoatData[i].carriedPriceReports[vanillaPorts + moddedPorts - 1] = new PriceReport();
                }
            }
            //↓THIS IS FOR UPDATING AN EXISTING SAVE WITH NEW INDEXES
            //process is like this basically: 
            //1)create a reverse port - index map from indexMap (only once is enough)
            //2)copy the loaded arrays (already resized above or already loaded with the correct size) store them to temp arrays
            //3)loop through the loaded arrays and for each entry that matches the check below (portMap.ContainsKey() && loadedIndexMap.Count != 0)
            //  assign the value from the temp array using the loadedIndexMap[port] index as index

            float[][] msTemp = saveContainer.marketsSupply;
            PriceReport[][] mkpTemp = saveContainer.marketsKnownPrices;
            int[][] mwTemp = saveContainer.missionWarehouses;
            int[][] pdTemp = saveContainer.portDemands;
            PriceReport[] pkpTemp = saveContainer.playerKnownPrices;

            Dictionary<int, string> portMap = indexMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key); //flip the dic to an index - port name one
            for (int i = 0; i < saveContainer.marketsSupply.Length; i++)
            {   //we cycle through marketsSupply and if there is a modded port we assign the supply using the og values stored in temp
                if (portMap.ContainsKey(i) && loadedIndexMap.Count != 0)
                {   //this array all have the same length
                    
                    saveContainer.marketsSupply[i] = msTemp[loadedIndexMap[portMap[i]]];
                    saveContainer.marketsKnownPrices[i] = mkpTemp[loadedIndexMap[portMap[i]]];
                    saveContainer.missionWarehouses[i] = mwTemp[loadedIndexMap[portMap[i]]];
                    saveContainer.portDemands[i] = pdTemp[loadedIndexMap[portMap[i]]];
                    if (saveContainer.playerKnownPrices != null)
                    {
                        saveContainer.playerKnownPrices[i] = pkpTemp[loadedIndexMap[portMap[i]]];
                    }
                }
                //alternatively we could use a foreach loop going through port in IndexMap and use loadedIndexMap[port]. It would be slower beacause there are i * j iterations vs i + j iterations in this case
            }
            foreach (string port in loadedIndexMap.Keys)
            {   //needs to be checked for each modded port
                Debug.LogWarning("PI: Got here: foreach loop");
                if (saveContainer.lastVisitedPort == loadedIndexMap[port])
                {   //Update lastVisitedPort
                    saveContainer.lastVisitedPort = indexMap[port];
                }
                if (saveContainer.savedMissions != null)
                {
                    foreach (SaveMissionData md in saveContainer.savedMissions)
                    {   //Update the missions data
                        if (md == null) continue;
                        if (md.originPort == loadedIndexMap[port])
                        {
                            md.originPort = indexMap[port];
                        }
                        if (md.destinationPort == loadedIndexMap[port])
                        {
                            md.destinationPort = indexMap[port];
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("PI: savedMissions is null!");
                }
            }

            return saveContainer;
        }
        public static void SavePortData()
        {   //used by SaveIndex and StartNewGamePatch to save data to modData dictionary
            //this really could be moved to SaveManager and merged with SaveItemData (I can't bother)
            GameState.modData[modDataKey] = "";
            foreach (string name in indexMap.Keys)
            {
                string entry = name.ToString() + ":" + indexMap[name].ToString() + ";"; //name:1;
                if (GameState.modData.ContainsKey(modDataKey))
                {
                    GameState.modData[modDataKey] += entry;
                }
                else
                {
                    GameState.modData[modDataKey] = entry;
                }
            }
        }

        //PATCHES
        [HarmonyPrefix]
        private static bool MarketPatch(IslandMarket __instance, ref Port ___port, ref IslandMarketWarehouseArea ___warehouseArea, ref float ___econTimer)
        {   //this is necessary because otherwise the Awake() method generates a 30 long priceReport, which overwrites any longer pricereport and in turns makes Awake fail at the last line.
            //when Awake() fails Unity disables the component, meaning IslandMarket would not be enabled on modded ports!
            //NOTE: bool patches are dangerous because updates to the original method would be skipped. In case the mod breaks after a game update check this

            Port p = __instance.GetComponent<Port>();
            if (p != null && indexMap.ContainsValue(p.portIndex))
            {   //this is a modded port, let's run an alternative Awake and skip all stuff
                ___port = p;
                ___warehouseArea = __instance.GetComponent<IslandMarketWarehouseArea>();
                __instance.currentSupply = __instance.production.Clone() as float[];
                __instance.currentPlayerGoods = new int[__instance.production.Length];
                ___econTimer = 1f;
                __instance.knownPrices = new PriceReport[vanillaPorts + moddedPorts];
                __instance.knownPrices[p.portIndex] = new PriceReport();

                return false;
            }
            return true;
        }
        [HarmonyPrefix]
        private static void TraderPatch(TraderBoat __instance)
        {   //patches Start(). This is to add the modded port to the invisible trader traderoutes. 
            //currently it uses a the island object name (and thus the port name) to get the relevant group of traders
            foreach (Port p in modPorts)
            {
                if (__instance.name.Contains(GetRegion(p.name)))
                {   //resize the destination and add the port if it matches the regional loop.
                    Array.Resize(ref __instance.destinations, __instance.destinations.Length + 1);
                    __instance.destinations[__instance.destinations.Length - 1] = p.GetComponent<IslandMarket>();
                }
                Array.Resize(ref __instance.carriedPriceReports, __instance.carriedPriceReports.Length + 1);
                __instance.carriedPriceReports[__instance.carriedPriceReports.Length - 1 ] = new PriceReport();
            }
        }
        [HarmonyPrefix]
        private static void EcoUIPatch(int[][] ___bookmarkIslands)
        {   //make sure the size of the bookmarks array is correct in the EconomyUI. Also adds the modded ports to the correct region array
            
            if (updatedRegions) { return; }
            foreach (Port p in modPorts)
            {
                if (p.name.Contains("ALA") && !___bookmarkIslands[0].Contains(p.portIndex))
                {
                    Array.Resize(ref ___bookmarkIslands[0], ___bookmarkIslands[0].Length + 1);
                    ___bookmarkIslands[0][___bookmarkIslands[0].Length - 1] = p.portIndex;
                }
                if (p.name.Contains("EME") && !___bookmarkIslands[1].Contains(p.portIndex))
                {
                    Array.Resize(ref ___bookmarkIslands[1], ___bookmarkIslands[1].Length + 1);
                    ___bookmarkIslands[1][___bookmarkIslands[1].Length - 1] = p.portIndex;
                }
                if (p.name.Contains("MED") && !___bookmarkIslands[2].Contains(p.portIndex))
                {
                    Array.Resize(ref ___bookmarkIslands[2], ___bookmarkIslands[2].Length + 1);
                    ___bookmarkIslands[2][___bookmarkIslands[2].Length - 1] = p.portIndex;
                }
                if (p.name.Contains("LAG") && !___bookmarkIslands[3].Contains(p.portIndex))
                {
                    Array.Resize(ref ___bookmarkIslands[3], ___bookmarkIslands[3].Length + 1);
                    ___bookmarkIslands[3][___bookmarkIslands[3].Length - 1] = p.portIndex;
                }
            }
            updatedRegions = true;
        }
    }
}
