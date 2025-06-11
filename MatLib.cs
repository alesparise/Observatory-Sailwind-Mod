using PsychoticLab;
using UnityEngine;

namespace Observatory
{   /// <summary>
    /// Register the materials that don't get unpacked correctly so that we can use them later
    /// </summary>
    internal class MatLib
    {
        public static Material dudeMatM;

        public static void RegisterMaterials(Transform sw)
        {   //get the materials, run this in Main.AddAssets()
            dudeMatM = sw.Find("island 15 M (Fort)").Find("port dude (1)").Find("Modular NPC").GetComponent<CharacterCustomizer>().mat;
        }
    }
}
