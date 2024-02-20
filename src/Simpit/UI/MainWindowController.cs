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

    private const string DefaultDebugText = "Write here with 'printToKSP' function";

    // The UIDocument component of the window game object
    private UIDocument _window;

    // The elements of the window that we need to access
    private VisualElement _rootElement;
    private VisualElement[] _portElements = new VisualElement[SimpitPlugin.MAX_NUM_PORTS];
    private TextField[] _serialPortNameFields = new TextField[SimpitPlugin.MAX_NUM_PORTS];
    private TextField[] _baudRateFields = new TextField[SimpitPlugin.MAX_NUM_PORTS];
    private Label[] _statusLabels = new Label[SimpitPlugin.MAX_NUM_PORTS];
    private Label[] _debugTextLabels = new Label[SimpitPlugin.MAX_NUM_PORTS];

    private int[] _debugTextCounters = new int[SimpitPlugin.MAX_NUM_PORTS];

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

        // Center the window by default
        _rootElement.CenterByDefault();
        //_rootElement.StopMouseEventsPropagation();

        

        for (int i = 0; i < SimpitPlugin.MAX_NUM_PORTS; i++)
        {
            //Get all the containers for the ports
            _portElements[i] = _rootElement.Q<VisualElement>("port" + i);

            //Remove the unused ones
            if (i >= SimpitPlugin.Instance.ports.Length || SimpitPlugin.Instance.ports[i] == null)
            {
                _portElements[i].parent.Remove(_portElements[i]);
            }
            else
            {
                // Get the text fields and labels from the container
                _serialPortNameFields[i] = _portElements[i].Q<TextField>("serial-port");
                _baudRateFields[i] = _portElements[i].Q<TextField>("baud-rate");
                _statusLabels[i] = _portElements[i].Q<Label>("connection-status");
                _debugTextLabels[i] = _portElements[i].Q<Label>("debug-text");

                //Set values for the ports
                _serialPortNameFields[i].value = SimpitPlugin.Instance.ports[i].PortName;
                _baudRateFields[i].value = SimpitPlugin.Instance.ports[i].BaudRate.ToString();
                _statusLabels[i].text = $"{SimpitPlugin.Instance.ports[i].portStatus}";

                if(_debugTextLabels[i].text == "Debug text") _debugTextLabels[i].text = DefaultDebugText;
            }
        }

        // Get the exit button from the window
        var exitButton = _rootElement.Q<Button>("exit-button");
        exitButton.clicked += () => IsWindowOpen = false;

        // Get the "Open" button from the window
        var openButton = _rootElement.Q<Button>("open-button");
        openButton.clicked += OpenPorts;

        // Get the "Close" button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        closeButton.clicked += ClosePorts;
    }

    private void OpenPorts()
    {
        for (int i = 0; i < SimpitPlugin.MAX_NUM_PORTS && i < SimpitPlugin.Instance.ports.Length; i++)
        {
            int baudRate = 0;
            if (Int32.TryParse(_baudRateFields[i].value, out baudRate))
            {
                SimpitPlugin.Instance.OpenPort(i, _serialPortNameFields[i].value, baudRate);
                _debugTextLabels[i].text = DefaultDebugText;
                _debugTextCounters[i] = 0;
            }
            else
            {
                GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
                {
                    Tier = NotificationTier.Passive,
                    Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Invalid baud rate {0} at index {1}.", _baudRateFields[i].value, i) }
                });
            }
        }
    }

    private void ClosePorts()
    {
        for (int i = 0; i < SimpitPlugin.MAX_NUM_PORTS; i++)
        {
            SimpitPlugin.Instance.ClosePort(i);
        }
    }

    public void SetConnectionStatus(int portIndex, string status)
    {
        _statusLabels[portIndex].text = status;
    }

    public void SetDebugText(int portIndex, string text)
    {
        _debugTextCounters[portIndex]++;
        _debugTextCounters[portIndex] %= 1000;
        _debugTextLabels[portIndex].text = String.Format("{0:000}: {1}", _debugTextCounters[portIndex], text);
    }
}