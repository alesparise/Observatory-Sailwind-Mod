using Crest;
using HarmonyLib;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Observatory
{   /// <summary>
    /// Controls loading / unloading the scene for modded island, controls changing the waves _attenuationInShallows value 
    /// </summary>
    public class ModHorizon : MonoBehaviour
    {
        public static ModHorizon instance;
        private IslandHorizon island;
        private SimSettingsAnimatedWaves settings;
        private FieldInfo attInfo;

        public int modHorizonIndex;

        private float distance;
        private float loadDistSqr;
        private float vanillaAtt;
        private const float modAtt = 0.96f;
        private const float sceneryOffset = 120f;   //for observatory

        private bool attenuatedWaves;
        private bool sceneLoaded;
        private bool unloading;

        private string sceneName;

        private Transform player;
        private Vector3 lastParentPos;

        private void Awake()
        {
            instance = this;
            //modHorizonIndex = -1 * island.islandIndex;
            island = GetComponent<IslandHorizon>();
            player = GameObject.FindGameObjectWithTag("Player").transform;
            loadDistSqr = island.islandLoadDistance * island.islandLoadDistance;
            sceneName = name.Split('(')[0] + "Scene";   //split off the (Clone) part and add Scene. Keep this naming convention in the editor!

            //Wave attenuation
            settings = GameObject.Find("Ocean (Crest)").GetComponent<OceanRenderer>()._simSettingsAnimatedWaves;
            attInfo = AccessTools.Field(typeof(SimSettingsAnimatedWaves), "_attenuationInShallows");
            vanillaAtt = settings.AttenuationInShallows;
            
            Debug.LogWarning("Observatory/MH: awakened correctly");
        }
        private void LateUpdate()
        {
            GetDistance();
            if (distance < loadDistSqr)
            {
                if (!attenuatedWaves)
                {
                    AttenuateWaves();
                    Debug.LogWarning("ModHorizon: attenuatedWaves: " + settings.AttenuationInShallows);
                }
                if (!sceneLoaded)
                {
                    LoadScene();
                }
            }
            else
            {
                if (attenuatedWaves)
                {
                    ResetAttenuation();
                    Debug.LogWarning("ModHorizon: resetAttenuation: " + settings.AttenuationInShallows);
                }
                if (sceneLoaded)
                {
                    UnloadScene();
                }
            }
        }

        private void GetDistance()
        {   
            distance = (transform.position - player.transform.position).sqrMagnitude;
            //Debug.LogWarning("ModHorizon: distance " + distance + " / " + loadDistSqr);
            
            //alternative with full distance calculation (not really necessary, the sqr is enough)
            //distance = Vector3.Distance(transform.position, player.transform.position);
            //Debug.LogWarning("ModHorizon: distance " + distance + " / " + island.islandLoadDistance);
        }
        private void LoadScene()
        {   //load the modded scene
            Debug.LogWarning("ModHorizon: Loading scene for " + name);
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            GameState.loadingScenes++;
            StartCoroutine(RegisterLoadingFinished());
            sceneLoaded = true;
        }
        private void UnloadScene()
        {   //unload the modded scene
            Debug.LogWarning("ModHorizon: Unloading scene for " + name);
            if (!unloading)
            {
                unloading = true;
                StartCoroutine(DoUnloadScene());
            }

            sceneLoaded = false;
        }
        private IEnumerator RegisterLoadingFinished()
        {
            while (!SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                yield return new WaitForEndOfFrame();
            }
            GameState.loadingScenes--;
        }
        private IEnumerator DoUnloadScene()
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(sceneName);
            while (unload != null && !unload.isDone)
            {
                yield return new WaitForEndOfFrame();
            }
            yield return new WaitForEndOfFrame();
            sceneLoaded = false;
            unloading = false;
        }
        private void AttenuateWaves()
        {   //when we are close to the island, set attenuation to 0.96 to make sure waves are attenuated
            attInfo.SetValue(settings, modAtt);
            attenuatedWaves = true;
        }
        private void ResetAttenuation()
        {   //when we are far from the island reset the attenuation to the vanilla value
            attInfo.SetValue(settings, vanillaAtt);
            attenuatedWaves = false;
        }

        //PATCHES
        [HarmonyPrefix]
        private static bool UpdatePatch(IslandSceneryScene __instance)
        {
            if (__instance.parentIslandIndex != instance.island.islandIndex)
            {
                return true;
            }
            else
            {
                //DEBUG: I can remove the offset this way: make the scenery a child of an object that is centered on the island without a y offset!
                //it will be cleaner and more versitile than having that thing.
                //also, what happens if I have more ModHorizon classes? I need to store the instances in a List or array.
                //then I can associate the correct IslandScene with the correct ModHorizon. Maybe I should do that in the PortIndexManager?
                Vector3 pos = instance.transform.position - new Vector3(0f, sceneryOffset, 0f);
                if (instance.lastParentPos != pos)
                {
                    __instance.transform.position = pos;
                }
                instance.lastParentPos = pos;
                return false;
            }
        }
    }
}
