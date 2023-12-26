using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using KSP.Game;
using KerbalSimpit.Serial;
using System.IO.Ports;
using KSP.Iteration.UI.Binding;
using System.Reflection;
//using System.ComponentModel.Primitives;

namespace Simpit;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class SimpitPlugin : BaseSpaceWarpPlugin
{
    // Useful in case some other mod wants to use this mod a dependency
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    // Singleton instance of the plugin class
    [PublicAPI] public static SimpitPlugin Instance { get; set; }

    // UI window state
    private bool _isWindowOpen;
    private Rect _windowRect;

    // AppBar button IDs
    private const string ToolbarFlightButtonID = "BTN-SimpitFlight";
    private const string ToolbarOabButtonID = "BTN-SimpitOAB";
    private const string ToolbarKscButtonID = "BTN-SimpitKSC";

    //Serial Port
    string SerialPortName;
    int SerialPortBaudRate;
    KSPSerialPort port;

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Assembly ass = Assembly.LoadFile(Path.Combine(PluginFolderPath, "assets", "lib", "System.ComponentModel.Primitives.dll"));
        Logger.LogDebug("Loaded dll: " + ass.ToString());
        ass = Assembly.LoadFile(Path.Combine(PluginFolderPath, "assets", "lib", "System.IO.Ports.dll"));
        Logger.LogDebug("Loaded dll: " + ass.ToString());

        Instance = this;

        // Fetch configuration values or create a default one if it does not exist
        const string defaultComPort = "COMxx";
        var comPortValue = Config.Bind<string>("Settings section", "Serial Port Name", defaultComPort, "Which Serial Port the controller uses. E.g. COM4");
        SerialPortName = comPortValue.Value;

        const int defaultBaudRate = 115200;
        var baudRateValue = Config.Bind<int>("Settings section", "Baud Rate", defaultBaudRate, "Which speed the Serial Port uses. E.g. 115200");
        SerialPortBaudRate = baudRateValue.Value;

        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Using Serial Port: {SerialPortName} with Baud Rate: {SerialPortBaudRate}");
        
        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            ModName,
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        // Register OAB AppBar Button
        Appbar.RegisterOABAppButton(
            ModName,
            ToolbarOabButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        // Register KSC AppBar Button
        Appbar.RegisterKSCAppButton(
            ModName,
            ToolbarKscButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            () =>
            {
                _isWindowOpen = !_isWindowOpen;
            }
        );
        
        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(SimpitPlugin).Assembly);
    }

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "Simpit",
                GUILayout.Height(350),
                GUILayout.Width(350)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
		// Add a Simpit icon to the upper left corner of the GUI
        GUI.Label(new Rect(9, 2, 29, 29), AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"));
		//Add a X to close the window to the upper right corner of the GUI
		if ( GUI.Button(new Rect(Instance._windowRect.width - 4 - 27, 4, 27, 27), AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/cross.png")) )
            Instance.CloseWindow();

        //Add a text to the GUI
        GUILayout.Label($"Using Serial Port: {Instance.SerialPortName} with Baud Rate: {Instance.SerialPortBaudRate}");

        //Add Buttons
        if (GUI.Button(new Rect(9, 100, 100, 50), "Open")) Instance.OpenPort();
        if (GUI.Button(new Rect(9, 170, 100, 50), "Close")) Instance.ClosePort();
		
		
        GUI.DragWindow(new Rect(0, 0, 10000, 500));
    }
	
	private void CloseWindow()
    {
		GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
		GameObject.Find(ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
		GameObject.Find(ToolbarKscButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        _isWindowOpen = false;
    }
    public void OpenPort()
    {
        //Create the Serial Port if necessary
        if (port == null) port = new KSPSerialPort(Instance.SerialPortName, Instance.SerialPortBaudRate);
        if (port.portStatus != KSPSerialPort.ConnectionStatus.CLOSED && port.portStatus != KSPSerialPort.ConnectionStatus.ERROR)
        {
            //Port already opened. Nothing to do.
            Logger.LogInfo(String.Format("Port {0} already opened. Nothing to do.", port.PortName));
            return;
        }

        String portName = port.PortName;
        if (portName.StartsWith("COM") || portName.StartsWith("/"))
        {
            if(portName.Equals("COMxx"))
            {
                Logger.LogWarning("port name is default for port " + port.ID + ". Please provide a specific port the Simpit configs in the main menu.");
                GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
                {
                    Tier = NotificationTier.Passive,
                    Primary = new NotificationLineItemData { LocKey = "No Simpit Serial Port defined. Go to config (main menu -> mods) to set one." }
                });
                return;
            }
        }
        else
        {
            Logger.LogWarning("no port name is defined for port " + port.ID + ". Please check the Simpit configs in the main menu.");
            return;
        }

        if (port.open())
        {
            Logger.LogInfo(String.Format("Opened port {0}", portName));
        }
        else
        {
            Logger.LogInfo(String.Format("Unable to open port {0}", portName));
        }

        // TODO Add this back in
        /* Removed this section while porting to KSP2 to get initial stuff going
        if (!DoEventDispatching)
            StartEventDispatch();
        */
    }

    private void ClosePort()
    {
        if (port == null)  return;

        port.close();
    }

    /*
    private void MyButtonPress()
    {
        Logger.LogInfo("MyButtonPress");
        // Try to get the currently active vessel, set its throttle to 100% and toggle on the landing gear
        try
        {
            var currentVessel = Vehicle.ActiveVesselVehicle;
            if (currentVessel != null)
            {
                currentVessel.SetMainThrottle(1.0f);
                currentVessel.SetGearState(true);
            }
        }
        catch (Exception) { }

        GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
        {
            Tier = NotificationTier.Passive,
            Primary = new NotificationLineItemData { LocKey = "You successfully pressed a Simpit Button" }
        });
    }

    private void MyButtonPress2()
    {
        Logger.LogInfo("MyButtonPress2");
        // Try to get the currently active vessel, set its throttle to 0% and toggle off the landing gear
        try
        {
            var currentVessel = Vehicle.ActiveVesselVehicle;
            if (currentVessel != null)
            {
                currentVessel.SetMainThrottle(0.0f);
                currentVessel.SetGearState(false);
            }
        }
        catch (Exception) { }
    }
    */

    public void OnPacketReceived(byte Type, byte ID, byte[] buf)
    {
        GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
        {
            Tier = NotificationTier.Passive,
            Primary = new NotificationLineItemData { LocKey = "Received Serial Packet" }
        });
    }
}

