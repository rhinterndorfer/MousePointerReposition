using FMUtils.KeyboardHook;
using Microsoft.Win32;
using MousePointerReposition.Helper;
using MousePointerReposition.ViewModel;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace MousePointerReposition
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        #region constants

        /// <summary>
        /// Timeout waiting for new foreground application 
        /// </summary>
        private const double CHECK_TIMEOUT = 5000; // ms

        /// <summary>
        /// Period for detecting multiple CTRL key pressing
        /// </summary>
        private const long CTRL_KEY_TIMEOUT = 1000; // ms

        #endregion constants


        #region fields

        private Vanara.PInvoke.RECT foreGroundWindowRectStart;
        private WindowState windowState;
        private bool showInTaskbar;
        private bool autostart;
        private RelayCommand loaded;
        private RelayCommand closing;
        private RelayCommand exit;
        private RelayCommand hide;
        private bool? disableAltTab;
        private bool? disableWinLeftRight;
        private bool? disableManualReposition;
        private bool autoStartDisabled;
        private HWND foregroundWindowHandleStart;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion fields


        #region private properties

        private NotifyIcon NotifyIcon { get; set; }
        private Hook KeyboardHook { get; set; }
        private double CheckPeriod { get; set; }
        private bool IsMousePositioningTriggered { get; set; }

        private bool IsManualMousePositioningTriggered { get; set; }
        private int CtrlKeyPressCounter { get; set; }
        private DateTime CtrlKeyPressLast { get; set; }
        private System.Timers.Timer RepositioningTimer { get; set; }

        #endregion private properties


        #region public properties

        /// <summary>
        /// Window loaded command
        /// </summary>
        public RelayCommand Loaded
        {
            get
            {
                if (loaded == null)
                    loaded = new RelayCommand(OnLoaded);
                return loaded;
            }
        }

        /// <summary>
        /// Window closing command
        /// </summary>
        public RelayCommand Closing
        {
            get
            {
                if (closing == null)
                    closing = new RelayCommand(OnClosing);
                return closing;
            }
        }

        /// <summary>
        /// Close programm
        /// </summary>
        public RelayCommand Exit
        {
            get
            {
                if (exit == null)
                    exit = new RelayCommand(OnExit);
                return exit;
            }
        }

        /// <summary>
        /// Hide window
        /// </summary>
        public RelayCommand Hide
        {
            get
            {
                if (hide == null)
                    hide = new RelayCommand(OnHide);
                return hide;
            }
        }

        

        /// <summary>
        /// Window state
        /// </summary>
        public WindowState WindowState
        {
            get => windowState;
            set
            {
                windowState = value;
                OnPropertyChanged(nameof(WindowState));
            }
        }

        /// <summary>
        /// Show in taskbar
        /// </summary>
        public bool ShowInTaskbar
        {
            get => showInTaskbar;
            set
            {
                showInTaskbar = value;
                OnPropertyChanged(nameof(ShowInTaskbar));
            }
        }





        public bool AutoStartDisabled
        {
            get => autoStartDisabled; set
            {
                autoStartDisabled = value;
                OnPropertyChanged(nameof(AutoStartDisabled));
            }
        }

        /// <summary>
        /// Autostart
        /// </summary>
        public bool Autostart
        {
            get
            {
                return autostart;
            }
            set
            {
                if (autostart != value) // value changed
                {
                    autostart = value;

#if _UWP
                    var startupTask = GetStartupTask();
                    if (value)
                    {
                        var task = startupTask.RequestEnableAsync().AsTask();
                        task.Wait();
                        SetAutostartState(task.Result);
                    }
                    else
                        startupTask.Disable();
#else
                    RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    if (value)
                    {
                        string assemblyPath = Assembly.GetExecutingAssembly().Location;
                        rkApp.SetValue("MousePointerReposition", assemblyPath, RegistryValueKind.String);
                    }
                    else
                    {
                        rkApp.DeleteValue("MousePointerReposition");
                    }
                    
#endif
                    OnPropertyChanged(nameof(Autostart));
                }
            }
        }

        /// <summary>
        ///  Disable ALT+TAB key combination
        /// </summary>
        public bool DisableAltTab
        {
            get
            {
                if (!disableAltTab.HasValue)
                    disableAltTab = SettingsHelper.Instance.DisableAltTab;
                return disableAltTab ?? false;
            }
            set
            {
                disableAltTab = value;
                SettingsHelper.Instance.DisableAltTab = value;
                OnPropertyChanged(nameof(DisableAltTab));
            }
        }

        /// <summary>
        ///  Disable WIN+LEFT or RIGHT key combination
        /// </summary>
        public bool DisableWinLeftRight
        {
            get
            {
                if (!disableWinLeftRight.HasValue)
                    disableWinLeftRight = SettingsHelper.Instance.DisableWinLeftRight;
                return disableWinLeftRight ?? false;
            }
            set
            {
                disableWinLeftRight = value;
                SettingsHelper.Instance.DisableWinLeftRight = value;
                OnPropertyChanged(nameof(DisableWinLeftRight));
            }

        }

        /// <summary>
        /// Disable manual cursor repositioning (2x CTRL key)
        /// </summary>
        public bool DisableManuelPositioning
        {
            get
            {
                if (!disableManualReposition.HasValue)
                    disableManualReposition = SettingsHelper.Instance.DisableManualCursorReposition;
                return disableManualReposition ?? false;
            }
            set
            {
                disableManualReposition = value;
                SettingsHelper.Instance.DisableManualCursorReposition = value;
                OnPropertyChanged(nameof(DisableManuelPositioning));
            }
        }

        #endregion public properties


        #region .ctor

        /// <summary>
        /// .ctor
        /// </summary>
        public MainWindowVM()
        {
            App.logger.Debug("MainWindowVM entry");
            App.logger.Information($"Version/build information: {Assembly.GetExecutingAssembly().ImageRuntimeVersion} / {Assembly.GetExecutingAssembly().FullName}");


            WindowState = WindowState.Minimized;
            ShowInTaskbar = true;

            // timer 
            RepositioningTimer = new System.Timers.Timer(200);
            RepositioningTimer.Elapsed += RepositioningTimer_Elapsed;
            RepositioningTimer.AutoReset = false;

            // setup keyboard hook
            KeyboardHook = new Hook("Global Keyboard Hook");
            KeyboardHook.KeyUpEvent += KeyUpHook;
            KeyboardHook.KeyDownEvent += KeyDownHook;

            // check autostart task state
            AutoStartDisabled = true;
            RefreshAutostartState();

            App.logger.Debug("MainWindowVM exit");
        }

        #endregion .ctor


        #region private methods
        /// <summary>
        /// Window on load
        /// </summary>
        /// <param name="state"></param>
        private void OnLoaded(object state)
        {
            App.logger.Debug("OnLoaded entry");
            // notifyIcon
            App.logger.Debug("NotifyIcon init");
            NotifyIcon = new NotifyIcon();

            System.IO.Stream iconStream = this.GetType().Assembly.GetManifestResourceStream("MousePointerReposition.Icons.mouse_gray.ico");

            NotifyIcon.Icon = new System.Drawing.Icon(iconStream);
            NotifyIcon.Visible = true;
            NotifyIcon.Click += NotifyIcon_Click;
            App.logger.Debug("NotifyIcon done");


            // use background dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                    {
                        ShowInTaskbar = false;
                    }),
                System.Windows.Threading.DispatcherPriority.Background);

            App.logger.Debug("OnLoaded exit");
        }

        /// <summary>
        /// Window in closing
        /// </summary>
        /// <param name="state"></param>
        private void OnClosing(object state)
        {
            App.logger.Debug("OnClosing");
            NotifyIcon.Visible = false;
        }

        /// <summary>
        /// Exit programm
        /// </summary>
        /// <param name="state"></param>
        private void OnExit(object state)
        {
            App.logger.Debug("OnExit");
            System.Windows.Application.Current.MainWindow.Close();
        }

        /// <summary>
        /// Hide window
        /// </summary>
        /// <param name="state"></param>
        private void OnHide(object state)
        {
            App.logger.Debug("OnHide");
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
        }

        /// <summary>
        /// Retries to move the mouse cursor to the center of a new active application window for a specific amount of time (CHECK_TIMEOUT).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RepositioningTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            CheckPeriod += RepositioningTimer.Interval;

            // check timeout
            // and only restart if repositiong does not occur
            if (CheckPeriod < CHECK_TIMEOUT
                && !TryMouseRepositioning())
            {
                RepositioningTimer.Start();
            }
        }

        /// <summary>
        /// Open main window when notification icon is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            App.logger.Debug("NotifyIcon_Click");
            if (WindowState != WindowState.Minimized)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
            }
            else
            {
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
                System.Windows.Application.Current.MainWindow.Focus();
            }
        }


        /// <summary>
        /// Keyboard key down event handler method
        /// </summary>
        /// <param name="e"></param>
        private void KeyDownHook(KeyboardHookEventArgs e)
        {
            App.logger.Debug("KeyDownHook entry");

            // win + shift + left or right key
            if (!DisableWinLeftRight)
            {
                if (e.Key == Keys.Left
                    || e.Key == Keys.Right && e.isWinPressed) // with or without shift //&& e.isShiftPressed)
                {
                    App.logger.Debug("Win+Left or Win+Right");
                    TriggerMousePositioning();
                }
            }

            // alt + tap
            if (!DisableAltTab)
            {
                if (e.Key == Keys.Tab && e.isLAltPressed)
                {
                    App.logger.Debug("Alt+Tab");
                    TriggerMousePositioning();
                }
            }

            if (!DisableManuelPositioning)
            {
                if (e.isCtrlPressed && e.Key == Keys.None && !e.isRAltPressed)
                {
                    App.logger.Debug("Ctrl pressed");
                    CtrlKeyPressCounter++;

                    if (CtrlKeyPressCounter == 1)
                    {

                        CtrlKeyPressLast = DateTime.Now;
                    }


                    if (CtrlKeyPressCounter >= 2)
                    {
                        App.logger.Debug("2xCtrl");
                        if ((DateTime.Now - CtrlKeyPressLast).TotalMilliseconds <= CTRL_KEY_TIMEOUT)
                        {
                            TriggerManualMousePositioning();
                        }
                        else
                        {
                            CtrlKeyPressLast = DateTime.Now;
                            CtrlKeyPressCounter = 1;
                        }
                    }
                }

                if (e.isCtrlPressed && e.Key != Keys.None && !e.isRAltPressed)
                {
                    CtrlKeyPressCounter = 0;
                    IsManualMousePositioningTriggered = false;
                }
            }

            App.logger.Debug("KeyDownHook exit");
        }

        /// <summary>
        /// Keyboard key up event handler method
        /// </summary>
        /// <param name="e"></param>
        private void KeyUpHook(KeyboardHookEventArgs e)
        {
            App.logger.Debug("KeyUpHook entry");
            // when all keys are released
            if (e.Key == Keys.None && IsMousePositioningTriggered)
            {
                ExecuteMousePositioning();
            }

            if (!DisableManuelPositioning)
            {
                if (e.isCtrlPressed && e.Key == Keys.None && IsManualMousePositioningTriggered)
                {
                    CtrlKeyPressCounter = 0;
                    IsManualMousePositioningTriggered = false;
                    ExecuteMousePositioning();
                }
            }
            App.logger.Debug("KeyUpHook exit");
        }


        /// <summary>
        /// Called when key combination for cursor repositioning is detected.
        /// Stores current foreground window geometry.
        /// </summary>
        private void TriggerMousePositioning()
        {
            App.logger.Debug("TriggerMousePositioning enter");
            RepositioningTimer.Stop(); // stop currently executing repositioning
            IsMousePositioningTriggered = true;

            // save current foreground window rectangle
            foregroundWindowHandleStart = Vanara.PInvoke.User32.GetForegroundWindow();
            foreGroundWindowRectStart = new Vanara.PInvoke.RECT();
            Vanara.PInvoke.User32.GetWindowRect(foregroundWindowHandleStart, out foreGroundWindowRectStart);
            App.logger.Debug("TriggerMousePositioning exit");
        }


        /// <summary>
        /// Called when key combination for manual cursor repositioning is detected.
        /// Stores current foreground window geometry.
        /// </summary>
        private void TriggerManualMousePositioning()
        {
            App.logger.Debug("TriggerManualMousePositioning start");
            RepositioningTimer.Stop(); // stop currently executing repositioning
            IsManualMousePositioningTriggered = true;

            // save empty current foreground window rectangle
            foreGroundWindowRectStart = new Vanara.PInvoke.RECT();
            App.logger.Debug("TriggerManualMousePositioning exit");
        }


        /// <summary>
        /// Start timer for trying cursor repositining to a new foreground application window.
        /// </summary>
        private void ExecuteMousePositioning()
        {
            App.logger.Debug("ExecuteMousePositioning start");
            IsMousePositioningTriggered = false;

            CheckPeriod = 0;
            // start timer that checks active window change (rectangle change)
            RepositioningTimer.Start();
            App.logger.Debug("ExecuteMousePositioning end");
        }


        /// <summary>
        /// Try to move cursor to the center of a new foreground application window.
        /// The active foreground window is compared with the active window before pressing the triggering key combinations.
        /// If the active foreground window has changed the mouse cursor is only repositioned 
        /// if the current cursor location is not already within the foreground window rectangle.
        /// </summary>
        /// <returns></returns>
        private bool TryMouseRepositioning()
        {
            App.logger.Debug("TryMouseRepositioning start");
            // get foreground window
            var foregroundWindowHandle = Vanara.PInvoke.User32.GetForegroundWindow();
            Vanara.PInvoke.User32.GetWindowRect(foregroundWindowHandle, out Vanara.PInvoke.RECT windowRect);

            StringBuilder sbWindowName = new StringBuilder();
            Vanara.PInvoke.User32.GetWindowText(foregroundWindowHandle, sbWindowName, 256);

            StringBuilder sbWindowNameStart = new StringBuilder();
            Vanara.PInvoke.User32.GetWindowText(foregroundWindowHandleStart, sbWindowNameStart, 256);

            App.logger.Debug(String.Format("Old window: name={0} left={1} right={2} top={3} bottom={4}", sbWindowNameStart, foreGroundWindowRectStart.left, foreGroundWindowRectStart.top, foreGroundWindowRectStart.Width, foreGroundWindowRectStart.Height));
            App.logger.Debug(String.Format("New window: name={0} left={1} right={2} top={3} bottom={4}", sbWindowName, windowRect.left, windowRect.right, windowRect.top, windowRect.bottom));

            if (foreGroundWindowRectStart.left != windowRect.left
                || foreGroundWindowRectStart.right != windowRect.right
                || foreGroundWindowRectStart.top != windowRect.top
                || foreGroundWindowRectStart.bottom != windowRect.bottom)
            {
                // check if mouse position is within new active application window
                // get current cursor position
                Vanara.PInvoke.User32.GetCursorPos(out System.Drawing.Point cursorPos);

                App.logger.Debug(String.Format("Current cursor: x={0} y={1}", cursorPos.X, cursorPos.Y));

                if (windowRect.left <= cursorPos.X
                    && windowRect.right >= cursorPos.X
                    && windowRect.top <= cursorPos.Y
                    && windowRect.bottom >= cursorPos.Y)
                {
                    App.logger.Debug("TryMouseRepositioning end (nothing to do: mouse within new window)");
                    return true;
                }
                else
                {
                    int x = windowRect.left + windowRect.Width / 2;
                    int y = windowRect.top + windowRect.Height / 2;

                    App.logger.Debug(String.Format("Set cursor: x={0} y={1}", x, y));
                    
                    Vanara.PInvoke.User32.SetCursorPos(x + 1, y + 1); // sometimes cursor is not positioned right
                    Vanara.PInvoke.User32.SetCursorPos(x - 1, y - 1); // sometimes cursor is not positioned right
                    Vanara.PInvoke.User32.SetCursorPos(x, y); // calling twice sets the correct postion

                    Vanara.PInvoke.User32.GetCursorPos(out cursorPos);
                    App.logger.Debug(String.Format("New Cursor: x={0} y={1}", cursorPos.X, cursorPos.Y));

                    App.logger.Debug("TryMouseRepositioning end");
                    return true;
                }
            }
            else
            {
                App.logger.Debug("TryMouseRepositioning end (nothing to do: same window position)");
                return false;
            }
                

            
        }


#if _UWP
        /// <summary>
        /// Get windows universal app startup task.
        /// </summary>
        /// <returns></returns>
        private Windows.ApplicationModel.StartupTask GetStartupTask()
        {
            var task = Windows.ApplicationModel.StartupTask.GetAsync("MousePointerRepositionStartupTask").AsTask();
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Get windows universal app startup task.
        /// </summary>
        private void GetStartupTaskAndContinue(Action<Task<Windows.ApplicationModel.StartupTask>> action)
        {
            var task = Windows.ApplicationModel.StartupTask.GetAsync("MousePointerRepositionStartupTask").AsTask();
            task.ContinueWith(action);
        }
    
        /// <summary>
        /// Get start task state and set AutostartDisabled and Autostart property values;
        /// </summary>
        private void RefreshAutostartState()
        {
            GetStartupTaskAndContinue((t) =>
            {
                SetAutostartState(t.Result.State);
            });
        }

        /// <summary>
        /// Set AutostartDisabled and Autostart property view startup task state (universal app startup)
        /// </summary>
        /// <param name="startupTask"></param>
        private void SetAutostartState(Windows.ApplicationModel.StartupTaskState startupTaskState)
        {
            AutoStartDisabled = startupTaskState == Windows.ApplicationModel.StartupTaskState.DisabledByUser ||
                    startupTaskState == Windows.ApplicationModel.StartupTaskState.DisabledByPolicy ||
                    startupTaskState == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;

            autostart = startupTaskState == Windows.ApplicationModel.StartupTaskState.Enabled ||
                startupTaskState == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            OnPropertyChanged(nameof(Autostart));
        }
#else
        /// <summary>
        /// Get start task state and set AutostartDisabled and Autostart property values;
        /// </summary>
        private void RefreshAutostartState()
        {
            AutoStartDisabled = false;

            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            string appPath = rkApp.GetValue("MousePointerReposition")?.ToString();
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (appPath == null || !assemblyPath.Equals(appPath))
            {
                autostart = false;
            }
            else
            {
                autostart = true;
            }
            OnPropertyChanged(nameof(Autostart));
        }
#endif

        #endregion private methods


        #region INotifyPropertyChanged
        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }
        #endregion INotifyPropertyChanged
    }

}
