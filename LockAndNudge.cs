using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine; 
using Assets.Scripts.Inventory; 
using Assets.Scripts.GridSystem; 
using Assets.Scripts.Util;


[BepInPlugin("com.Nightjar.mod", "Lock And Nudge Mod", "1.0")]

public class LockAndNudge : BaseUnityPlugin {

    public static bool IsLocked = false;
    public static bool AllowRedirect = false;
    public static Vector3 LockedPos;
    public static Vector3 InitialPos;

    // --- CONFIGURATION ENTRIES ---
    public static ConfigEntry<KeyCode> KeyLock;
    public static ConfigEntry<KeyCode> KeyForward;
    public static ConfigEntry<KeyCode> KeyBack;
    public static ConfigEntry<KeyCode> KeyLeft;
    public static ConfigEntry<KeyCode> KeyRight;
    public static ConfigEntry<KeyCode> KeyUp;
    public static ConfigEntry<KeyCode> KeyDown;
    
    public static ConfigEntry<float> MaxStepDistance;
    public static ConfigEntry<bool> UseItemGridSize;


    private Vector3 GetDominantAxis(Vector3 dir) {
        dir.y = 0; 

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z)) {
            // X is dominant (East/West)
            return new Vector3(Mathf.Sign(dir.x), 0, 0); 
        } else {
            // Z is dominant (North/South)
            return new Vector3(0, 0, Mathf.Sign(dir.z));
        }
    }

    void Awake() {
        //nudge inputs intialize
        KeyLock     = Config.Bind("Controls", "ToggleLock", KeyCode.H, "Key to freeze the hologram");

        KeyForward  = Config.Bind("Controls", "NudgeForward", KeyCode.UpArrow, "Push hologram away");
        KeyBack     = Config.Bind("Controls", "NudgeBack", KeyCode.DownArrow, "Pull hologram closer");
        KeyLeft     = Config.Bind("Controls", "NudgeLeft", KeyCode.LeftArrow, "Slide hologram left");
        KeyRight    = Config.Bind("Controls", "NudgeRight", KeyCode.RightArrow, "Slide hologram right");
        KeyUp       = Config.Bind("Controls", "NudgeUp", KeyCode.PageUp, "Lift hologram up");
        KeyDown     = Config.Bind("Controls", "NudgeDown", KeyCode.PageDown, "Drop hologram down");

        MaxStepDistance    = Config.Bind("Settings", "MaxNudgeDistance(Meters)", 3f, "Max distance nudge can reach. A frame is 2 meters for scale.");
        UseItemGridSize = Config.Bind("Settings", "UseDynamicGrid", true, "If true nudge distance will change based on Item. If false, always uses 0.5 meter steps.(Disable if high grid needs small nudges.)");
        
        var harmony = new Harmony("com.Nighjar.mod");
        harmony.PatchAll();
    }
    
    void Update() {
    if (InventoryManager.ConstructionCursor == null) {
        if (IsLocked) {
            IsLocked = false;//Reset the lock flag to false upon build ends
        }
        return; //Instantly out if not in placement mode
    }
    
    if (Input.GetKeyDown(KeyLock.Value)) {
        IsLocked = !IsLocked;
        if (IsLocked)
                {
                    InitialPos = LockedPos;
                }
    }

    if (IsLocked) {
        float step = 0.5f; 
        if (UseItemGridSize.Value && InventoryManager.ConstructionCursor != null)
        {
            float currentGrid=InventoryManager.ConstructionCursor.GridSize;
            if(currentGrid > 0.01f)
                step = currentGrid;
        }
        Transform cam = Camera.main.transform;

        Vector3 rawForward = cam.forward;
        Vector3 rawRight = cam.right;

        Vector3 forwardDir = GetDominantAxis(rawForward);
        Vector3 rightDir = GetDominantAxis(rawRight);

        Vector3 proposedPos = LockedPos;

        if (Input.GetKeyDown(KeyForward.Value)) proposedPos += forwardDir * step;
        if (Input.GetKeyDown(KeyBack.Value))    proposedPos -= forwardDir * step;
        if (Input.GetKeyDown(KeyRight.Value))   proposedPos += rightDir * step;
        if (Input.GetKeyDown(KeyLeft.Value))    proposedPos -= rightDir * step;
        if (Input.GetKeyDown(KeyUp.Value))      proposedPos += Vector3.up * step;
        if (Input.GetKeyDown(KeyDown.Value))    proposedPos += Vector3.down * step;

        if (Vector3.Distance(proposedPos, InitialPos) < MaxStepDistance.Value)
            LockedPos=proposedPos;

    }
}
}
// Set Allow Redirect if the caller is placement functions.

[HarmonyPatch(typeof(InventoryManager), "PlacementMode")] // Check exact class name
public static class ScopePatch_Placement {
    
    static void Prefix() {
        LockAndNudge.AllowRedirect = true; // OPEN THE GATE
    }

    static void Postfix() {
        LockAndNudge.AllowRedirect = false; // CLOSE THE GATE
    }
}

[HarmonyPatch(typeof(InventoryManager), "UnitTest_ConstructionValidate")] 
public static class ScopePatch_Validation {
    static void Prefix() {
        LockAndNudge.AllowRedirect = true; 
    }
    static void Postfix() {
        LockAndNudge.AllowRedirect = false; 
    }
}


[HarmonyPatch(typeof(InputHelpers), "GetCameraForwardGrid")]
public static class CameraGridPatch {
        public static bool Prefix(ref Vector3 __result) {
        
        if (LockAndNudge.IsLocked && LockAndNudge.AllowRedirect) {
            __result = LockAndNudge.LockedPos;
            return false;
        }
        return true;
    }
    
    public static void Postfix(ref Vector3 __result) {
        if (!LockAndNudge.IsLocked && LockAndNudge.AllowRedirect) {
            LockAndNudge.LockedPos = __result;
        }
    }
}