﻿using KSP.Game;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;
using KSP.UI.Binding;
using Simpit.Providers;
using Simpit.Utilities;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Game;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;

namespace Simpit
{
    public class SimpitGui
    {
        // UI window state
        private static bool _isWindowOpen;
        private static Rect _windowRect;

        // AppBar button IDs
        private const string ToolbarFlightButtonID = "BTN-SimpitFlight";
        private const string ToolbarOabButtonID = "BTN-SimpitOAB";
        private const string ToolbarKscButtonID = "BTN-SimpitKSC";

        static string debugText = "Show stuff here with SimpitGui.SetDebugText()";
        static int debugTextChangeCounter = 0;

        public void InitGui()
        {
            // Register Flight AppBar button
            Appbar.RegisterAppButton(
                SimpitPlugin.ModName,
                ToolbarFlightButtonID,
                AssetManager.GetAsset<Texture2D>($"{SimpitPlugin.ModGuid}/images/icon.png"),
                isOpen =>
                {
                    SimpitPlugin.Instance.ReadConfig();
                    _isWindowOpen = isOpen;
                    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
                }
            );

            // Register OAB AppBar Button
            Appbar.RegisterOABAppButton(
                SimpitPlugin.ModName,
                ToolbarOabButtonID,
                AssetManager.GetAsset<Texture2D>($"{SimpitPlugin.ModGuid}/images/icon.png"),
                isOpen =>
                {
                    SimpitPlugin.Instance.ReadConfig();
                    _isWindowOpen = isOpen;
                    GameObject.Find(ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
                }
            );

            // Register KSC AppBar Button
            Appbar.RegisterKSCAppButton(
                SimpitPlugin.ModName,
                ToolbarKscButtonID,
                AssetManager.GetAsset<Texture2D>($"{SimpitPlugin.ModGuid}/images/icon.png"),
                () =>
                {
                    SimpitPlugin.Instance.ReadConfig();
                    _isWindowOpen = !_isWindowOpen;
                }
            );
        }


        /// <summary>
        /// Draws a the UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
        /// </summary>
        public void OnGui()
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
                    GUILayout.Height(310),
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
            GUI.Label(new Rect(9, 2, 29, 29), AssetManager.GetAsset<Texture2D>($"{SimpitPlugin.ModGuid}/images/icon.png"));
            //Add a X to close the window to the upper right corner of the GUI
            if (GUI.Button(new Rect(_windowRect.width - 4 - 27, 4, 27, 27), AssetManager.GetAsset<Texture2D>($"{SimpitPlugin.ModGuid}/images/cross.png")))
                CloseWindow();

            //Add a text to the GUI
            GUILayout.Label(
                $"Serial Port: {SimpitPlugin.Instance.config_SerialPortName} \n" +
                $"Baud Rate: {SimpitPlugin.Instance.config_SerialPortBaudRate} \n" +
                $"Status: {SimpitPlugin.Instance.port.portStatus}");
            
            //Add the debug text to the GUI
            GUILayout.Label(debugText);

            //Add Buttons
            if (GUI.Button(new Rect(10, 190, 100, 50), "Open")) OnButtonOpenPort();
            if (GUI.Button(new Rect(_windowRect.width - 10 - 100, 190, 100, 50), "Close")) OnButtonClosePort();
            if (GUI.Button(new Rect(_windowRect.width/ 2 - 50, 250, 100, 50), "Debug")) OnDebugButtonPress();


            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        private static void CloseWindow()
        {
            GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
            GameObject.Find(ToolbarOabButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
            GameObject.Find(ToolbarKscButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
            _isWindowOpen = false;
        }

        static void OnButtonClosePort()
        {
            SimpitPlugin.Instance.ClosePort();
        }

        static void OnButtonOpenPort()
        {
            SimpitPlugin.Instance.OpenPort();
        }

        static void OnDebugButtonPress()
        {
            KerbalSimpitUtils.PrintAllAvailableResources();
            SimpitPlugin.Instance.Logger.LogDebug("DebugButtonPress");
        }

        public static void SetDebugText(string text)
        {
            debugTextChangeCounter++;
            debugTextChangeCounter %= 100;
            debugText = String.Format("{0:000} {1}", debugTextChangeCounter, text);
        }
    }
}
