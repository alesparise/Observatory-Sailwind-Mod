using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Observatory
{   /// <summary>
    /// Helper class for classes that need to manipulate saves
    /// • SaveCleaner
    /// • PortIndexer
    /// • ItemIndexer
    /// TODO: 
    /// • Add savecleaner
    /// handles assigning a prefabIndex dynamically to the object, making sure it uses a free one while persisting
    /// through saves, regardless of other mods and updates adding more items
    /// </summary>

    internal class SaveManager
    {
        private static bool updateSave;      //to know if the save should be updated or not.

        //mod version
        public static string loadedVersion;

        //PATCHES
        [HarmonyPriority(400)]  //this should make sure Manager() runs before SaveCleaner()
        [HarmonyPrefix]
        private static void Manager(int backupIndex)
        {   //runs the necessary methods in order when LoadGame() is called
            string path = ((backupIndex != 0) ? SaveSlots.GetBackupPath(SaveSlots.currentSlot, backupIndex) : SaveSlots.GetCurrentSavePath());
            SaveContainer saveContainer;
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (FileStream fileStream = File.Open(path, FileMode.Open))
            {   //unpack the save to access the saveContainer
                saveContainer = (SaveContainer)binaryFormatter.Deserialize(fileStream);
            }
            ReadVersion(saveContainer);
            saveContainer = PortIndexer.Manager(saveContainer);
            ItemIndexer.Manager(saveContainer);
            if (updateSave)
            {
                saveContainer = ItemIndexer.UpdateSave(saveContainer);
                using (FileStream fileStream = File.Open(path, FileMode.Create))
                {   //pack the savecontainer back into the save
                    binaryFormatter.Serialize(fileStream, saveContainer);
                }
            }
        }
        [HarmonyPostfix]
        public static void SaveIndex()
        {   //adds the itemName:index data to the modData dictionary
            //this runs toward the end of the LoadGame() method (it patches LoadModData())

            if (!GameState.currentlyLoading)
            {
                //Debug.LogWarning("IndexManager: currently not loading...");
                return;
            }
            GameState.modData[ObservatoryMain.pluginGuid] = "";
            ItemIndexer.SaveItemData();
            PortIndexer.SavePortData();
            SaveModVersion();
        }
        [HarmonyPostfix]
        public static void StartNewGamePatch()
        {   //we need to save the modData if this is a newgame!
            ItemIndexer.SaveItemData();
            PortIndexer.SavePortData();
            SaveModVersion();
        }

        //SAVE MANIPULATION
        public static Dictionary<string, int> LoadModData(SaveContainer saveContainer, string key)
        {   //Loads mod data from saveContainer
            Dictionary<string, int> dic = new Dictionary<string, int>();
            if (saveContainer.modData.ContainsKey(key))
            {
                //Debug.LogWarning("SaveManager: saved data is loading");
                string data = saveContainer.modData[key];
                string[] entries = data.Split(';');                         //entries is now an array like: ["item1:1","item2:2",...]
                
                for (int i = 0; i < entries.Length - 1; i++)
                {
                    string itemName = entries[i].Split(':')[0];             //itemName is a string like: "item1"
                    int itemIndex = int.Parse(entries[i].Split(':')[1]);    //itemIndex is an int like: 1

                    dic[itemName] = itemIndex;
                }
            }
            else
            {
                //Debug.LogWarning("SaveManager: No mod data saved...");
            }
            return dic;
        }
        private static void ReadVersion(SaveContainer saveContainer)
        {   //get mod version from modData (could be useful)
            if (saveContainer.modData.ContainsKey(ObservatoryMain.shortName + ".version"))
            {
                loadedVersion = saveContainer.modData[ObservatoryMain.shortName + ".version"];
            }
        }
        // UTILITIES
        public static void ValidateIndexes(Dictionary<string, int> loadedData, Dictionary<string, int> mappedData)
        {   //goes through all entries in the first dictionary and checks if they are still valid with the second dictionary
            //if at least one entry is no longer valid we set updateSave to true
            foreach (string item in mappedData.Keys)
            {
                if (loadedData.ContainsKey(item))
                {
                    if (loadedData[item] == mappedData[item])
                    {
                        //Debug.LogWarning($"IndexManager: {item} index is still valid: {indexMap[item]}");
                    }
                    else
                    {
                        //Debug.LogWarning($"IndexManager: {item} index is no longer valid, update required!");
                        updateSave = true;
                    }
                }
                else
                {   //in the case of islands, we need to update the save in the case the data is not in the save at all
                    updateSave = true;
                    //Debug.LogWarning($"IndexManager: {item} was not on the list before...");
                }
            }
        }
        private static void SaveModVersion()
        {   //Add mod version informations
            GameState.modData[ObservatoryMain.shortName + ".version"] = ObservatoryMain.pluginVersion;
        }
    }
}
