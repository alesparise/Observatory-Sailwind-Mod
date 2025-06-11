using UnityEngine;
using System.Collections;
using UnityEngine.PostProcessing;
using HarmonyLib;
using System.Reflection;

namespace Observatory
{   /// <summary>
    /// NOTE: to get saved, the item needs to have run SaveablePrefab.RegisterToSave() (or have otherwise been added
    /// to the SaveablePrefab list. This happens when the item is sold, so there should be no need to make a custom
    /// system to do that! (Unless I spawn the item as 'sold' in other ways!)
    /// TODO: Remove magnifier?
    /// Tweak inspecting position/rotation?
    /// Texture
    /// Tweak cloudiness values?
    /// Find a way to make readings easier to get
    /// </summary>
    public class ModItemSextant : ShipItem
    {   // Handles the sextant custom behaviours
        
        private Transform index;
        private Transform frame;
        private Transform shade1;
        private Transform shade2;
        private Transform shade3;
        private Transform shade4;
        private Transform shade5;
        private Transform shade6;

        private float indexAngle = 0f;

        private int shadeLevel = 0;

        private bool moving;
        private bool inspecting;
        private bool inInventory;
        static bool constructed;

        //ROTATIONS
        //Whole sextant
        private Quaternion initialRot;
        private Quaternion inspectRot;
        //shade1
        private Quaternion closedRot1;
        private Quaternion openRot1;
        //shade2
        private Quaternion closedRot2;
        private Quaternion openRot2;
        //shade3
        private Quaternion closedRot3;
        private Quaternion openRot3;
        //shade4
        private Quaternion closedRot4;
        private Quaternion openRot4;
        //shade5
        private Quaternion closedRot5;
        private Quaternion openRot5;
        //shade6
        private Quaternion closedRot6;
        private Quaternion openRot6;

        //POSITIONS
        private Vector3 inspectPos = new Vector3(0.25f, 0.2f, 0.2f);
        private Vector3 inventoryPos = new Vector3(0f, 0.18f, -0.22f);

        private Camera indexCam;
        private Camera horizonCam;
        private Camera ocularCam;
        private Camera magnifierCam;

        private Renderer indexRenderer;
        private Renderer horizonRenderer;
        private Renderer ocularRenderer;
        private Renderer magnifierRenderer;

        private Material telescopeMat;
        private Material indexMat;
        private Material horizonMat;
        private Material ocularMat;
        private Material magnifierMat;

        private PostProcessingProfile postProcessing;


        // CONSTRUCTOR
        private ModItemSextant()
        {   //this assigns a couple of the values BEFORE ShipItem.Awake() is called. THIS IS GENIUS!
            //NOTE: this only works well for static things, don't use it to initialise references to part of the object
            //they will reference only the og parts (as in the loaded object from the bundle if you make them static
            //or be null if you don't! NOT THAT GENIUS :(
            //Use OnLoad() to initialise those (To be fair this is kinda genius still!!!)
            if (!constructed)
            {
                value = 32000;      //DEBUG: Adjust this
                sold = true;    //DEBUG: this is for testing purposes
                name = "sextant";
                category = TransactionCategory.toolsAndSupplies;
                inventoryScale = 0.75f;
                inventoryRotation = 90f;
                holdDistance = 0.159f;
                furniturePlaceHeight = 0.5f;
                constructed = true;
                Debug.LogWarning("Sextant: called the constructor...");
            }
        }
        // SPECIAL METHODS
        public override void OnAltActivate()
        {   //what happens when right clicking the item
            if (!sold)
            {
                base.OnAltActivate();
            }
            else
            {
                if (!held)
                {
                    return;
                }
                if (moving)
                {
                    return;
                }
                inspecting = !inspecting;
                if (!moving)
                {
                    if (inspecting)
                    {
                        StartCoroutine(RotateAndMove(inspectRot, inspectPos));
                    }
                    else
                    {
                        StartCoroutine(RotateAndMove(initialRot, Vector3.zero));
                    }
                }
            }
        }
        public override void OnLoad()
        {   //kinda like an Awake() method, initialise here all NON static fields
            //transforms
            frame = transform.GetChild(0);
            index = frame.Find("index_bar");
            shade1 = frame.Find("shades_holder").GetChild(0);
            shade2 = frame.Find("shades_holder").GetChild(1);
            shade3 = frame.Find("shades_holder").GetChild(2);
            shade4 = frame.Find("shades_holder_2").GetChild(0);
            shade5 = frame.Find("shades_holder_2").GetChild(1);
            shade6 = frame.Find("shades_holder_2").GetChild(2);

            //rotations //DEBUG: I think we can get away with just two rotations values for the open / close position...
            initialRot = frame.localRotation;
            inspectRot = initialRot * Quaternion.Euler(Vector3.up * -90f);
            
            closedRot1 = shade1.localRotation;
            closedRot2 = shade2.localRotation;
            closedRot3 = shade3.localRotation;
            closedRot4 = shade4.localRotation;
            closedRot5 = shade5.localRotation;
            closedRot6 = shade6.localRotation;
            
            openRot1 = closedRot1 * Quaternion.Euler(Vector3.up * 160f);
            openRot2 = closedRot2 * Quaternion.Euler(Vector3.up * 160f);
            openRot3 = closedRot3 * Quaternion.Euler(Vector3.up * 160f);
            openRot4 = closedRot4 * Quaternion.Euler(Vector3.up * 120f);    //the horizon shades need less rotation?
            openRot5 = closedRot5 * Quaternion.Euler(Vector3.up * 120f);
            openRot6 = closedRot6 * Quaternion.Euler(Vector3.up * 120f);

            //cameras
            indexCam = index.GetChild(0).GetChild(0).GetComponentInChildren<Camera>();
            horizonCam = frame.GetChild(0).GetComponentInChildren<Camera>();
            ocularCam = frame.GetChild(2).GetComponentInChildren<Camera>();
            magnifierCam = index.GetChild(1).GetChild(1).GetComponentInChildren<Camera>();
            indexCam.enabled = false;
            horizonCam.enabled = false;
            ocularCam.enabled = false;
            magnifierCam.enabled = false;

            //renderers & materials
            indexRenderer = indexCam.GetComponentInParent<Renderer>();
            horizonRenderer = horizonCam.transform.parent.Find("horizon_glass_mirror").GetComponent<Renderer>();
            ocularRenderer = ocularCam.transform.parent.Find("ocular_view").GetComponent<Renderer>();
            magnifierRenderer = magnifierCam.GetComponentInParent<Renderer>();

            telescopeMat = ocularCam.transform.parent.Find("ocular_glass").GetComponent<Renderer>().sharedMaterial;
            indexMat = indexRenderer.sharedMaterial;
            horizonMat = horizonRenderer.sharedMaterial;
            ocularMat = ocularRenderer.sharedMaterial;
            magnifierMat = magnifierRenderer.sharedMaterial;

            indexRenderer.sharedMaterial = telescopeMat;
            horizonRenderer.sharedMaterial = telescopeMat;
            ocularRenderer.sharedMaterial = telescopeMat;
            magnifierRenderer.sharedMaterial = telescopeMat;

            //other
            postProcessing = FindObjectOfType<PlayerAlcohol>().postProcessing;
        }
        public override void OnEnterInventory()
        {   //offset the frame so that it looks good in the inventory
            inInventory = true;
            frame.localRotation = initialRot;
            frame.localPosition = inventoryPos;
            inspecting = false;
        }
        public override void OnLeaveInventory()
        {   // revert the offset of the fram when leaving the inventory
            inInventory = false;
            frame.localPosition = Vector3.zero;
        }
        public override void OnPickup()
        {   //enable all cams when picked up (includes taking out of the inventory)
            base.OnPickup();
            indexCam.enabled = true;
            horizonCam.enabled = true;
            ocularCam.enabled = true;
            magnifierCam.enabled = true;
            SwapCamMaterial(false);
        }
        public override void OnDrop()
        {   //when dropped reset the sextant position so it's not wonky
            if (!sold)
            { 
                ReturnToShopPos();
            }
            else
            {
                if (!inInventory)
                {
                    //Debug.LogWarning("Sextant: Dropped!");
                    frame.localRotation = initialRot;
                    frame.localPosition = Vector3.zero;
                    inspecting = false;
                }
            }
            //disables cameras so that you can have multiple sextants
            indexCam.enabled = false;
            horizonCam.enabled = false;
            ocularCam.enabled = false;
            magnifierCam.enabled = false;
            SwapCamMaterial(true);
        }
        public override void OnScroll(float input)
        {   //move the index bar if we are not inspecting
            if (moving)
            {   //do nothing if something is moving!
                return;
            }
            if(!inspecting)
            {   //adjust the index bar with the scrollwheel if not inspecting
                indexAngle += input * -0.25f;   //inverting scroll direction so it makes more sense
                indexAngle = LimitIndex(indexAngle);
                index.localEulerAngles = new Vector3(indexAngle, 0f, 0f);
                UISoundPlayer.instance.PlayOpenSound();
            }
            else
            {   // in inspecting mode we change the shades with the scroll wheel
                if (input < 0f)
                {
                    //close one open shade
                    StartCoroutine(MoveShade(false));
                }
                else if ( input > 0f)
                {
                    //open one closed shade
                    StartCoroutine(MoveShade(true));
                }
            }
        }
        private void Update()
        {   //used to check if the player should be blinded
            if (held && !inspecting)
            {
                UpdateIndexCam();
                if (Sun.sun.localTime > 18f || Sun.sun.localTime < 6f)
                {   // if it's night, we don't need to blind the player in any case
                    return;
                }
                float angle = MirrorSunAngle();
                if (angle < 35f)
                {
                    BlindPlayer(angle);
                }
            }
            else
            {
                //nothing
            }
        }

        //CUSTOM METHODS
        private float MirrorSunAngle()
        {   //returns true if looking toward the sun, false if not
            Vector3 sunDirection = -Sun.sun.transform.forward;
            Vector3 sextantDirection = indexCam.transform.forward;
            
            float angle = Vector3.Angle(sunDirection, sextantDirection);

            //Debug.LogWarning("Sextant: " + angle);
            return angle;
        }
        private void BlindPlayer(float angle)
        {
            if (DetectWeather() >= shadeLevel)
            {   //(3: no shades closed, 0: all shades closed)
                return;
            }
            float t;
            if ( angle >= 35f)
            {
                t = 0;
            }
            else
            {
                t = 1 - angle / 35f;
            }

            //Bloom
            BloomModel.Settings bloomSettings = postProcessing.bloom.settings;
            bloomSettings.bloom.threshold = Mathf.Lerp(1.05f, 0f, t);
            bloomSettings.bloom.radius = Mathf.Lerp(2.5f, 5f, t);
            postProcessing.bloom.settings = bloomSettings;

            //Color Grading
            ColorGradingModel.Settings colorSettings = postProcessing.colorGrading.settings;
            colorSettings.basic.postExposure = Mathf.Lerp(0.66f, -1f, t);
            postProcessing.colorGrading.settings = colorSettings;
        }
        private int DetectWeather()
        {   //returns the shade level needed for the current cloudiness (0: all shades closed, 3: no shades closed)

            //use reflection to get the cloud density, no other way to get that value!
            //DEBUG: The FieldInfo should be cached in OnLoad(), no need to do it everytime here...
            FieldInfo finalParticlesInfo = AccessTools.Field(typeof(Weather), "finalParticles");
            WeatherParticlesSettings finalParticles = (WeatherParticlesSettings)finalParticlesInfo.GetValue(Weather.instance);
            float cloudsDensity = finalParticles.cloudDensity;
            //Debug.LogWarning("Debug: cloudDensity: " + cloudsDensity);  //DEBUG: this is going to be needed to find the values to tweak this stuff
            
            if (cloudsDensity < 0.1f)
            {   //clear weather
                return 0;
            }
            if (cloudsDensity >= 0.1f && cloudsDensity < 0.5f)
            {   //cloudy weather
                return 1;
            }
            if (cloudsDensity >= 0.5f && cloudsDensity < 1f)
            {   //cloudier weather
                return 2;
            }
            if (cloudsDensity >= 1f)
            {   //stormy weather
                return 3;
            }
            return 0;
        }
        private void UpdateIndexCam()
        {   //rotate indexCam as the indexBar moves
            //indexCam needs to move to simulate the double mirror effect, which doubles the angles on the sextant.
            //this means that to measure an object's altitude of 20° the index bar should only move 10°.

            float offsetAngle = indexAngle - 70f;
            Quaternion targetRot = Quaternion.Euler(offsetAngle, 0f, 90f);
            indexCam.transform.localRotation = targetRot;

            //debug (this is the exact sun altitude that should be measured with the sextant)
            Vector3 sunDirection = -Sun.sun.transform.forward;
            float sunAltitude = Mathf.Rad2Deg * Mathf.Asin(sunDirection.y);
            Debug.LogWarning("Sextant: sunAltitude = " + sunAltitude + "°");
        }
        
        // UTILITIES
        private IEnumerator RotateAndMove(Quaternion targetRot, Vector3 targetPos)
        {   //rotates and moves the sextant between the inspecting and !inspecting positions
            moving = true;
            Quaternion startRot = frame.localRotation;
            Vector3 startPos = frame.localPosition;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                frame.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                frame.localPosition = Vector3.Slerp(startPos, targetPos, t);
                yield return new WaitForEndOfFrame();
            }
            frame.localRotation = targetRot;
            frame.localPosition = targetPos;
            moving = false;
        }
        private IEnumerator MoveShade(bool open)
        {   //opens and closes the shades. Pass true to use opening logic, pass false to use closing logic
            moving = true;
            for (float t = 0f; t <= 1f; t += Time.deltaTime * 5f)
            {
                if (!open)
                {
                    if (shadeLevel == 0)
                    {   //shade1/4 closed, open shade1/4
                        shade1.localRotation = Quaternion.Slerp(closedRot1, openRot1, t);
                        shade4.localRotation = Quaternion.Slerp(closedRot4, openRot4, t);
                    }
                    else if (shadeLevel == 1)
                    {   //shade2/5, shade1/4 closed, open shade2/5
                        shade2.localRotation = Quaternion.Slerp(closedRot2, openRot2, t);
                        shade5.localRotation = Quaternion.Slerp(closedRot5, openRot5, t);
                    }
                    else if (shadeLevel == 2)
                    {   //shade3/6, shade2/5, shade1/4 closed, open shade3/6
                        shade3.localRotation = Quaternion.Slerp(closedRot3, openRot3, t);
                        shade6.localRotation = Quaternion.Slerp(closedRot6, openRot6, t);
                    }
                }
                else
                {
                    if (shadeLevel == 3)
                    {   //shade1/4, shade2/5 closed, close shade3/6
                        shade3.localRotation = Quaternion.Slerp(openRot3, closedRot3, t);
                        shade6.localRotation = Quaternion.Slerp(openRot6, closedRot6, t);
                    }
                    else if (shadeLevel == 2)
                    {   //shade1/4 closed, close shade2/5
                        shade2.localRotation = Quaternion.Slerp(openRot2, closedRot2, t);
                        shade5.localRotation = Quaternion.Slerp(openRot5, closedRot5, t);
                    }
                    else if (shadeLevel == 1)
                    {   //no shades closed, close shade1/4
                        shade1.localRotation = Quaternion.Slerp(openRot1, closedRot1, t);
                        shade4.localRotation = Quaternion.Slerp(openRot4, closedRot4, t);
                    }
                }
                yield return new WaitForEndOfFrame();
            }
            if (!open)
            {
                if (shadeLevel == 0)
                {
                    shade1.localRotation = openRot1;
                    shade4.localRotation = openRot4;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 1)
                {
                    shade2.localRotation = openRot2;
                    shade5.localRotation = openRot5;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 2)
                {
                    shade3.localRotation = openRot3;
                    shade6.localRotation = openRot6;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                shadeLevel++;
                if (shadeLevel > 3)
                {
                    shadeLevel = 3;
                }
            }
            else
            {
                if (shadeLevel == 3)
                {
                    shade3.localRotation = closedRot3;
                    shade6.localRotation = closedRot6;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 2)
                {
                    shade2.localRotation = closedRot2;
                    shade5.localRotation = closedRot5;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                else if (shadeLevel == 1)
                {
                    shade3.localRotation = closedRot3;
                    shade6.localRotation = closedRot6;
                    UISoundPlayer.instance.PlayOpenSound();
                }
                shadeLevel--;
                if (shadeLevel < 0)
                {
                    shadeLevel = 0;
                }
            }
            //Debug.LogWarning("Sextant: shadeLevel: " + shadeLevel);
            moving = false;
        }
        private float LimitIndex(float indexAngle)
        {   //limit the angle of the index bar beteween 35 and -35 degrees
            if (indexAngle < -35f)
            {
                return -35f;
            }
            else if (indexAngle > 35f)
            {
                return 35f;
            }
            else
            {
                return indexAngle;
            }
        }
        private void SwapCamMaterial(bool dropped)
        {   //swaps the camera glasses (mirrors, magnifier and ocular) with the spyglass material when not held
            if (!dropped)
            {
                indexRenderer.sharedMaterial = indexMat;
                horizonRenderer.sharedMaterial = horizonMat;
                ocularRenderer.sharedMaterial = ocularMat;
                magnifierRenderer.sharedMaterial = magnifierMat;
            }
            else
            {
                indexRenderer.sharedMaterial = telescopeMat;
                horizonRenderer.sharedMaterial = telescopeMat;
                ocularRenderer.sharedMaterial = telescopeMat;
                magnifierRenderer.sharedMaterial = telescopeMat;
            }
        }
    }
}
