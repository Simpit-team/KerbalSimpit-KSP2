﻿using Simpit.Utilities;
using System.Runtime.InteropServices;
using UnityEngine;
using WindowsInput;
using WindowsInput.Native;

namespace Simpit.External
{
    class KeyboardEmulator : MonoBehaviour
    {

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [Serializable]
        public struct KeyboardEmulatorStruct
        {
            public byte modifier;
            public Int16 key;
        }

        private EventDataObsolete<byte, object> keyboardEmulatorEvent;

        private InputSimulator input;

        public void Start()
        {
            keyboardEmulatorEvent = GameEvents.FindEvent<EventDataObsolete<byte, object>>("onSerialReceived" + InboundPackets.KeyboardEmulator);
            if (keyboardEmulatorEvent != null) keyboardEmulatorEvent.Add(KeyboardEmulatorCallback);

            input = new InputSimulator();
        }

        public void OnDestroy()
        {
            if (keyboardEmulatorEvent != null) keyboardEmulatorEvent.Remove(KeyboardEmulatorCallback);
        }

        public void KeyboardEmulatorCallback(byte ID, object Data)
        {
            try
            {
                KeyboardEmulatorStruct payload = KerbalSimpitUtils.ByteArrayToStructure<KeyboardEmulatorStruct>((byte[])Data);

                Int32 key32 = payload.key; //To cast it in the enum, we need a Int32 but only a Int16 is sent

                if (Enum.IsDefined(typeof(VirtualKeyCode), key32))
                {
                    VirtualKeyCode key = (VirtualKeyCode)key32;
                    if ((payload.modifier & KeyboardEmulatorModifier.ALT_MOD) != 0)
                    {
                        input.Keyboard.KeyDown(VirtualKeyCode.MENU);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.CTRL_MOD) != 0)
                    {
                        input.Keyboard.KeyDown(VirtualKeyCode.CONTROL);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.SHIFT_MOD) != 0)
                    {
                        // Use LSHIFT instead of SHIFT since some function (like SHIFT+Tab to cycle through bodies in map view) only work with left shift.
                        // This requires a custom version of the WindowsInput library to properly handle it.
                        input.Keyboard.KeyDown(VirtualKeyCode.LSHIFT);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.KEY_DOWN_MOD) != 0)
                    {
                        Debug.Log("Simpit emulates key down of " + key);
                        input.Keyboard.KeyDown(key);
                    }
                    else if ((payload.modifier & KeyboardEmulatorModifier.KEY_UP_MOD) != 0)
                    {
                        Debug.Log("Simpit emulates key up of " + key);
                        input.Keyboard.KeyUp(key);
                    }
                    else
                    {
                        Debug.Log("Simpit emulates keypress of " + key);
                        input.Keyboard.KeyPress(key);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.ALT_MOD) != 0)
                    {
                        input.Keyboard.KeyUp(VirtualKeyCode.MENU);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.CTRL_MOD) != 0)
                    {
                        input.Keyboard.KeyUp(VirtualKeyCode.CONTROL);
                    }

                    if ((payload.modifier & KeyboardEmulatorModifier.SHIFT_MOD) != 0)
                    {
                        input.Keyboard.KeyUp(VirtualKeyCode.LSHIFT);
                    }
                }
                else
                {
                    Debug.Log("Simpit : I received a message to emulate a keypress of key " + payload.key + " but I do not recognize it. I ignore it.");
                }

            }
            catch (DllNotFoundException exception)
            {
                Debug.LogWarning("Simpit : I received a message to emulate a keypress. This is currently only available on Windows. I ignore it.");
                if (SimpitPlugin.Instance.config_verbose)
                {
                    Debug.LogWarning(exception.Message);
                    Debug.LogWarning(exception.ToString());
                }
            }

        }

    }




}