using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.Collections;

namespace FpsLimitMod
{
    [BepInPlugin("com.Palmblom.FpsLimitMod", "FPS Limit Mod", "1.0.0")]
    public class FpsLimitMod : BaseUnityPlugin
    {
        #region Fields and Properties

        private bool isInitialized = false;
        private bool showMenu = false;
        private bool waitingForKeyPress = false;
        private string currentBindingAction = "";

        private const float InitializationDelay = 2f;

        // Config for keybindings
        private ConfigEntry<KeyCode> OpenMenuKey;
        private KeyBinding limit45KeyBinding;
        private KeyBinding limit60KeyBinding;
        private ConfigEntry<KeyCode> UncapFpsKey;

        private KeyBinding controllerLimit45KeyBinding;
        private KeyBinding controllerLimit60KeyBinding;
        private ConfigEntry<KeyCode> ControllerUncapFpsKey;

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            InitializeConfig();
            InitializeKeyBindings();
            StartCoroutine(InitializeWhenReady());
        }

        private IEnumerator InitializeWhenReady()
        {
            yield return new WaitForSeconds(InitializationDelay);
            isInitialized = true;
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

            HandleKeyInputs();
        }

        private void OnGUI()
        {
            if (!isInitialized) return;

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
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }

        #endregion

        #region Initialization Methods

        private void InitializeConfig()
        {
            OpenMenuKey = Config.Bind("General", "OpenMenuKey", KeyCode.H, "Key to open the FPS Limit Mod menu");

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

            // Controller button controls
            if (Input.GetKeyDown(controllerLimit45KeyBinding.Key.Value))
                SetFpsLimit(controllerLimit45KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(controllerLimit60KeyBinding.Key.Value))
                SetFpsLimit(controllerLimit60KeyBinding.FpsValue.Value);

            if (Input.GetKeyDown(ControllerUncapFpsKey.Value))
                UncapFps();

            // Handle custom axis mappings
            CheckAxisMapping(controllerLimit45KeyBinding.Key.Value, () => SetFpsLimit(controllerLimit45KeyBinding.FpsValue.Value));
            CheckAxisMapping(controllerLimit60KeyBinding.Key.Value, () => SetFpsLimit(controllerLimit60KeyBinding.FpsValue.Value));
            CheckAxisMapping(ControllerUncapFpsKey.Value, UncapFps);
        }

        private void CheckAxisMapping(KeyCode keyCode, System.Action action)
        {
            // Check if this is one of our custom axis mappings
            if ((int)keyCode > (int)KeyCode.JoystickButton19 && (int)keyCode <= (int)KeyCode.JoystickButton19 + 10)
            {
                int axisNumber = (int)keyCode - (int)KeyCode.JoystickButton19 - 1;
                string axisName = axisNumber <= 1 ? (axisNumber == 0 ? "X" : "Y") : axisNumber.ToString();

                try
                {
                    float axisValue = Input.GetAxis("Joystick1Axis" + axisName);
                    if (Mathf.Abs(axisValue) > 0.7f)  // Threshold to detect significant movement
                    {
                        action.Invoke();
                    }
                }
                catch (System.Exception)
                {
                    // Axis might not exist, just continue
                }
            }
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
                return;
            }

            // Check for controller button presses
            for (KeyCode key = KeyCode.JoystickButton0; key <= KeyCode.JoystickButton19; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    AssignNewKeyBinding(key);
                    waitingForKeyPress = false;
                    currentBindingAction = "";
                    return;
                }
            }

            // Check for controller axis inputs
            if (currentBindingAction.StartsWith("Controller"))
            {
                // Check joystick axes
                string[] axisNames = { "X", "Y", "3", "4", "5", "6", "7", "8", "9", "10" };
                foreach (string axis in axisNames)
                {
                    try
                    {
                        float axisValue = Input.GetAxis("Joystick1Axis" + axis);
                        if (Mathf.Abs(axisValue) > 0.7f)  // Threshold to detect significant movement
                        {
                            // Map to a KeyCode that represents this axis
                            // We'll use KeyCode.Joystick1Button0 through Button19 for actual buttons
                            // And use higher values for axes
                            KeyCode axisKeyCode = (KeyCode)(KeyCode.JoystickButton19 + int.Parse(axis) + 1);
                            AssignNewKeyBinding(axisKeyCode);
                            waitingForKeyPress = false;
                            currentBindingAction = "";
                            return;
                        }
                    }
                    catch (System.Exception)
                    {
                        // Axis might not exist, just continue
                    }
                }
            }
        }

        private void AssignNewKeyBinding(KeyCode newKey)
        {
            switch (currentBindingAction)
            {
                case "OpenMenuKey":
                    OpenMenuKey.Value = newKey;
                    break;
                case "Limit45Key":
                    limit45KeyBinding.Key.Value = newKey;
                    break;
                case "Limit60Key":
                    limit60KeyBinding.Key.Value = newKey;
                    break;
                case "UncapFpsKey":
                    UncapFpsKey.Value = newKey;
                    break;
                case "ControllerLimit45Key":
                    controllerLimit45KeyBinding.Key.Value = newKey;
                    break;
                case "ControllerLimit60Key":
                    controllerLimit60KeyBinding.Key.Value = newKey;
                    break;
                case "ControllerUncapFpsKey":
                    ControllerUncapFpsKey.Value = newKey;
                    break;
            }
        }

        #endregion

        #region UI Drawing Methods

        private void DrawKeybindingMenu()
        {
            GUI.Box(new Rect(10, 10, 500, 420), "FPS Limit Mod Keybindings");
            int currentYOffset = 40;
            int buttonOffset = 40;

            DrawOpenMenuKey(ref currentYOffset, buttonOffset);
            DrawKeyboardBindings(ref currentYOffset, buttonOffset);
            DrawControllerBindings(ref currentYOffset, buttonOffset);

            if (waitingForKeyPress)
            {
                string message = currentBindingAction.StartsWith("Controller")
                    ? $"Press any controller button to set binding for {currentBindingAction}"
                    : $"Press any key to set binding for {currentBindingAction}";

                GUI.Label(new Rect(20, currentYOffset, 460, 20), message);
                currentYOffset += 30;

                // Add a cancel button
                if (GUI.Button(new Rect(20, currentYOffset, 100, 20), "Cancel"))
                {
                    waitingForKeyPress = false;
                    currentBindingAction = "";
                }
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

        private void DrawKeyboardBindings(ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), "Keyboard shortcuts");
            y += 20;

            DrawKeyBindingOption(limit45KeyBinding, ref y, buttonOffset);
            DrawKeyBindingOption(limit60KeyBinding, ref y, buttonOffset);

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

            DrawControllerBindingOption(controllerLimit45KeyBinding, ref y, buttonOffset);
            DrawControllerBindingOption(controllerLimit60KeyBinding, ref y, buttonOffset);

            GUI.Label(new Rect(20, y, 250, 20), $"{GetControllerButtonName(ControllerUncapFpsKey.Value)} uncaps FPS");
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
                }
            }
            y += 30;
        }

        private void DrawControllerBindingOption(KeyBinding binding, ref int y, int buttonOffset)
        {
            GUI.Label(new Rect(20, y, 250, 20), $"{GetControllerButtonName(binding.Key.Value)} sets FPS to:");
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
                }
            }
            y += 30;
        }

        private string GetControllerButtonName(KeyCode keyCode)
        {
            if (keyCode >= KeyCode.JoystickButton0 && keyCode <= KeyCode.JoystickButton19)
            {
                int buttonNumber = keyCode - KeyCode.JoystickButton0;
                return $"Controller Button {buttonNumber}";
            }
            else if ((int)keyCode > (int)KeyCode.JoystickButton19 && (int)keyCode <= (int)KeyCode.JoystickButton19 + 10)
            {
                // This is one of our custom axis mappings
                int axisNumber = (int)keyCode - (int)KeyCode.JoystickButton19 - 1;
                return $"Controller Axis {axisNumber}";
            }
            return keyCode.ToString();
        }

        #endregion

        #region FPS Control Methods

        private void SetFpsLimit(int fps)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }

        private void UncapFps()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
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