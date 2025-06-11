using HarmonyLib;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Observatory
{   /// <summary>
    /// Cleans the save so that the mod can be removed safely.
    /// </summary>
    public class SaveCleaner
    {   
        public static Dictionary<string, int> portIndexMap = new Dictionary<string, int>(); //this are the loadedIndexMap of PortIndexer
        public static Dictionary<string, int> itemIndexMap = new Dictionary<string, int>(); //and ItemIndexer respectively

        [HarmonyPriority(300)]
        [HarmonyPrefix]
        public static void CleanSave(int backupIndex)
        {   //It cleans the save. Note that if no modded item is saved (bought) and no current missions are from or to a modded island, it might be possible to remove the mod without running the cleaner
            Debug.LogWarning("Observatory/SaveCleaner: cleaning save...");
            string path = ((backupIndex != 0) ? SaveSlots.GetBackupPath(SaveSlots.currentSlot, backupIndex) : SaveSlots.GetCurrentSavePath());
            SaveContainer saveContainer;
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (FileStream fileStream = File.Open(path, FileMode.Open))
            {   //unpack the save to access the saveContainer
                saveContainer = (SaveContainer)binaryFormatter.Deserialize(fileStream);
            }
            //load dictionaries from modData
            portIndexMap = SaveManager.LoadModData(saveContainer, PortIndexer.modDataKey);  //ports
            itemIndexMap = SaveManager.LoadModData(saveContainer, ItemIndexer.modDataKey);  //items

            //remove the lastVisitedPort
            saveContainer.lastVisitedPort = 0;  //should be GRC

            //remove islands (missions & TraderBoatData)
            for (int i = 0; i < saveContainer.savedMissions.Length; i++)
            {
                if (saveContainer.savedMissions[i] == null) continue;
                if (portIndexMap.ContainsValue(saveContainer.savedMissions[i].originPort) || portIndexMap.ContainsValue(saveContainer.savedMissions[i].destinationPort))
                {   //means the mission was from or to a modded port that was removed
                    saveContainer.savedPrefabs.RemoveAll(prefab => prefab.itemMissionIndex == saveContainer.savedMissions[i].missionIndex);
                }
                Debug.LogWarning("SC: cleaned mission " + i);
                saveContainer.savedMissions[i] = null;
            }
            for (int i = 0; i < saveContainer.traderBoatData.Length; i++)
            {   //we swap around a the port indexes: This might be problematic if a trader circuits between multiple modded ports. Might be more straightforward to set everything to 0 or to the index of the first destination available for the traderBoat
                //NOTE: this is definetely not complete! Better to have a fix for multiple modded port!
                //NOTE: this also does not take into account modded goods traded by the traderBoats
                if (portIndexMap.ContainsValue(saveContainer.traderBoatData[i].currentDestination))
                {   //means the boat is, is going or was in a modded port
                    saveContainer.traderBoatData[i].currentDestination = saveContainer.traderBoatData[i].lastIslandMarket;
                }
                else if (portIndexMap.ContainsValue(saveContainer.traderBoatData[i].currentIslandMarket))
                {
                    saveContainer.traderBoatData[i].currentIslandMarket = saveContainer.traderBoatData[i].lastIslandMarket;
                }
                else if (portIndexMap.ContainsValue(saveContainer.traderBoatData[i].lastIslandMarket))
                {
                    saveContainer.traderBoatData[i].lastIslandMarket = saveContainer.traderBoatData[i].currentIslandMarket;
                }
            }
            //remove items
            saveContainer.savedPrefabs.RemoveAll(p => itemIndexMap.ContainsValue(p.prefabIndex));
            //resize the marketSupply array
            Array.Resize(ref saveContainer.marketsSupply, Port.ports.Length);
            
            using (FileStream fileStream = File.Open(path, FileMode.Create))
            {   //pack the savecontainer back into the save
                binaryFormatter.Serialize(fileStream, saveContainer);
            }
            Debug.LogWarning("Observatory/SaveCleaner: save cleaned!");
        }
    }
}
