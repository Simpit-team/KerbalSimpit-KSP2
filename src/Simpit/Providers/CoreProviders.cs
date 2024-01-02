using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using KSP.Game;
using KSP.IO;
using KSP.Iteration.UI.Binding;
using KSP.Messages;
using UnityEngine;

namespace Simpit.Providers
{
    public class KerbalSimpitEchoProvider : MonoBehaviour
    {
        private EventDataObsolete<byte, object> echoRequestEvent;
        private EventDataObsolete<byte, object> echoReplyEvent;
        private EventDataObsolete<byte, object> customLogEvent;
        private EventDataObsolete<byte, object> sceneChangeEvent;
        private EventDataObsolete<byte, object> controlledVesselChangeEvent;

        public ConcurrentQueue<NotificationData> notificationQueue = new ConcurrentQueue<NotificationData>();

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
            controlledVesselChangeEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("toSerial" + OutboundPackets.VesselChange);

            GameEvents.onFlightReady.Add(FlightReadyHandler);
            GameEvents.onGameSceneSwitchRequested.Add(FlightShutdownHandler);

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

        public void Update()
        {
            if (!notificationQueue.IsEmpty)
            {
                NotificationData notification;
                notificationQueue.TryDequeue(out notification);
                GameManager.Instance.Game.Notifications.ProcessNotification(notification);
            }
        }

        public void OnDestroy()
        {
            if (echoRequestEvent != null) echoRequestEvent.Remove(EchoRequestCallback);
            if (echoReplyEvent != null) echoReplyEvent.Remove(EchoReplyCallback);
            if (customLogEvent != null) customLogEvent.Remove(CustomLogCallback);

            GameEvents.onFlightReady.Remove(FlightReadyHandler);
            GameEvents.onGameSceneSwitchRequested.Remove(FlightShutdownHandler);
        }

        public void EchoRequestCallback(byte ID, object Data)
        {
            if (SimpitPlugin.Instance.config_verbose) SimpitPlugin.Instance.Logger.LogInfo(String.Format("Echo request on port {0}. Replying.", ID));
            SimpitPlugin.Instance.port.sendPacket(CommonPackets.EchoResponse, Data);
        }

        public void EchoReplyCallback(byte ID, object Data)
        {
            SimpitPlugin.Instance.Logger.LogInfo(String.Format("Echo reply received on port {0}.", ID));
        }

        public void CustomLogCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;

            byte logStatus = payload[0];
            String message = System.Text.Encoding.UTF8.GetString(payload.Skip(1).ToArray());

            SimpitGui.SetDebugText(message);

            if((logStatus & CustomLogBits.NoHeader) == 0)
            {
                message = "Simpit : " + message;
            }

            if ((logStatus & CustomLogBits.PrintToScreen) != 0)
            {
                notificationQueue.Enqueue(new NotificationData
                {
                    Tier = NotificationTier.Passive,
                    Primary = new NotificationLineItemData { LocKey = message }
                });
            }
            
            if ((logStatus & CustomLogBits.Verbose) == 0 || SimpitPlugin.Instance.config_verbose)
            {
                SimpitPlugin.Instance.Logger.LogInfo(message);
            }
        }

        private void FlightReadyHandler()
        {
            if (sceneChangeEvent != null)
            {
                sceneChangeEvent.Fire(OutboundPackets.SceneChange, 0x00);
            }
        }

        private void FlightShutdownHandler(GameEvents.FromToAction<GameScenes, GameScenes> scenes)
        {
            if (scenes.from == GameScenes.FLIGHT)
            {
                if (sceneChangeEvent != null)
                {
                    sceneChangeEvent.Fire(OutboundPackets.SceneChange, 0x01);
                }
            }
        }
    }
}
