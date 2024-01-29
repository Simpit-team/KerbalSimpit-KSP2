using System.Reflection;
using KSP.Game;
using KSP.UI.Binding;
using SpaceWarp.API.Assets;
using SpaceWarp.API.UI.Appbar;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace Simpit.UI;

/// <summary>
/// Controller for the Window UI.
/// </summary>
public class MainWindowController : MonoBehaviour
{
    // AppBar button IDs
    private const string ToolbarFlightButtonID = "BTN-SimpitFlight";
    private const string ToolbarOabButtonID = "BTN-SimpitOAB";
    private const string ToolbarKscButtonID = "BTN-SimpitKSC";

    private const string DefaultDebugText = "Write text here with the 'printToKSP' function";

    // The UIDocument component of the window game object
    private UIDocument _window;

    // The elements of the window that we need to access
    private VisualElement _rootElement;
    private TextField _serialPortField;
    private TextField _baudRateField;
    private Label _statusLabel;
    private Label _debugTextLabel;

    private int _debugTextCounter = 0;

    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen = false;

    public static MainWindowController Instance { get; private set; }

    /// <summary>
    /// Loads all the assemblies for the mod and registers the window controller.
    /// </summary>
    public static void Init(string ModGuid, string ModName)
    {
        // Load the Unity project assembly
        var currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!.FullName;
        var unityAssembly = Assembly.LoadFrom(Path.Combine(currentFolder, "Simpit.Unity.dll"));
        // Register any custom UI controls from the loaded assembly
        CustomControls.RegisterFromAssembly(unityAssembly);

        // Load the UI from the asset bundle
        var windowUxml = AssetManager.GetAsset<VisualTreeAsset>(
            // The case-insensitive path to the asset in the bundle is composed of:
            // - The mod GUID:
            $"{ModGuid}/" +
            // - The name of the asset bundle:
            "simpit_ui/" +
            // - The path to the asset in your Unity project (without the "Assets/" part)
            "ui/mainwindow/mainwindow.uxml"
        );

        // Create the window options object
        var windowOptions = new WindowOptions
        {
            // The ID of the window. It should be unique to your mod.
            WindowId = "Simpit_Window",
            // The transform of parent game object of the window.
            // If null, it will be created under the main canvas.
            Parent = null,
            // Whether or not the window can be hidden with F2.
            IsHidingEnabled = true,
            // Whether to disable game input when typing into text fields.
            DisableGameInputForTextFields = true,
            MoveOptions = new MoveOptions
            {
                // Whether or not the window can be moved by dragging.
                IsMovingEnabled = true,
                // Whether or not the window can only be moved within the screen bounds.
                CheckScreenBounds = true
            }
        };

        // Create the window
        var window = Window.Create(windowOptions, windowUxml);
        // Add a controller for the UI to the window's game object
        Instance = window.gameObject.AddComponent<MainWindowController>();

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            ModName,
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen => Instance.IsWindowOpen = isOpen
        );

        // Register OAB AppBar Button
        Appbar.RegisterOABAppButton(
            ModName,
            ToolbarOabButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen => Instance.IsWindowOpen = isOpen
        );

        // Register KSC AppBar Button
        Appbar.RegisterKSCAppButton(
            ModName,
            ToolbarKscButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            () => Instance.IsWindowOpen = !Instance.IsWindowOpen
        );
    }

    /// <summary>
    /// The state of the window. Setting this value will open or exit the window.
    /// </summary>
    public bool IsWindowOpen
    {
        get => _isWindowOpen;
        set
        {
            _isWindowOpen = value;

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            // Alternatively, you can deactivate the window game object to exit the window and stop it from updating,
            // which is useful if you perform expensive operations in the window update loop. However, this will also
            // mean you will have to re-register any event handlers on the window elements when re-enabled in OnEnable.
            // gameObject.SetActive(value);

            // Update the Flight AppBar button state
            GameObject.Find(ToolbarFlightButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);

            // Update the OAB AppBar button state
            GameObject.Find(ToolbarOabButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);
        }
    }

    /// <summary>
    /// Runs when the window is first created, and every time the window is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        // Get the UIDocument component from the game object
        _window = GetComponent<UIDocument>();

        // Get the root element of the window.
        // Since we're cloning the UXML tree from a VisualTreeAsset, the actual root element is a TemplateContainer,
        // so we need to get the first child of the TemplateContainer to get our actual root VisualElement.
        _rootElement = _window.rootVisualElement[0];

        IsWindowOpen = false;

        // Get the text field from the window
        _serialPortField = _rootElement.Q<TextField>("serial-port");
        _baudRateField = _rootElement.Q<TextField>("baud-rate");
        _statusLabel = _rootElement.Q<Label>("connection-status");
        _debugTextLabel = _rootElement.Q<Label>("debug-text");

        // Center the window by default
        _rootElement.CenterByDefault();
        //_rootElement.StopMouseEventsPropagation();

        if (SimpitPlugin.Instance.port != null)
        {
            _serialPortField.value = SimpitPlugin.Instance.port.PortName;
            _baudRateField.value = SimpitPlugin.Instance.port.BaudRate.ToString();
            _statusLabel.text = $"{SimpitPlugin.Instance.port.portStatus}";
        }
        else
        {
            _serialPortField.value = SimpitPlugin.Instance.config_SerialPortName;
            _baudRateField.value = SimpitPlugin.Instance.config_SerialPortBaudRate.ToString();
            _statusLabel.text = "UNKNOWN";
        }

        _debugTextLabel.text = DefaultDebugText;

        // Get the exit button from the window
        var exitButton = _rootElement.Q<Button>("exit-button");
        exitButton.clicked += () => IsWindowOpen = false;

        // Get the "Open" button from the window
        var openButton = _rootElement.Q<Button>("open-button");
        openButton.clicked += OpenPort;

        // Get the "Close" button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        closeButton.clicked += ClosePort;
    }

    private void OpenPort()
    {
        int baudRate = 0;
        if (Int32.TryParse(_baudRateField.value, out baudRate))
        {
            SimpitPlugin.Instance.OpenPort(_serialPortField.value, baudRate);
        }
        else
        {
            GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Passive,
                Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Invalid baud rate", _baudRateField.value) }
            });
        }
    }

    private void ClosePort()
    {
        SimpitPlugin.Instance.ClosePort();
        _debugTextLabel.text = DefaultDebugText;
        _debugTextCounter = 0;
    }

    public void SetConnectionStatus(string status)
    {
        _statusLabel.text = status;
    }

    public void SetDebugText(string text)
    {
        _debugTextCounter++;
        _debugTextCounter %= 100;
        _debugTextLabel.text = String.Format("{0:000}: {1}", _debugTextCounter, text);
    }
}