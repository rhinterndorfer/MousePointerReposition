using FMUtils.KeyboardHook;
using MousePointerReposition.Helper;
using MousePointerReposition.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace MousePointerReposition
{
    public class MainWindowVM : INotifyPropertyChanged
    {
        private const double CHECK_TIMEOUT = 5000; // ms
        private const long CTRL_KEY_TIMEOUT = 1000; // ms


        private Vanara.PInvoke.RECT foreGroundWindowRectStart;
        private WindowState windowState;
        private bool showInTaskbar;
        private RelayCommand loaded;
        private bool? autostart;
        private bool? disableAltTab;
        private bool? disableWinLeftRight;
        private bool? disableManualReposition;

        public event PropertyChangedEventHandler PropertyChanged;


        private NotifyIcon NotifyIcon { get; set; }
        private Hook KeyboardHook { get; set; }
        private double CheckPeriod { get; set; }
        private bool IsMousePositioningTriggered { get; set; }
        private bool IsManualMousePositioningTriggered { get; set; }
        private int CtrlKeyPressCounter { get; set; }
        private DateTime CtrlKeyPressLast { get; set; }
        private System.Timers.Timer RepositioningTimer { get; set; }


        public RelayCommand Loaded
        {
            get
            {
                if (loaded == null)
                    loaded = new RelayCommand(OnLoaded);
                return loaded;
            }
        }


        public WindowState WindowState
        {
            get => windowState;
            set
            {
                windowState = value;
                OnPropertyChanged(nameof(WindowState));
            }
        }

        public bool ShowInTaskbar
        {
            get => showInTaskbar;
            set
            {
                showInTaskbar = value;
                OnPropertyChanged(nameof(ShowInTaskbar));
            }
        }



        public bool Autostart
        {
            get
            {
                if (!autostart.HasValue)
                    autostart = SettingsHelper.Instance.Autostart;
                return autostart ?? false;
            }
            set
            {
                autostart = value;
                SettingsHelper.Instance.Autostart = value;
                OnPropertyChanged(nameof(Autostart));
            }
        }

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



        /// <summary>
        /// .ctor
        /// </summary>
        public MainWindowVM()
        {
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
        }


        private void OnLoaded(object state)
        {
            // notifyIcon
            NotifyIcon = new NotifyIcon();

            System.IO.Stream iconStream = this.GetType().Assembly.GetManifestResourceStream("MousePointerReposition.Icons.mouse_gray.ico");

            NotifyIcon.Icon = new System.Drawing.Icon(iconStream);
            NotifyIcon.Visible = true;
            NotifyIcon.Click += NotifyIcon_Click;

            // use background dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                    {
                        ShowInTaskbar = false;
                    }), 
                System.Windows.Threading.DispatcherPriority.Background);
        }

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


        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Minimized)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
            }
            else
            {
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
            }
        }


        private void KeyDownHook(KeyboardHookEventArgs e)
        {
            // win + shift + left or right key
            if (!DisableWinLeftRight)
            {
                if (e.Key == Keys.Left
                    || e.Key == Keys.Right && e.isWinPressed && e.isShiftPressed)
                {
                    TriggerMousePositioning();
                }
            }

            if (!DisableManuelPositioning)
            {
                if (e.isCtrlPressed && e.Key == Keys.None)
                {
                    CtrlKeyPressCounter++;
                    if (CtrlKeyPressCounter == 1)
                    {
                        CtrlKeyPressLast = DateTime.Now;
                    }


                    if (CtrlKeyPressCounter >= 2)
                    {
                        if ((DateTime.Now - CtrlKeyPressLast).TotalMilliseconds <= CTRL_KEY_TIMEOUT)
                        {
                            IsManualMousePositioningTriggered = true;
                        }
                        else
                        {
                            CtrlKeyPressLast = DateTime.Now;
                            CtrlKeyPressCounter = 1;
                        }
                    }
                }

                if (e.isCtrlPressed && e.Key != Keys.None)
                {
                    CtrlKeyPressCounter = 0;
                    IsManualMousePositioningTriggered = false;
                }
            }
        }


        private void KeyUpHook(KeyboardHookEventArgs e)
        {
            // alt + tap
            if (!DisableAltTab)
            {
                if (e.Key == Keys.Tab && e.isAltPressed)
                {
                    TriggerMousePositioning();
                }
            }

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
        }

        private void TriggerMousePositioning()
        {
            IsMousePositioningTriggered = true;

            // save current foreground window rectangle
            var foregroundWindowHandleStart = Vanara.PInvoke.User32_Gdi.GetForegroundWindow();
            foreGroundWindowRectStart = new Vanara.PInvoke.RECT();
            Vanara.PInvoke.User32_Gdi.GetWindowRect(foregroundWindowHandleStart, out foreGroundWindowRectStart);
        }

        private void ExecuteMousePositioning()
        {
            IsMousePositioningTriggered = false;

            CheckPeriod = 0;
            // start timer that checks active window change (rectangle change)
            RepositioningTimer.Start();
        }

        private bool TryMouseRepositioning()
        {
            // get foreground window
            var foregroundWindowHandle = Vanara.PInvoke.User32_Gdi.GetForegroundWindow();
            Vanara.PInvoke.RECT windowRect;
            Vanara.PInvoke.User32_Gdi.GetWindowRect(foregroundWindowHandle, out windowRect);

            if (foreGroundWindowRectStart.left != windowRect.left
                || foreGroundWindowRectStart.Width != windowRect.Width
                || foreGroundWindowRectStart.top != windowRect.top
                || foreGroundWindowRectStart.Height != windowRect.Height)
            {
                // check if mouse position is within new active application window
                // get current cursor position
                System.Drawing.Point cursorPos;
                Vanara.PInvoke.User32_Gdi.GetCursorPos(out cursorPos);

                if (windowRect.left <= cursorPos.X
                    && windowRect.right >= cursorPos.X
                    && windowRect.top <= cursorPos.Y
                    && windowRect.bottom >= cursorPos.Y)
                {
                    return true;
                }
                else
                {

                    int x = windowRect.left + windowRect.Width / 2;
                    int y = windowRect.top + windowRect.Height / 2;

                    Vanara.PInvoke.User32_Gdi.SetCursorPos(x + 1, y + 1);
                    Vanara.PInvoke.User32_Gdi.SetCursorPos(x, y);

                    return true;
                }
            }
            else
                return false;
        }

        internal void Window_Closed(object sender, EventArgs e)
        {
            NotifyIcon.Visible = false;
        }

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

    }

}
