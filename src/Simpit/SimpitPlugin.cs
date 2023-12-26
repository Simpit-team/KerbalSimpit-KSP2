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
using System.Reflection;
using System.Runtime.InteropServices;
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

    bool verbose = true;

    //Serial Port
    string SerialPortName;
    int SerialPortBaudRate;
    KSPSerialPort port;

    //Serial Data Management
    // To receive events from serial devices on channel i,
    // register a callback for onSerialReceivedArray[i].
    public EventDataObsolete<byte, object>[] onSerialReceivedArray = new EventDataObsolete<byte, object>[256];
    // To send a packet on channel i, call
    // toSerialArray[i].Fire()
    public EventDataObsolete<byte, object>[] toSerialArray = new EventDataObsolete<byte, object>[256];
    // To be notified when a message must be sent (to send a first
    // non-periodic message when a channel is subscribed for instance),
    // register a callback for onSerialChannelForceSendArray[i].
    public EventDataObsolete<byte, object>[] onSerialChannelForceSendArray = new EventDataObsolete<byte, object>[256];

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct HandshakePacket
    {
        public byte HandShakeType;
        public byte Payload;
    }


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

        const bool defaultVerbose = true;
        var verboseValue = Config.Bind<bool>("Settings section", "Verbose Mode", defaultVerbose, "Should verbose logs be generated");
        verbose = verboseValue.Value;

        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Using Serial Port \"{SerialPortName}\" with Baud Rate \"{SerialPortBaudRate}\"");

        //Initialize everything needed for Serial
        initSerial();

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
                GUILayout.Height(200),
                GUILayout.Width(250)
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
        GUILayout.Label(
            $"Serial Port: {Instance.SerialPortName} \n" +
            $"Baud Rate: {Instance.SerialPortBaudRate} \n" +
            $"Status: {Instance.port.portStatus}");

        //Add Buttons
        if (GUI.Button(new Rect(10, 140, 100, 50), "Open")) Instance.OpenPort();
        if (GUI.Button(new Rect(Instance._windowRect.width - 10 - 100, 140, 100, 50), "Close")) Instance.ClosePort();
		
		
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
        if (port.portStatus != KSPSerialPort.ConnectionStatus.CLOSED && port.portStatus != KSPSerialPort.ConnectionStatus.ERROR)
        {
            //Port already opened. Nothing to do.
            Logger.LogInfo(String.Format("Port {0} already opened. Nothing to do.", port.PortName));

            GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Passive,
                Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Port {0} already opened.Nothing to do.", port.PortName) }
            });
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
                    Primary = new NotificationLineItemData { LocKey = "Simpit: No Serial Port defined. Go to config (main menu -> mods) to set one." }
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

            GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Passive,
                Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Opened port {0}", portName) }
            });
        }
        else
        {
            Logger.LogInfo(String.Format("Unable to open port {0}", portName));

            GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Passive,
                Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Unable to open port {0}", portName) }
            });
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



    // Method that inits the ports. Will only be called once to initialize them when starting the mod. It will also open them.
    private void initSerial()
    {
        //Create the serial port
        port = new KSPSerialPort(SerialPortName, SerialPortBaudRate);

        for (int i = 254; i >= 0; i--)
        {
            this.onSerialReceivedArray[i] = new EventDataObsolete<byte, object>(String.Format("onSerialReceived{0}", i));
            this.toSerialArray[i] = new EventDataObsolete<byte, object>(String.Format("toSerial{0}", i));
            this.onSerialChannelForceSendArray[i] = new EventDataObsolete<byte, object>(String.Format("onSerialChannelForceSend{0}", i));
        }

        this.onSerialReceivedArray[CommonPackets.Synchronisation].Add(this.handshakeCallback);
        this.onSerialReceivedArray[InboundPackets.CloseSerialPort].Add(this.serialCalledClose);
        this.onSerialReceivedArray[InboundPackets.RegisterHandler].Add(this.registerCallback);
        this.onSerialReceivedArray[InboundPackets.DeregisterHandler].Add(this.deregisterCallback);
        this.onSerialReceivedArray[InboundPackets.RequestMessage].Add(this.requestMessageCallback);
    }

    private void handshakeCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        HandshakePacket hs;
        hs.Payload = 0x37;
        switch (payload[0])
        {
            case 0x00:
                if (verbose) Debug.Log(String.Format("KerbalSimpit: SYN received on port {0}. Replying.", port.PortName));

                //When handshake is started, unregister all channels to avoid duplication of messages when new channels are subscribed after an Arduino reset
                for (int idx = 0; idx < 255; idx++)
                {
                    toSerialArray[idx].Remove(port.sendPacket);
                }
                // Remove all messages not yet sent to make sure the next message sent is an SYNACK
                port.clearSendingQueue();

                port.portStatus = KSPSerialPort.ConnectionStatus.HANDSHAKE;
                hs.HandShakeType = 0x01;
                port.sendPacket(CommonPackets.Synchronisation, hs);
                break;
            case 0x01:
                if (verbose) Debug.Log(String.Format("KerbalSimpit: SYNACK received on port {0}. Replying.", port.PortName));
                port.portStatus = KSPSerialPort.ConnectionStatus.CONNECTED;
                hs.HandShakeType = 0x02;
                port.sendPacket(CommonPackets.Synchronisation, hs);
                break;
            case 0x02:
                byte[] verarray = new byte[payload.Length - 1];
                Array.Copy(payload, 1, verarray, 0,
                           (payload.Length - 1));
                string VersionString = System.Text.Encoding.UTF8.GetString(verarray);
                Debug.Log(String.Format("KerbalSimpit: ACK received on port {0}. Handshake complete, Resetting channels, Arduino library version '{1}'.", port.PortName, VersionString));
                port.removeAllPacketSubscriptionRecords();
                port.portStatus = KSPSerialPort.ConnectionStatus.CONNECTED;

                break;
        }
    }

    private void serialCalledClose(byte portID, object data)
    {
        // Spit out log that the port wants to be closed
        if (verbose)
        {
            Debug.Log(String.Format("KerbalSimpit: Serial port {0} asked to be closed", portID));
        }

        foreach (int packetID in port.getPacketSubscriptionList())
        {

            // Remove the callback of the serial port from the event caller
            toSerialArray[packetID].Remove(port.sendPacket);

            if (verbose)
            {
                Debug.Log(String.Format("KerbalSimpit: Serial port {0} unsubscribed from packet {1}", portID, packetID));
            }
        }

        ClosePort();
    }

    private void registerCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        byte idx;
        for (int i = payload.Length - 1; i >= 0; i--)
        {
            idx = payload[i];


            if (!port.isPacketSubscribedTo(idx))
            {
                if (verbose)
                {
                    Debug.Log(String.Format("KerbalSimpit: Serial port {0} subscribing to channel {1}", portID, idx));
                }
                // Adds the sendPacket method as a callback to the event that is called when a value in the toSerialArray is updated
                toSerialArray[idx].Add(port.sendPacket);
                onSerialChannelForceSendArray[idx].Fire(idx, null);
                // Adds a record of the port subscribing to a packet to a list stored in the port instance.
                port.addPacketSubscriptionRecord(idx);
            }
            else
            {
                if (verbose) Debug.Log(String.Format("KerbalSimpit: Serial port {0} trying to subscribe to channel {1} but is already subscribed. Ignoring it", portID, idx));
            }
        }
    }

    private void deregisterCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        byte idx;
        for (int i = payload.Length - 1; i >= 0; i--)
        {
            idx = payload[i];
            toSerialArray[idx].Remove(port.sendPacket);
            // Removes the record of a port subscribing to a packet from the port's internal record
            port.removePacketSubscriptionRecord(idx);
            if (verbose)
            {
                Debug.Log(String.Format("KerbalSimpit: Serial port {0} ubsubscribed from channel {1}", portID, idx));
            }
        }
    }

    private void requestMessageCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        byte channelID = payload[0];

        if (channelID == 0)
        {
            foreach (byte packetID in port.getPacketSubscriptionList())
            {
                onSerialChannelForceSendArray[packetID].Fire(packetID, null);
            }
        }
        else
        {
            onSerialChannelForceSendArray[channelID].Fire(channelID, null);
        }
    }
}

