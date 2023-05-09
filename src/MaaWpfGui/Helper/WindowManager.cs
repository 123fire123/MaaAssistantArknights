// <copyright file="WindowManager.cs" company="MaaAssistantArknights">
// MaaWpfGui - A part of the MaaCoreArknights project
// Copyright (C) 2021 MistEO and Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Windows;
using MaaWpfGui.Constants;
using MaaWpfGui.Models;
using MaaWpfGui.Views.UI;
using Newtonsoft.Json;
using Serilog;
using Stylet;

namespace MaaWpfGui.Helper
{
    public class WindowManager : Stylet.WindowManager
    {
        public WindowManager(IViewManager viewManager, Func<IMessageBoxViewModel> messageBoxViewModelFactory, IWindowManagerConfig config)
    : base(viewManager, messageBoxViewModelFactory, config)
        {
        }

        private static readonly ILogger _logger = Log.ForContext<WindowManager>();

        private readonly bool _loadWindowPlacement = Convert.ToBoolean(ConfigurationHelper.GetValue(ConfigurationKeys.LoadWindowPlacement, bool.TrueString));
        private readonly bool _saveWindowPlacement = Convert.ToBoolean(ConfigurationHelper.GetValue(ConfigurationKeys.SaveWindowPlacement, bool.TrueString));
        private readonly bool _minimizeDirectly = bool.Parse(ConfigurationHelper.GetValue(ConfigurationKeys.MinimizeDirectly, bool.FalseString));
        private readonly bool _minimizeToTray = bool.Parse(ConfigurationHelper.GetValue(ConfigurationKeys.MinimizeToTray, bool.FalseString));

        /// <summary>
        /// Center other windows in MaaWpfGui.RootView
        /// </summary>
        private void MoveWindowToDisplay(Window window)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow.WindowState == WindowState.Normal)
            {
                // In Stylet, CreateWindow().WindowStartupLocation is CenterScreen or CenterOwner (if w.WSLoc == Manual && w.Left == NaN && w.Top == NaN && ...)
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = mainWindow!.Left + ((mainWindow.Width - window.Width) / 2);
                window.Top = mainWindow.Top + ((mainWindow.Height - window.Height) / 2);
            }
        }

        /// <inheritdoc/>
        protected override Window CreateWindow(object viewModel, bool isDialog, IViewAware ownerViewModel)
        {
            Window window = base.CreateWindow(viewModel, isDialog, ownerViewModel);
            if (window is RootView)
            {
                if (_loadWindowPlacement && GetConfiguration(out WindowPlacement wp))
                {
                    window.SourceInitialized += (s, e) =>
                    {
                        bool success = SetWindowPlacement(window, ref wp);
                        _logger.Information("Whether the window placement was set successfully: {Success}", success);
                    };
                }

                if (_loadWindowPlacement && _saveWindowPlacement)
                {
                    window.Closing += (s, e) =>
                    {
                        GetWindowPlacement(window, out WindowPlacement wp);
                        SetConfiguration(wp);
                    };
                }

                if (_minimizeDirectly)
                {
                    window.WindowState = WindowState.Minimized;
                }

                if (_minimizeDirectly && _minimizeToTray)
                {
                    window.ShowInTaskbar = false;
                    window.Visibility = Visibility.Hidden;
                }
            }
            else if (!isDialog && ownerViewModel == null)
            {
                MoveWindowToDisplay(window);
            }

            return window;
        }

        private bool SetConfiguration(WindowPlacement wp)
        {
            try
            {
                // 请在配置文件中修改该部分配置，暂不支持从GUI设置
                // Please modify this part of configuration in the configuration file.
                ConfigurationHelper.SetValue(ConfigurationKeys.LoadWindowPlacement, _loadWindowPlacement.ToString());
                ConfigurationHelper.SetValue(ConfigurationKeys.SaveWindowPlacement, _saveWindowPlacement.ToString());

                return ConfigurationHelper.SetValue(ConfigurationKeys.WindowPlacement, JsonConvert.SerializeObject(wp));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to serialize json string to {Key}", ConfigurationKeys.WindowPlacement);
            }

            return false;
        }

        private bool GetConfiguration(out WindowPlacement wp)
        {
            wp = default;
            var jsonStr = ConfigurationHelper.GetValue(ConfigurationKeys.WindowPlacement, string.Empty);
            if (string.IsNullOrEmpty(jsonStr))
            {
                return false;
            }

            try
            {
                wp = JsonConvert.DeserializeObject<WindowPlacement?>(jsonStr) ?? throw new Exception("Failed to parse json string");
                return true;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to deserialize json string from {Key}", ConfigurationKeys.WindowPlacement);
            }

            return false;
        }

        private bool SetWindowPlacement(WindowHandle window, ref WindowPlacement wp)
        {
            try
            {
                // Load window placement details for previous application session from application settings
                // Note - if window was closed on a monitor that is now disconnected from the computer,
                //        SetWindowPlacement will place the window onto a visible monitor.
                wp.Length = Marshal.SizeOf(typeof(WindowPlacement));
                wp.Flags = 0;

                // wp.ShowCmd = wp.ShowCmd == SwShowminimized ? SwShownormal : wp.ShowCmd;
                wp.ShowCmd = _minimizeDirectly ? SwShowminimized : SwShownormal;
                return SetWindowPlacement(window.Handle, ref wp);
            }
            catch
            {
                return false;
            }
        }

        private bool GetWindowPlacement(WindowHandle window, out WindowPlacement wp)
        {
            return GetWindowPlacement(window.Handle, out wp);
        }

        #region Win32 API declarations to set and get window placement

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WindowPlacement lpwndpl);

        private const int SwShownormal = 1;
        private const int SwShowminimized = 2;

        #endregion
    }
}
