using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Collections;

namespace FpsLimitMod
{
    [BepInPlugin("com.YourName.FpsLimitMod", "FPS Limit Mod", "1.0.7")]
    public class FpsLimitMod : BaseUnityPlugin
    {
        #region Fields and Properties

        private bool isInitialized = false;

        // FPS display variables
        private float currentFps;
        private bool showFpsCounter = true; 
        private bool showDebugOverlay = true;

        private bool showFpsMessage = false;
        private string fpsMessage = "";

        private bool showMenu = false;
        private bool waitingForKeyPress = false;
        private string currentBindingAction = "";

        private const float InitializationDelay = 2f;
        private const float MessageDuration = 2f;

        // Config for keybindings
        private ConfigEntry<KeyCode> OpenMenuKey;
        private ConfigEntry<KeyCode> ToggleDebugOverlayKey;

        private KeyBinding limit45KeyBinding;
        private KeyBinding limit60KeyBinding;
        private ConfigEntry<KeyCode> UncapFpsKey;

        private KeyBinding controllerLimit45KeyBinding;
        private KeyBinding controllerLimit60KeyBinding;
        private ConfigEntry<KeyCode> ControllerUncapFpsKey;

        // FPS statistics
        private float minFps = float.MaxValue;
        private float maxFps = 0f;
        private float accumulatedFps = 0f;
        private int fpsSampleCount = 0;

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            Logger.LogInfo("FPS Limit Mod is starting...");

            InitializeConfig();
            InitializeKeyBindings();

            StartCoroutine(InitializeWhenReady());
        }

        private IEnumerator InitializeWhenReady()
        {
            yield return new WaitForSeconds(InitializationDelay);
            isInitialized = true;
            Logger.LogInfo("FPS Limit Mod initialized successfully");
        }

        private void Update()
        {
            if (!isInitialized) return;

            HandleMenuToggle();

            if (waitingForKeyPress)
            {
                return; 
            }

            if (showMenu) return;

            UpdateFpsCounters();

            HandleKeyInputs();
        }

        private void OnGUI()
        {
            if (!isInitialized) return;

            if (showFpsCounter)
            {
                DisplayFpsCounter();
            }

            if (showFpsMessage)
            {
                DisplayFpsMessage();
            }

            if (showDebugOverlay)
            {
                DisplayDebugOverlay();
            }

            if (showMenu)
            {
                DrawKeybindingMenu();
            }

            if (waitingForKeyPress)
            {
                DetectKeyPress();
            }
        }

        private void OnDisable()
        {
            Logger.LogInfo("FPS Limit Mod disabled");
            QualitySettings.vSyncCount = 1; // Chatgpt added this idk why but Il keep it 
            Application.targetFrameRate = -1; // Same as above it fixes something I assume but idk 🤷
        }

        #endregion

        #region Initialization Methods

        private void InitializeConfig()
        {
            OpenMenuKey = Config.Bind("General", "OpenMenuKey", KeyCode.H, "Key to open the FPS Limit Mod menu");
            ToggleDebugOverlayKey = Config.Bind("General", "ToggleDebugOverlayKey", KeyCode.Q, "Key to toggle the debug overlay");

            limit45KeyBinding = new KeyBinding(
                Config.Bind("KeyboardBindings", "Limit45Key", KeyCode.K, "Keyboard key to set FPS limit to 45"),
                Config.Bind("KeyboardBindings", "Limit45Value", 45, "FPS value for the first FPS limit keybinding"),
                "Limit45Key");

            limit60KeyBinding = new KeyBinding(
                Config.Bind("KeyboardBindings", "Limit60Key", KeyCode.L, "Keyboard key to set FPS limit to 60"),
                Config.Bind("KeyboardBindings", "Limit60Value", 60, "FPS value for the second FPS limit keybinding"),
                "Limit60Key");

            UncapFpsKey = Config.Bind("KeyboardBindings", "UncapFpsKey", KeyCode.O, "Keyboard key to uncap FPS");

            controllerLimit45KeyBinding = new KeyBinding(
                Config.Bind("ControllerBindings", "Limit45Key", KeyCode.JoystickButton2, "Controller button to set FPS limit to 45"),
                Config.Bind("ControllerBindings", "Limit45Value", 45, "Controller FPS value for the first FPS limit keybinding"),
                "ControllerLimit45Key");

            controllerLimit60KeyBinding = new KeyBinding(
                Config.Bind("ControllerBindings", "Limit60Key", KeyCode.JoystickButton0, "Controller button to set FPS limit to 60"),
                Config.Bind("ControllerBindings", "Limit60Value", 60, "Controller FPS value for the second FPS limit keybinding"),
                "ControllerLimit60Key");

            ControllerUncapFpsKey = Config.Bind("ControllerBindings", "UncapFpsKey", KeyCode.JoystickButton1, "Controller button to uncap FPS");
        }

        private void InitializeKeyBindings()
        {
            limit45KeyBinding.InputFieldText = limit45KeyBinding.FpsValue.Value.ToString();
            limit60KeyBinding.InputFieldText = limit60KeyBinding.FpsValue.Value.ToString();

            controllerLimit45KeyBinding.InputFieldText = controllerLimit45KeyBinding.FpsValue.Value.ToString();
            controllerLimit60KeyBinding.InputFieldText = controllerLimit60KeyBinding.FpsValue.Value.ToString();
        }

        #endregion

        #region Input Handling Methods

        private void HandleMenuToggle()
        {
            if (Input.GetKeyDown(OpenMenuKey.Value))
            {
                showMenu = !showMenu;
                waitingForKeyPress = false;
                currentBindingAction = "";
            }

            if (Input.GetKeyDown(ToggleDebugOverlayKey.Value))
            {
                showDebugOverlay = !showDebugOverlay;
            }
        }

        private void HandleKeyInputs()
        {
            // Keyboard controls
            if (Input.GetKeyDown(limit45KeyBinding.Key.Value))
                SetFpsLimit(limit45KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(limit60KeyBinding.Key.Value))
                SetFpsLimit(limit60KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(UncapFpsKey.Value))
                UncapFps();

            // Controller controls
            if (Input.GetKeyDown(controllerLimit45KeyBinding.Key.Value))
                SetFpsLimit(controllerLimit45KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(controllerLimit60KeyBinding.Key.Value))
                SetFpsLimit(controllerLimit60KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(ControllerUncapFpsKey.Value))
                UncapFps();
        }

        private void DetectKeyPress()
        {
            Event e = Event.current;
            if (e.isKey && e.type == EventType.KeyDown)
            {
                AssignNewKeyBinding(e.keyCode);
                waitingForKeyPress = false;
                currentBindingAction = "";
                e.Use();
            }
        }

        private void AssignNewKeyBinding(KeyCode newKey)
        {
            switch (currentBindingAction)
            {
                case "OpenMenuKey":
                    OpenMenuKey.Value = newKey;
                    Logger.LogInfo($"Open Menu Key changed to {newKey}");
                    break;
                case "ToggleDebugOverlayKey":
                    ToggleDebugOverlayKey.Value = newKey;
                    Logger.LogInfo($"Toggle Debug Overlay Key changed to {newKey}");
                    break;
                case "Limit45Key":
                    limit45KeyBinding.Key.Value = newKey;
                    Logger.LogInfo($"Limit to {limit45KeyBinding.FpsValue.Value} FPS Key changed to {newKey}");
                    break;
                case "Limit60Key":
                    limit60KeyBinding.Key.Value = newKey;
                    Logger.LogInfo($"Limit to {limit60KeyBinding.FpsValue.Value} FPS Key changed to {newKey}");
                    break;
                case "UncapFpsKey":
                    UncapFpsKey.Value = newKey;
                    Logger.LogInfo($"Uncap FPS Key changed to {newKey}");
                    break;
                case "ControllerLimit45Key":
                    controllerLimit45KeyBinding.Key.Value = newKey;
                    Logger.LogInfo($"Controller Limit to {controllerLimit45KeyBinding.FpsValue.Value} FPS Key changed to {newKey}");
                    break;
                case "ControllerLimit60Key":
                    controllerLimit60KeyBinding.Key.Value = newKey;
                    Logger.LogInfo($"Controller Limit to {controllerLimit60KeyBinding.FpsValue.Value} FPS Key changed to {newKey}");
                    break;
                case "ControllerUncapFpsKey":
                    ControllerUncapFpsKey.Value = newKey;
                    Logger.LogInfo($"Controller Uncap FPS Key changed to {newKey}");
                    break;
            }
        }

        #endregion

        #region UI Drawing Methods

        private void DisplayFpsCounter()
        {
            GUIStyle style = new GUIStyle
            {
                fontSize = 16,
                normal = { textColor = Color.yellow }
            };
            float xPosition = Screen.width - 120; 
            GUI.Label(new Rect(xPosition, 10, 200, 30), $"FPS: {currentFps:F2}", style);
        }

        private void DisplayFpsMessage()
        {
            GUIStyle style = new GUIStyle
            {
                fontSize = 20,
                normal = { textColor = Color.white }
            };
            float xPosition = Screen.width - 320; 
            GUI.Label(new Rect(xPosition, 40, 300, 30), fpsMessage, style);
        }

        private void DisplayDebugOverlay()
        {
            GUIStyle style = new GUIStyle
            {
                fontSize = 14,
                normal = { textColor = Color.cyan }
            };
            int yPosition = 70;
            float xPosition = Screen.width - 320; 

            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"Stats reset when switching fps limit and press *H* to change keybinds", style);
            yPosition += 20;
            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"Current FPS Limit: {(Application.targetFrameRate == -1 ? "Uncapped" : Application.targetFrameRate.ToString())}", style);
            yPosition += 20;
            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"VSync off/on: {QualitySettings.vSyncCount}", style);
            yPosition += 20;
            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"Lowest Observed Fps: {minFps:F2}", style);
            yPosition += 20;
            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"Highest Observed Fps: {maxFps:F2}", style);
            yPosition += 20;
            float averageFps = fpsSampleCount > 0 ? accumulatedFps / fpsSampleCount : 0f;
            GUI.Label(new Rect(xPosition, yPosition, 300, 20), $"Average FPS: {averageFps:F2}", style);
        }

        private void DrawKeybindingMenu()
        {
            GUI.Box(new Rect(10, 10, 500, 420), "FPS Limit Mod Keybindings");
            int currentYOffset = 40;
            int buttonOffset = 40;

            DrawOpenMenuKey(ref currentYOffset, buttonOffset);
            DrawToggleDebugOverlayKey(ref currentYOffset, buttonOffset);
            DrawKeyboardBindings(ref currentYOffset, buttonOffset);
            DrawControllerBindings(ref currentYOffset, buttonOffset);

            if (waitingForKeyPress)
            {
                GUI.Label(new Rect(20, currentYOffset, 460, 20), $"Press any key to set binding for {currentBindingAction}");
            }
        }

        private void DrawOpenMenuKey(ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), $"Open Menu Key: {OpenMenuKey.Value}");
            if (!waitingForKeyPress && GUI.Button(new Rect(280 + buttonOffset, y, 100, 20), "Change Key"))
            {
                waitingForKeyPress = true;
                currentBindingAction = "OpenMenuKey";
            }
            y += 30;
        }

        private void DrawToggleDebugOverlayKey(ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), $"Toggle Debug Overlay Key: {ToggleDebugOverlayKey.Value}");
            if (!waitingForKeyPress && GUI.Button(new Rect(280 + buttonOffset, y, 100, 20), "Change Key"))
            {
                waitingForKeyPress = true;
                currentBindingAction = "ToggleDebugOverlayKey";
            }
            y += 30;
        }

        private void DrawKeyboardBindings(ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), "Keyboard shortcuts");
            y += 20;

            DrawKeyBindingOption(limit45KeyBinding, ref y, buttonOffset);
            DrawKeyBindingOption(limit60KeyBinding, ref y, buttonOffset);

            // Uncap FPS Key
            GUI.Label(new Rect(20, y, 250, 20), $"{UncapFpsKey.Value} uncaps FPS");
            if (!waitingForKeyPress && GUI.Button(new Rect(280 + buttonOffset, y, 100, 20), "Change Key"))
            {
                waitingForKeyPress = true;
                currentBindingAction = "UncapFpsKey";
            }
            y += 40;
        }

        private void DrawControllerBindings(ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), "Controller shortcuts");
            y += 20;

            DrawKeyBindingOption(controllerLimit45KeyBinding, ref y, buttonOffset);
            DrawKeyBindingOption(controllerLimit60KeyBinding, ref y, buttonOffset);

            // Controller Uncap FPS Key!
            GUI.Label(new Rect(20, y, 250, 20), $"{ControllerUncapFpsKey.Value} uncaps FPS");
            if (!waitingForKeyPress && GUI.Button(new Rect(280 + buttonOffset, y, 100, 20), "Change Key"))
            {
                waitingForKeyPress = true;
                currentBindingAction = "ControllerUncapFpsKey";
            }
            y += 40;
        }

        private void DrawKeyBindingOption(KeyBinding binding, ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), $"{binding.Key.Value} sets FPS to:");
            binding.InputFieldText = GUI.TextField(new Rect(200, y, 40, 20), binding.InputFieldText, 3);

            if (!waitingForKeyPress && GUI.Button(new Rect(250 + buttonOffset, y, 100, 20), "Change Key"))
            {
                waitingForKeyPress = true;
                currentBindingAction = binding.ActionDescription;
            }

            if (GUI.Button(new Rect(360 + buttonOffset, y, 120, 20), "Set FPS Limit"))
            {
                if (int.TryParse(binding.InputFieldText, out int fps))
                {
                    binding.FpsValue.Value = fps;
                    Logger.LogInfo($"{binding.ActionDescription} Value changed to {fps}");
                }
                else
                {
                    DisplayErrorMessage($"Invalid FPS value for {binding.ActionDescription}.");
                }
            }
            y += 30;
        }

        #endregion

        #region FPS Control Methods

        private void SetFpsLimit(int fps)
        {
            QualitySettings.vSyncCount = 0; // Disable VSync permanently but sometimes no?
            Application.targetFrameRate = fps;
            ResetFpsStats();
            DisplayFpsSetMessage($"FPS set to {fps}");
            Logger.LogInfo($"FPS limited to {fps}");
        }

        private void UncapFps()
        {
            QualitySettings.vSyncCount = 0; // Ensure VSync remains disabled!
            Application.targetFrameRate = -1; 
            ResetFpsStats();
            DisplayFpsSetMessage("FPS uncapped");
            Logger.LogInfo("FPS uncapped");
        }

        private void DisplayFpsSetMessage(string message)
        {
            fpsMessage = message;
            showFpsMessage = true;
            StartCoroutine(HideMessageAfterDelay());
        }

        private IEnumerator HideMessageAfterDelay()
        {
            yield return new WaitForSeconds(MessageDuration);
            showFpsMessage = false;
        }

        private void ResetFpsStats()
        {
            minFps = float.MaxValue;
            maxFps = 0f;
            accumulatedFps = 0f;
            fpsSampleCount = 0;
        }

        private void UpdateFpsCounters()
        {
            currentFps = 1.0f / Time.unscaledDeltaTime;

            // Update min and max FPS
            if (currentFps < minFps) minFps = currentFps;
            if (currentFps > maxFps) maxFps = currentFps;

            // Accumulate FPS for average calculation
            accumulatedFps += currentFps;
            fpsSampleCount++;
        }

        #endregion

        #region Utility Methods

        private void DisplayErrorMessage(string message)
        {
            Logger.LogError(message);
            fpsMessage = message;
            showFpsMessage = true;
            StartCoroutine(HideMessageAfterDelay());
        }

        #endregion

        #region Nested Classes

        [System.Serializable]
        public class KeyBinding
        {
            public ConfigEntry<KeyCode> Key;
            public ConfigEntry<int> FpsValue;
            public string ActionDescription;
            public string InputFieldText;

            public KeyBinding(ConfigEntry<KeyCode> key, ConfigEntry<int> fpsValue, string actionDescription)
            {
                Key = key;
                FpsValue = fpsValue;
                ActionDescription = actionDescription;
                InputFieldText = fpsValue.Value.ToString();
            }
        }

        #endregion
    }
}