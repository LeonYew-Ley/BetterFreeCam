using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterFreeCam
{
    // Independent source reimplementation derived from Bananegame FreeCAM 0.0.2.
    // See README.md for provenance; config keys retain the compatible 0.05 defaults.
    [BepInPlugin("com.github.LeonYewLey.BetterFreeCam", "BetterFreeCam", "1.0.0")]
    [DefaultExecutionOrder(10000)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static readonly string[] HudNames = { "Canvas_HUD", "DisplayCanvas", "Canvas_Names", "Binocs" };
        private static readonly FieldInfo GodCamField = typeof(MainCameraMovement).GetField("isGodCam", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ResetInputMethod = typeof(CharacterInput).GetMethod("ResetInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly Dictionary<GameObject, bool> hudStates = new Dictionary<GameObject, bool>();
        private ConfigEntry<KeyCode> hotkey;
        private ConfigEntry<float> moveSpeed;
        private ConfigEntry<float> mouseSensitivity;
        private MainCameraMovement cameraMovement;
        private MainCamera camera;
        private Camera renderCamera;
        private CharacterInput characterInput;
        private bool cameraWasEnabled;
        private bool inputWasEnabled;
        private bool godCamWasEnabled;
        private bool cursorWasVisible;
        private CursorLockMode cursorWasLocked;
        private bool active;
        private bool hudVisible;
        private int activeSceneHandle;
        private float yaw;
        private float pitch;
        private float originalFov;
        private float fov;

        private void Awake()
        {
            hotkey = Config.Bind("Controls", "FreeCamHotkey", KeyCode.F3, "Toggle FreeCam.");
            moveSpeed = Config.Bind("FreeCam Settings", "MoveSpeed", 0.05f, "Movement per 1/60 second. 0.05 = 3 metres/second; Shift = 3x. No acceleration or deceleration.");
            mouseSensitivity = Config.Bind("FreeCam Settings", "MouseSensitivity", 0.05f, "Raw mouse sensitivity. 0.05 = 0.5 degrees per mouse-axis unit. No look inertia.");
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("BetterFreeCam 1.0.0 loaded. Native GodCam movement, shake and depth-of-field are bypassed while active.");
        }

        private static bool ValidScene()
        {
            string name = SceneManager.GetActiveScene().name;
            return name == "Airport" || name.StartsWith("Level_");
        }

        private void Update()
        {
            if (active && (!ValidScene() || SceneManager.GetActiveScene().handle != activeSceneHandle || !cameraMovement || !camera))
                DisableFreeCam();
            if (Input.GetKeyDown(hotkey.Value))
            {
                if (active) DisableFreeCam();
                else if (ValidScene()) EnableFreeCam();
            }
            if (!active) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { DisableFreeCam(); return; }
            if (Input.GetKeyDown(KeyCode.H))
            {
                hudVisible = !hudVisible;
                foreach (var entry in hudStates)
                    if (entry.Key) entry.Key.SetActive(hudVisible && entry.Value);
            }
        }

        private void LateUpdate()
        {
            if (!active || !cameraMovement || !camera) return;
            // Native MainCameraMovement and PerspectiveSwitcher hooks are suspended while active.
            // We control the actual camera, not the BepInEx plugin GameObject as 0.0.2 did.
            if (GUIManager.instance && GUIManager.instance.windowBlockingInput) return;
            Transform target = cameraMovement.transform;
            float sensitivity = Mathf.Max(0f, mouseSensitivity.Value) * 10f;
            yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -89f, 89f);
            target.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 direction = new Vector3(
                Key(KeyCode.D) - Key(KeyCode.A),
                Key(KeyCode.Space) - Key(KeyCode.LeftControl),
                Key(KeyCode.W) - Key(KeyCode.S));
            direction = Vector3.ClampMagnitude(direction, 1f);
            // No velocity history: zero input produces exactly zero displacement this frame.
            float speed = Mathf.Max(0f, moveSpeed.Value) * 60f * (Input.GetKey(KeyCode.LeftShift) ? 3f : 1f);
            Vector3 displacement = target.right * direction.x + Vector3.up * direction.y + target.forward * direction.z;
            target.position += displacement * speed * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            fov = Mathf.Clamp(fov - Input.mouseScrollDelta.y * 2f, 1f, 120f);
            if (renderCamera) renderCamera.fieldOfView = fov;
        }

        private static float Key(KeyCode key) => Input.GetKey(key) ? 1f : 0f;

        private void EnableFreeCam()
        {
            MainCamera foundCamera = MainCamera.instance;
            MainCameraMovement foundMovement = foundCamera ? foundCamera.GetComponent<MainCameraMovement>() : null;
            Camera foundRenderCamera = foundCamera ? foundCamera.GetComponent<Camera>() : null;
            if (!foundCamera || !foundMovement || !foundRenderCamera || GodCamField == null || ResetInputMethod == null)
            {
                Logger.LogWarning("FreeCam is not ready: the gameplay camera is unavailable. Try F3 after loading.");
                return;
            }
            camera = foundCamera;
            renderCamera = foundRenderCamera;
            cameraMovement = foundMovement;
            cameraWasEnabled = cameraMovement.enabled;
            godCamWasEnabled = GodCamField != null && (bool)GodCamField.GetValue(cameraMovement);
            cursorWasVisible = Cursor.visible;
            cursorWasLocked = Cursor.lockState;
            characterInput = Character.localCharacter ? Character.localCharacter.input : null;
            if (characterInput) { inputWasEnabled = characterInput.enabled; ClearCharacterInput(characterInput); characterInput.enabled = false; }
            cameraMovement.enabled = false;
            // Leave native GodCam out of the loop. It accumulates vel/lookVel, applies drag,
            // changes DOF and adds Gamefeel; just reducing our configured speed cannot fix it.
            // CharacterMovement.Update checks isGodCam to ResetInput instead of sampling player controls.
            // Keep this flag true for that existing input-freeze path while its camera component is disabled.
            GodCamField.SetValue(cameraMovement, true);
            Vector3 angles = cameraMovement.transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -89f, 89f);
            originalFov = fov = renderCamera.fieldOfView;
            activeSceneHandle = SceneManager.GetActiveScene().handle;
            hudStates.Clear();
            foreach (string name in HudNames)
            {
                GameObject hud = GameObject.Find(name);
                if (hud && !hudStates.ContainsKey(hud)) { hudStates.Add(hud, hud.activeSelf); hud.SetActive(false); }
            }
            hudVisible = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            active = true;
            Logger.LogInfo("FreeCam activated: direct camera input, no inertia. F3/ESC exits; H toggles HUD.");
        }

        private void DisableFreeCam()
        {
            if (!active) return;
            active = false;
            // Always restore captured values, including a component originally disabled by another mod.
            if (cameraMovement)
            {
                if (GodCamField != null) GodCamField.SetValue(cameraMovement, godCamWasEnabled);
                cameraMovement.enabled = cameraWasEnabled;
            }
            if (characterInput) { ClearCharacterInput(characterInput); characterInput.enabled = inputWasEnabled; }
            if (renderCamera) renderCamera.fieldOfView = originalFov;
            foreach (var entry in hudStates) if (entry.Key) entry.Key.SetActive(entry.Value);
            hudStates.Clear();
            Cursor.visible = cursorWasVisible;
            Cursor.lockState = cursorWasLocked;
            camera = null;
            renderCamera = null;
            cameraMovement = null;
            characterInput = null;
            Logger.LogInfo("FreeCam deactivated; original camera and input states restored.");
        }

        private static void ClearCharacterInput(CharacterInput input)
        {
            // Reuse the game reset for movement/jump/use. It omits scroll and hotbar/ping flags.
            ResetInputMethod.Invoke(input, null);
            input.scrollInput = 0f;
            input.scrollForwardWasPressed = input.scrollBackwardWasPressed = false;
            input.scrollForwardIsPressed = input.scrollBackwardIsPressed = false;
            input.selectSlotForwardWasPressed = input.selectSlotBackwardWasPressed = false;
            input.unselectSlotWasPressed = input.selectBackpackWasPressed = false;
            input.spectateLeftWasPressed = input.spectateRightWasPressed = false;
            input.sprintToggleIsPressed = input.pingWasPressed = input.pauseWasPressed = false;
        }
        private void SceneChanged(Scene previous, Scene current) => DisableFreeCam();
        private void OnDisable() => DisableFreeCam();
        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            DisableFreeCam();
        }
    }
}



