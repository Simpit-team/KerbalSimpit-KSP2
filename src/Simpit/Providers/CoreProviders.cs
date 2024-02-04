using KSP.Game;
using KSP.Messages;
using UnityEngine;
using Simpit.UI;

namespace Simpit.Providers
{
    public class KerbalSimpitEchoProvider : MonoBehaviour
    {
        private EventDataObsolete<byte, object> echoRequestEvent;
        private EventDataObsolete<byte, object> echoReplyEvent;
        private EventDataObsolete<byte, object> customLogEvent;
        private EventDataObsolete<byte, object> sceneChangeEvent;
        private EventDataObsolete<byte, object> controlledVesselChangeEvent;

        bool isInFlightScene = false;

        public void Start()
        {
            DontDestroyOnLoad(this); // Make this provider persistent

            echoRequestEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + CommonPackets.EchoRequest);
            if (echoRequestEvent != null) echoRequestEvent.Add(EchoRequestCallback);
            echoReplyEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + CommonPackets.EchoResponse);
            if (echoReplyEvent != null) echoReplyEvent.Add(EchoReplyCallback);
            customLogEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.CustomLog);
            if (customLogEvent != null) customLogEvent.Add(CustomLogCallback);

            sceneChangeEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.SceneChange);
            SimpitPlugin.AddToDeviceHandler(SceneChangeProvider);
            //GameManager.Instance.Game.Messages.Subscribe<GameStateChangedMessage>(new Action<MessageCenterMessage>(OnGameStateChangedMessage));
            //GameManager.Instance.Game.Messages.Subscribe<FlightViewEnteredMessage>(new Action<MessageCenterMessage>(OnFlightViewEnteredMessage));
            //GameManager.Instance.Game.Messages.Subscribe<FlightViewLeftMessage>(new Action<MessageCenterMessage>(OnFlightViewLeftMessage));
            controlledVesselChangeEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.VesselChange);

            //deal with event related to a new vessel being controlled

            GameManager.Instance.Game.Messages.Subscribe<VesselDockedMessage>(new Action<MessageCenterMessage>((MessageCenterMessage mess) => {
                controlledVesselChangeEvent.Fire(OutboundPackets.VesselChange, VesselChangeValues.docking);
            }));

            GameManager.Instance.Game.Messages.Subscribe<VesselUndockedMessage>(new Action<MessageCenterMessage>((MessageCenterMessage mess) => {
                controlledVesselChangeEvent.Fire(OutboundPackets.VesselChange, VesselChangeValues.undocking);
            }));

            GameManager.Instance.Game.Messages.Subscribe<VesselChangedMessage>(new Action<MessageCenterMessage>((MessageCenterMessage mess) => {
                controlledVesselChangeEvent.Fire(OutboundPackets.VesselChange, VesselChangeValues.switching);
            }));
        }

        public void OnDestroy()
        {
            if (echoRequestEvent != null) echoRequestEvent.Remove(EchoRequestCallback);
            if (echoReplyEvent != null) echoReplyEvent.Remove(EchoReplyCallback);
            if (customLogEvent != null) customLogEvent.Remove(CustomLogCallback);
        }

        public void EchoRequestCallback(byte ID, object Data)
        {
            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.loggingQueueInfo.Enqueue(String.Format("Echo request on port {0}. Replying.", ID));
            SimpitPlugin.Instance.port.sendPacket(CommonPackets.EchoResponse, Data);
        }

        public void EchoReplyCallback(byte ID, object Data)
        {
            SimpitPlugin.Instance.loggingQueueInfo.Enqueue(String.Format("Echo reply received on port {0}.", ID));
        }

        public void CustomLogCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;

            byte logStatus = payload[0];
            String message = System.Text.Encoding.UTF8.GetString(payload.Skip(1).ToArray());

            MainWindowController.Instance.SetDebugText(message);

            if((logStatus & CustomLogBits.NoHeader) == 0)
            {
                message = "Simpit : " + message;
            }

            if ((logStatus & CustomLogBits.PrintToScreen) != 0)
            {
                SimpitPlugin.Instance.notificationQueue.Enqueue(new NotificationData
                {
                    Tier = NotificationTier.Passive,
                    Primary = new NotificationLineItemData { LocKey = message }
                });
            }
            
            if ((logStatus & CustomLogBits.Verbose) == 0 || SimpitPlugin.Instance.config_verbose)
            {
                SimpitPlugin.Instance.loggingQueueInfo.Enqueue(message);
            }
        }

        public void SceneChangeProvider()
        {
            //Both FlightView and Map3DView can mean you are controlling a ship
            //But the game can also be in Map3DView when in the tracking station
            //So to see if you are actually in control of your ship, test, if the navball is visible
            GameState currentState = GameManager.Instance.Game.GlobalGameState.GetGameState().GameState;
            bool isinFlightOrMap = (currentState == GameState.FlightView || currentState == GameState.Map3DView);
            bool navballVisible = false;
            try { navballVisible = GameManager.Instance.Game.ViewController.DataProvider.IsNavballVisible.GetValue(); } catch { }
            
            if (isinFlightOrMap && navballVisible) //In flight
            {
                if (!isInFlightScene) //Was not in flight
                {
                    //SimpitPlugin.Instance.loggingQueueDebug.Enqueue("Scene Change to Flight");
                    sceneChangeEvent.Fire(OutboundPackets.SceneChange, 0x00);
                    isInFlightScene = true;
                }
            }
            else //Not in flight
            {
                if (isInFlightScene) //Was in flight
                {
                    //SimpitPlugin.Instance.loggingQueueDebug.Enqueue("Scene Change exit Flight");
                    sceneChangeEvent.Fire(OutboundPackets.SceneChange, 0x01);
                    isInFlightScene = false;
                }
            }
        }
    }
}
