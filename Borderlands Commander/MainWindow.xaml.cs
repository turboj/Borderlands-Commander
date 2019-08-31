﻿using System;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using static KeyBinding;
using System.Diagnostics;


namespace BorderlandsCommander
{

    public partial class MainWindow : Window
    {
        // System functions for listening to foreground window info.
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        // The delegate type for handling foreground window changes.
        delegate void WinEventDelegate(IntPtr _0, uint _1, IntPtr windowHandle, int _2, int _3, uint _4, uint _5);
        WinEventDelegate ForegroundWindowDelegate;

        // Handle variables for the window to use in receiving hotkey events.
        private IntPtr Handle;
        private HwndSource source;

        // The constant used to distinguish our own key events.
        private const int HotkeySignature = 9000;

        private KeyBinding[] KeyBindings = null;
        private KeyBinding[] SymbolBindings = null;
        private KeyBinding[] NumpadBindings = null;
        private KeyBinding F7Binding;

        // Our icon in the system tray.
        private System.Windows.Forms.NotifyIcon NotifyIcon = null;
        private System.Windows.Forms.MenuItem FeedbackMenuItem = null;
        private System.Windows.Forms.MenuItem SymbolBindingsMenuItem = null;
        private System.Windows.Forms.MenuItem NumpadBindingsMenuItem = null;
        private System.Windows.Forms.MenuItem UdpEnableMenuItem = null;


        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            App.ShowFeedback = Properties.Settings.Default.ShowFeedback;

            NotifyIcon = new System.Windows.Forms.NotifyIcon();
            NotifyIcon.Text = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            NotifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            NotifyIcon.Click += new EventHandler(OnNotifyIconClicked);
            NotifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();

            FeedbackMenuItem = new System.Windows.Forms.MenuItem();
            FeedbackMenuItem.Text = "Show Feedback";
            FeedbackMenuItem.Click += OnNotifyMenuFeedbackClicked;
            FeedbackMenuItem.Checked = App.ShowFeedback;
            NotifyIcon.ContextMenu.MenuItems.Add(FeedbackMenuItem);

            NotifyIcon.ContextMenu.MenuItems.Add("-");

            SymbolBindingsMenuItem = new System.Windows.Forms.MenuItem();
            SymbolBindingsMenuItem.Text = "Symbol Bindings";
            SymbolBindingsMenuItem.Click += OnNotifyMenuBindingsClicked;
            SymbolBindingsMenuItem.RadioCheck = true;
            NotifyIcon.ContextMenu.MenuItems.Add(SymbolBindingsMenuItem);

            NumpadBindingsMenuItem = new System.Windows.Forms.MenuItem();
            NumpadBindingsMenuItem.Text = "Numpad Bindings";
            NumpadBindingsMenuItem.Click += OnNotifyMenuBindingsClicked;
            NumpadBindingsMenuItem.RadioCheck = true;
            NotifyIcon.ContextMenu.MenuItems.Add(NumpadBindingsMenuItem);

            NotifyIcon.ContextMenu.MenuItems.Add("-");
            App.udpEnabled = Properties.Settings.Default.enableUdpPort;
            UdpEnableMenuItem = new System.Windows.Forms.MenuItem();
            UdpEnableMenuItem.Text = "Enable UDP Port";
            UdpEnableMenuItem.Click += OnUdpEnableMenuItemClicked;
            UdpEnableMenuItem.Checked = App.udpEnabled;
            NotifyIcon.ContextMenu.MenuItems.Add(UdpEnableMenuItem);
            var portItem= 
                NotifyIcon.ContextMenu.MenuItems.Add(
                    String.Format("UDP Port: {0}",
                    Properties.Settings.Default.udpPort));
            portItem.Enabled = false;
            NotifyIcon.ContextMenu.MenuItems.Add("-");

            var exitMenu = new System.Windows.Forms.MenuItem();
            exitMenu.Text = "Exit";
            exitMenu.Click += OnNotifyMenuExitClicked;
            NotifyIcon.ContextMenu.MenuItems.Add(exitMenu);


            
            Loaded += (_0, _1) => NotifyIcon.Visible = true;
        }



        // Perform our own initilization when the window initilizes. 
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            F7Binding = new KeyBinding(Key.F7, ToggleHotkeys);

            var controlUpBinding    = new KeyBinding( Key.Up,    Modifier.Control, () => App.MoveForwardBackward( 500) );
            var controlDownBinding  = new KeyBinding( Key.Down,  Modifier.Control, () => App.MoveForwardBackward(-500) );
            var controlLeftBinding  = new KeyBinding( Key.Left,  Modifier.Control, () => App.MoveLeftRight(-500)       );
            var controlRightBinding = new KeyBinding( Key.Right, Modifier.Control, () => App.MoveLeftRight( 500)       );

            var controlEndBinding = new KeyBinding(Key.End, Modifier.Control, () => BLIO.RunCommand("disconnect"));

            SymbolBindings = new KeyBinding[]
            {
                new KeyBinding(Key.Equals,       App.ToggleThirdPerson              ),
                new KeyBinding(Key.LeftBracket,  App.HalveSpeed                     ),
                new KeyBinding(Key.RightBracket, App.DoubleSpeed                    ),
                new KeyBinding(Key.Backslash,    App.ResetSpeed                     ),
                new KeyBinding(Key.Comma,        App.RestorePosition                ),
                new KeyBinding(Key.Period,       App.SavePosition                   ),
                new KeyBinding(Key.Semicolon,    () => BLIO.RunCommand("togglehud") ),
                new KeyBinding(Key.Quote,        App.ToggleDamageNumbers            ),
                new KeyBinding(Key.Slash,        App.TogglePlayersOnly              ),
                controlUpBinding,
                controlDownBinding,
                controlLeftBinding,
                controlRightBinding,
                controlEndBinding,
            };

            NumpadBindings = new KeyBinding[]
            {
                new KeyBinding(Key.NumOne,   App.HalveSpeed                     ),
                new KeyBinding(Key.NumTwo,   App.DoubleSpeed                    ),
                new KeyBinding(Key.NumThree, App.ResetSpeed                     ),
                new KeyBinding(Key.NumFour,  App.RestorePosition                ),
                new KeyBinding(Key.NumFive,  App.SavePosition                   ),
                new KeyBinding(Key.NumSix,   App.TogglePlayersOnly              ),
                new KeyBinding(Key.NumSeven, () => BLIO.RunCommand("togglehud") ),
                new KeyBinding(Key.NumEight, App.ToggleDamageNumbers            ),
                new KeyBinding(Key.NumNine,  App.ToggleThirdPerson              ),
                controlUpBinding,
                controlDownBinding,
                controlLeftBinding,
                controlRightBinding,
                controlEndBinding,
            };

            if (Properties.Settings.Default.NumpadBindings)
            {
                NumpadBindingsMenuItem.Checked = true;
                // Set up the dictionary entires for our keybindings and their callbacks.
                KeyBindings = NumpadBindings;
            }
            else
            {
                SymbolBindingsMenuItem.Checked = true;
                // Set up the dictionary entires for our keybindings and their callbacks.
                KeyBindings = SymbolBindings;
            }

            // Set up the handle variables for binding hotkeys, and set up our
            // callback method as the hook for receiving hotkey events.
            Handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(Handle);
            source.AddHook(OnHotkeyPressed);

            // Create the delegate to handle notifications of foreground window
            // changes, and register it with the system.
            ForegroundWindowDelegate = (_0, _1, windowHandle, _3, _4, _5, _6) => HandleForegroundWindow(windowHandle);
            SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, ForegroundWindowDelegate, 0, 0, 0);

            // Set up bindings if the foreground window is already Borderlands.
            HandleForegroundWindow(GetForegroundWindow());
        }


        private void OnNotifyIconClicked(object sender, EventArgs e)
        {
            // Show();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            e.Cancel = true;
            Hide();
        }

        private void OnNotifyMenuFeedbackClicked(object sender, EventArgs e)
        {
            App.ShowFeedback = !App.ShowFeedback;
            FeedbackMenuItem.Checked = App.ShowFeedback;
            Properties.Settings.Default.ShowFeedback = App.ShowFeedback;
            Properties.Settings.Default.Save();
        }

        private void OnUdpEnableMenuItemClicked(object sender, EventArgs e)
        {
            App.udpEnabled = !App.udpEnabled;
            UdpEnableMenuItem.Checked = App.udpEnabled;
            Properties.Settings.Default.enableUdpPort = App.udpEnabled;
            Properties.Settings.Default.Save();
        }

        private void OnNotifyMenuBindingsClicked(object sender, EventArgs e)
        {
            if (sender == SymbolBindingsMenuItem)
            {
                if (SymbolBindingsMenuItem.Checked)
                    return;

                SymbolBindingsMenuItem.Checked = true;
                NumpadBindingsMenuItem.Checked = false;

                Properties.Settings.Default.NumpadBindings = false;

                foreach (KeyBinding keyBinding in KeyBindings)
                    keyBinding.Unregister(Handle);

                KeyBindings = SymbolBindings;
            }
            else if (sender == NumpadBindingsMenuItem)
            {
                if (NumpadBindingsMenuItem.Checked)
                    return;

                NumpadBindingsMenuItem.Checked = true;
                SymbolBindingsMenuItem.Checked = false;

                Properties.Settings.Default.NumpadBindings = true;

                foreach (KeyBinding keyBinding in KeyBindings)
                    keyBinding.Unregister(Handle);

                KeyBindings = NumpadBindings;
            }

            foreach (KeyBinding keyBinding in KeyBindings)
                keyBinding.Register(Handle);

            Properties.Settings.Default.Save();
        }

        private void OnNotifyMenuExitClicked(object sender, EventArgs e)
        {
            NotifyIcon.Visible = false;
            Application.Current.Shutdown();
        }

        private void HandleForegroundWindow(IntPtr windowHandle)
        {
            bool BLIsForeground = false;

            // Get a combined list of processes matching BL2 and TPS, and
            // iterate over them.
            var BLProcesses = new List<Process>(Process.GetProcessesByName("Borderlands2"));
            BLProcesses.AddRange(Process.GetProcessesByName("BorderlandsPreSequel"));
            foreach (Process BL2Process in BLProcesses)
                // If the BL2 or TPS process's main window matches that of the
                // one which was just brought to the foreground, set our flag.
                if (BL2Process.MainWindowHandle == windowHandle)
                {
                    BLIsForeground = true;
                    break;
                }

            // If BL2 or TPS were switched to, create the correct BLIO object
            // for said game if we have not already, then register our hotkeys.
            if (BLIsForeground)
            {
                // Bind the F7 key.
                F7Binding.Register(Handle);
                // If the user has not disabled hotkeys, enable them now.
                if (HotkeysEnabled)
                    foreach (KeyBinding keyBinding in KeyBindings)
                        keyBinding.Register(Handle);

                UdpControl.BlWindowHandle = windowHandle;
            }
            // If another process was switched to, unregister our hotkeys.
            else
            {
                UdpControl.BlWindowHandle = UdpControl.INVALID_HANDLE_VALUE;
                // Unbind the F7 key.
                F7Binding.Unregister(Handle);
                // Disable all of our other hotkeys.
                foreach (KeyBinding keyBinding in KeyBindings)
                    keyBinding.Unregister(Handle);
            }
        }


        // The callback invoked by the system for our registered key events.
        private IntPtr OnHotkeyPressed(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // If this is not a hotkey event, or is not tagged as ours, ignore it.
            if (msg != 0x0312)
                return IntPtr.Zero;

            int keyBindingID = wParam.ToInt32();

            if (keyBindingID == F7Binding.ID)
            {
                F7Binding.Method();
                handled = true;
                return IntPtr.Zero;
            }

            foreach (KeyBinding keyBinding in KeyBindings)
                if (keyBindingID == keyBinding.ID)
                {
                    keyBinding.Method();
                    handled = true;
                    break;
                }

            return IntPtr.Zero;
        }


        private bool HotkeysEnabled = true;
        private void ToggleHotkeys()
        {
            HotkeysEnabled = !HotkeysEnabled;

            if (HotkeysEnabled)
                foreach (KeyBinding keyBinding in KeyBindings)
                    keyBinding.Register(Handle);
            else
                foreach (KeyBinding keyBinding in KeyBindings)
                    keyBinding.Unregister(Handle);

            string feedback = "Hotkeys: " + (HotkeysEnabled ? "Enabled" : "Disabled");
            App.PerformAction(null, feedback);
        }
    }
}
