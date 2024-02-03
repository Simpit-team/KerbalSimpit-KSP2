using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using SpaceWarp;
using SpaceWarp.API.Mods;
using UnityEngine;
using KSP.Game;
using KerbalSimpit.Serial;
using System.Reflection;
using System.Runtime.InteropServices;
using Simpit.Providers;
using Simpit.External;
using System.Collections.Concurrent;
using Simpit.UI;
using KSP.Messages;
using BepInEx.Configuration;


//TODO Support multiple serial ports
//TODO CameraControl

//TODO Why are the EventData now called EventDataObsolete? Possible solution: Replace EventDataObsolete and GameEvents.XYZ with Messages and MessageCenter
//TODO FlightProvider.cs: There is the simVessel.ControlStatus (which is a VesselControlState, it has NoControl, NoCommNet, FullControlHibernation, FullControl) which is currently in use and there is simVessel._commandControlState (which is a CommandControlState , it has Disabled, NothEnoughCrew, NotEnoughResources, NoCommnetConnection, Hibernating, FullyFunctional). Should the latter be added?
//TODO FlightProvider.cs: There is additional athmospheric info available in KSP2 in KSP.Sim.impl.TelemetryComponent: AtmosphericHumidityPercentage, ExternalTemperature, DynamicPressure_kPa, SoundSpeed, MachNumber
//TODO FlightProvider.cs: Get a better CommNet signal strength. KSP2 currently only offers "Has connection" or "No connection". Signal strength would have to be calculated manually. Not doing it for now.
//TODO WarpControl.cs : New Feature: Allow Timewarp to PE and AP if they are in the next SOI.

namespace Simpit;

public delegate void ToDeviceCallback();

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
    //Using queues for thread safe logging
    public ConcurrentQueue<string> loggingQueueInfo = new ConcurrentQueue<string>();
    public ConcurrentQueue<string> loggingQueueDebug = new ConcurrentQueue<string>();
    public ConcurrentQueue<string> loggingQueueWarning = new ConcurrentQueue<string>();
    public ConcurrentQueue<string> loggingQueueError = new ConcurrentQueue<string>();
    public ConcurrentQueue<NotificationData> notificationQueue = new ConcurrentQueue<NotificationData>();

    public bool config_verbose;
    int config_refreshRate;
    //Serial Port
    public string config_SerialPortName;
    public int config_SerialPortBaudRate;
    public ConfigEntry<string> configEntrySeialPortName;
    public ConfigEntry<int> configEntrySeialPortBaudRate;
    public KSPSerialPort port;

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

    GameObject providers;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serializable]
    public struct HandshakePacket
    {
        public byte HandShakeType;
        public byte Payload;
    }

    private static List<ToDeviceCallback> RegularEventList = new List<ToDeviceCallback>(255);
    private bool DoEventDispatching = false;
    private Thread EventDispatchThread;

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Assembly ass = Assembly.LoadFile(Path.Combine(SWMetadata.Folder.FullName, "assets", "lib", "System.ComponentModel.Primitives.dll"));
        Logger.LogDebug("Loaded dll: " + ass.ToString());
        ass = Assembly.LoadFile(Path.Combine(SWMetadata.Folder.FullName, "assets", "lib", "System.IO.Ports.dll"));
        Logger.LogDebug("Loaded dll: " + ass.ToString());

        Instance = this;

        ReadConfig();

        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Using Serial Port \"{config_SerialPortName}\" with Baud Rate \"{config_SerialPortBaudRate}\"");

        //Initialize everything needed for Serial
        InitSerial();

        //Initialize everything needed for the Providers
        InitProviders();

        MainWindowController.Init(ModGuid, ModName);

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(SimpitPlugin).Assembly);

        GameManager.Instance.Game.Messages.Subscribe<GameLoadFinishedMessage>(new Action<MessageCenterMessage>(OpenPortOnGameLoaded));
    }

    public void Update()
    {
        if (!notificationQueue.IsEmpty)
        {
            notificationQueue.TryDequeue(out NotificationData notification);
            GameManager.Instance.Game.Notifications.ProcessNotification(notification);
        }

        while (!loggingQueueInfo.IsEmpty)
        {
            loggingQueueInfo.TryDequeue(out string log);
            Logger.LogInfo(log);
        }
        while (!loggingQueueDebug.IsEmpty)
        {
            loggingQueueDebug.TryDequeue(out string log);
            Logger.LogDebug(log);
        }
        while (!loggingQueueWarning.IsEmpty)
        {
            loggingQueueWarning.TryDequeue(out string log);
            Logger.LogWarning(log);
        }
        while (!loggingQueueError.IsEmpty)
        {
            loggingQueueError.TryDequeue(out string log);
            Logger.LogError(log);
        }
    }

    public void ReadConfig()
    {
        // Fetch configuration values or create a default one if it does not exist
        const string defaultComPort = "COMxx";
        configEntrySeialPortName = Config.Bind<string>("Settings section", "Serial Port Name", defaultComPort, "Which Serial Port the controller uses. E.g. COM4");
        config_SerialPortName = configEntrySeialPortName.Value;

        const int defaultBaudRate = 115200;
        configEntrySeialPortBaudRate = Config.Bind<int>("Settings section", "Baud Rate", defaultBaudRate, "Which speed the Serial Port uses. E.g. 115200");
        config_SerialPortBaudRate = configEntrySeialPortBaudRate.Value;

        const bool defaultVerbose = false;
        var verboseValue = Config.Bind<bool>("Settings section", "Verbose Mode", defaultVerbose, "Should verbose logs be generated");
        config_verbose = verboseValue.Value;

        const int defaultRefreshRate = 125;
        var refreshRateValue = Config.Bind<int>("Settings section", "Refresh Rate", defaultRefreshRate, "Refresh rate in milliseconds. E.g. 125");
        config_refreshRate = refreshRateValue.Value;

        if (port != null) 
        { 
            if (port.PortName != config_SerialPortName || port.BaudRate != config_SerialPortBaudRate) port.ChangePort(config_SerialPortName, config_SerialPortBaudRate); 
        }
    }

    public void OpenPortOnGameLoaded(MessageCenterMessage msg)
    {
        //Try to open port on Game Start
        OpenPort(config_SerialPortName, config_SerialPortBaudRate);
    }

    public void OpenPort(string portName, int baudRate)
    {
        if (port == null) return;

        if (port.portStatus != KSPSerialPort.ConnectionStatus.CLOSED && port.portStatus != KSPSerialPort.ConnectionStatus.ERROR)
        {
            //Port already opened. Nothing to do.
            Logger.LogInfo(String.Format("Port {0} already opened. Nothing to do.", port.PortName));

            GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Passive,
                Primary = new NotificationLineItemData { LocKey = String.Format("Simpit: Port {0} already opened. Nothing to do.", port.PortName) }
            });
            return;
        }

        if (portName.StartsWith("COM") || portName.StartsWith("/"))
        {
            if(portName.Equals("COMxx"))
            {
                Logger.LogWarning("port name is default for port " + port.ID + ". Please provide a specific port in the Simpit UI or the mod configs.");
                GameManager.Instance.Game.Notifications.ProcessNotification(new NotificationData
                {
                    Tier = NotificationTier.Passive,
                    Primary = new NotificationLineItemData { LocKey = "Simpit: No Serial Port defined. Go to the Simpit UI or the mod configs to set one." }
                });
                return;
            }
        }
        else
        {
            Logger.LogWarning("no valid port name is defined for port " + port.ID + ". Go to the Simpit UI or the mod configs to check it.");
            return;
        }

        if (port.ChangePort(portName, baudRate) && port.open())
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

        if (!DoEventDispatching)
            StartEventDispatch();
    }

    public void ClosePort()
    {
        if (port == null) return;

        port.close();
    }

    // Method that inits the ports. Will only be called once to initialize them when starting the mod.
    private void InitSerial()
    {
        //Create the serial port
        port = new KSPSerialPort(config_SerialPortName, config_SerialPortBaudRate);

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

    private void InitProviders()
    {
        providers = new GameObject();
        providers.AddComponent<KerbalSimpitEchoProvider>();
        
        providers.AddComponent<KerbalSimpitAxisController>();
        providers.AddComponent<RotationCommandProvider>();
        providers.AddComponent<TranslationCommandProvider>();
        providers.AddComponent<WheelCommandProvider>();
        providers.AddComponent<ThrottleCommandProvider>();

        providers.AddComponent<KerbalSimpitActionProvider>();
        providers.AddComponent<KerbalSimpitTelemetryProvider>();
        providers.AddComponent<KerbalSimpitWarpControl>();
        providers.AddComponent<KerbalSimpitNavBallProvider>();
        providers.AddComponent<AtmoConditionProvider>();
        providers.AddComponent<FlightStatusProvider>();
        providers.AddComponent<VesselNameProvider>();
        providers.AddComponent<SOINameProvider>();
        providers.AddComponent<KerbalSimpitCAGProvider>();
        providers.AddComponent<KeyboardEmulator>();

        providers.AddComponent<MonoPropellantProvider>();
        providers.AddComponent<SolidFuelProvider>();
        providers.AddComponent<SolidFuelStageProvider>();
        providers.AddComponent<IntakeAirProvider>();
        //providers.AddComponent<TestRocksProvider>();
        providers.AddComponent<EvaPropellantProvider>();
        providers.AddComponent<HydrogenProvider>();
        providers.AddComponent<HydrogenStageProvider>();
        providers.AddComponent<LiquidFuelProvider>();
        providers.AddComponent<LiquidFuelStageProvider>();
        providers.AddComponent<OxidizerProvider>();
        providers.AddComponent<OxidizerStageProvider>();
        //providers.AddComponent<MethaloxProvider>();
        //providers.AddComponent<MethaloxStageProvider>();
        //providers.AddComponent<MethaneAirProvider>();
        //providers.AddComponent<MethaneAirStageProvider>();
        providers.AddComponent<UraniumProvider>();
        providers.AddComponent<ElectricChargeProvider>();
        providers.AddComponent<XenonGasProvider>();
        providers.AddComponent<XenonGasStageProvider>();
        //providers.AddComponent<XenonECProvider>();
        //providers.AddComponent<XenonECStageProvider>();
        providers.AddComponent<AblatorProvider>();
        //providers.AddComponent<AblatorStageProvider>();
        providers.AddComponent<KerbalSimpitTargetProvider>();
    }

    private void StartEventDispatch()
    {
        this.EventDispatchThread = new Thread(this.EventWorker);
        this.EventDispatchThread.Start();
        while (!this.EventDispatchThread.IsAlive) ;
    }

    public static void AddToDeviceHandler(ToDeviceCallback cb)
    {
        RegularEventList.Add(cb);
    }

    public static bool RemoveToDeviceHandler(ToDeviceCallback cb)
    {
        return RegularEventList.Remove(cb);
    }

    private void EventWorker()
    {
        Action EventNotifier = null;
        ToDeviceCallback[] EventListCopy = new ToDeviceCallback[255];
        int EventCount;
        int TimeSlice;
        EventNotifier = delegate {
            EventCount = RegularEventList.Count;
            RegularEventList.CopyTo(EventListCopy);
            if (EventCount > 0)
            {
                TimeSlice = config_refreshRate / EventCount;
                for (int i = EventCount; i >= 0; --i)
                {
                    if (EventListCopy[i] != null)
                    {
                        EventListCopy[i]();
                        Thread.Sleep(TimeSlice);
                    }
                }
            }
            else
            {
                Thread.Sleep(config_refreshRate);
            }
        };
        DoEventDispatching = true;
        Logger.LogInfo("Starting event dispatch loop");
        while (DoEventDispatching)
        {
            EventNotifier();
        }
        Logger.LogInfo("Event dispatch loop exiting");
    }

    private void handshakeCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        HandshakePacket hs;
        hs.Payload = HandshakeValues.KerbalSpaceProgram2;
        switch (payload[0])
        {
            case 0x00:
                if (config_verbose) Logger.LogInfo(String.Format("SYN received on port {0}. Replying.", port.PortName));

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
                if (config_verbose) Logger.LogInfo(String.Format("SYNACK received on port {0}. Replying.", port.PortName));
                port.portStatus = KSPSerialPort.ConnectionStatus.CONNECTED;
                hs.HandShakeType = 0x02;
                port.sendPacket(CommonPackets.Synchronisation, hs);
                break;
            case 0x02:
                byte[] verarray = new byte[payload.Length - 1];
                Array.Copy(payload, 1, verarray, 0,
                           (payload.Length - 1));
                string VersionString = System.Text.Encoding.UTF8.GetString(verarray);
                Logger.LogInfo(String.Format("ACK received on port {0}. Handshake complete, Resetting channels, Arduino library version '{1}'.", port.PortName, VersionString));
                port.removeAllPacketSubscriptionRecords();
                port.portStatus = KSPSerialPort.ConnectionStatus.CONNECTED;

                break;
        }
    }

    private void serialCalledClose(byte portID, object data)
    {
        // Spit out log that the port wants to be closed
        if (config_verbose)
        {
            Logger.LogInfo(String.Format("Serial port {0} asked to be closed", portID));
        }

        foreach (int packetID in port.getPacketSubscriptionList())
        {

            // Remove the callback of the serial port from the event caller
            toSerialArray[packetID].Remove(port.sendPacket);

            if (config_verbose)
            {
                Logger.LogInfo(String.Format("Serial port {0} unsubscribed from packet {1}", portID, packetID));
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
                if (config_verbose)
                {
                    Logger.LogInfo(String.Format("Serial port {0} subscribing to channel {1}", portID, idx));
                }
                // Adds the sendPacket method as a callback to the event that is called when a value in the toSerialArray is updated
                toSerialArray[idx].Add(port.sendPacket);
                onSerialChannelForceSendArray[idx].Fire(idx, null);
                // Adds a record of the port subscribing to a packet to a list stored in the port instance.
                port.addPacketSubscriptionRecord(idx);
            }
            else
            {
                if (config_verbose) Logger.LogInfo(String.Format("Serial port {0} trying to subscribe to channel {1} but is already subscribed. Ignoring it", portID, idx));
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
            if (config_verbose)
            {
                Logger.LogInfo(String.Format("Serial port {0} ubsubscribed from channel {1}", portID, idx));
            }
        }
    }

    private void requestMessageCallback(byte portID, object data)
    {
        byte[] payload = (byte[])data;
        byte channelID = payload[0];

        if (channelID == 0)
        {
            if (config_verbose) Logger.LogInfo(String.Format("Request resending all channels"));
            foreach (byte packetID in port.getPacketSubscriptionList())
            {
                onSerialChannelForceSendArray[packetID].Fire(packetID, null);
            }
        }
        else
        {
            if (config_verbose) Logger.LogInfo(String.Format("Request resending on channel {0}", channelID));
            onSerialChannelForceSendArray[channelID].Fire(channelID, null);
        }
    }
}

