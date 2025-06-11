using System.Collections.Generic;
using UnityEngine;

namespace Observatory
{   /// <summary>
    /// Handles modded items indexes
    /// NOTE: ItemIndexer key for modData is the pluginGuid
    /// </summary>
    public class ItemIndexer
    {
        private static Dictionary<string, int> loadedIndexMap = new Dictionary<string, int>();
        private static Dictionary<string, int> indexMap = new Dictionary<string, int>();

        public const string modDataKey = ObservatoryMain.shortName + ".items";

        public static void Manager(SaveContainer saveContainer)
        {   //this is what gets called from SaveManager.Manager()

            loadedIndexMap = new Dictionary<string, int>(SaveManager.LoadModData(saveContainer, modDataKey));
            SaveManager.ValidateIndexes(loadedIndexMap, indexMap);
        }
        public static SaveContainer UpdateSave(SaveContainer saveContainer)
        {   //goes through the save to update references to the old index to the new one
            Debug.LogWarning($"Observatory/ItemIndexer: Updating save {SaveSlots.currentSlot}...");
            foreach (string item in loadedIndexMap.Keys)
            {
                foreach (SavePrefabData savedPrefab in saveContainer.savedPrefabs)
                {
                    if (savedPrefab != null && savedPrefab.prefabIndex == loadedIndexMap[item])
                    {
                        savedPrefab.prefabIndex = indexMap[item];
                    }
                }
            }
            Debug.LogWarning($"Observatory/ItemIndexer: Done updating save {SaveSlots.currentSlot}");
            return saveContainer;
        }
        public static void AssignAvailableIndex(GameObject obj)
        {   // scans the PrefabDirectory for available indexes, adds the modded item to the directory and assigns it an index
            //INFO: the prefabDirectory is like a library of prefabs. When the game needs to spawn an objects it takes it from the library and spawns a copy
            //      On the other end the List of SaveablePrefabs is only used as a register of the item that will have to be saved eventually. 
            //      TL;DR: add the items to the directory and make sure that on the item SaveablePrefab RegisterToSave() is called (usually when sold)
            GameObject[] dir = SaveLoadManager.instance.GetComponent<PrefabsDirectory>().directory;
            for (int i = 1; i < dir.Length; i++) //let's skip index = 0for safety!!!
            {
                if (dir[i] == null)
                {
                    //Debug.LogWarning("IndexManager: available index found: " + i);
                    obj.GetComponent<SaveablePrefab>().prefabIndex = i;
                    SaveLoadManager.instance.GetComponent<PrefabsDirectory>().directory[i] = obj;
                    indexMap[obj.name] = i;
                    return;
                }
            }
        }
        public static void SaveItemData()
        {   //used by SaveIndex and StartNewGamePatch to save data to modData dictionary
            //this really should be moved to SaveManager and merged with SavePortData() (I can't bother)
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
    }
}
