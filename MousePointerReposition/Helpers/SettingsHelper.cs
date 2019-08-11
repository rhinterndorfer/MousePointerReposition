using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MousePointerReposition.Helper
{
    public class SettingsHelper
    {
        private static SettingsHelper _instance;
        public static SettingsHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SettingsHelper();
                return _instance;
            }
        }


        public bool DisableAltTab
        {
            get { return ReadRegValue<int>(nameof(DisableAltTab)) == 1; }
            set { WriteRegValue<int>(nameof(DisableAltTab), value ? 1 : 0); }
        }

        public bool DisableWinLeftRight
        {
            get { return ReadRegValue<int>(nameof(DisableWinLeftRight)) == 1; }
            set { WriteRegValue<int>(nameof(DisableWinLeftRight), value ? 1 : 0); }
        }

        public bool DisableManualCursorReposition
        {
            get { return ReadRegValue<int>(nameof(DisableManualCursorReposition)) == 1; }
            set { WriteRegValue<int>(nameof(DisableManualCursorReposition), value ? 1 : 0); }
        }

        public bool Autostart
        {
            get { return GetAutostart(); }
            set { SetAutostart(value); }
        }

        private T ReadRegValue<T>(string key)
        {
            try
            {
                return (T)Registry.CurrentUser.CreateSubKey("Software\\rammelhof.at\\MousePointerReposition").GetValue(key, false);
            }
            catch
            {
                // and set default
                WriteRegValue<T>(key, default(T));
                return default(T);
            }
        }

        private void WriteRegValue<T>(string key, T value)
        {
            Registry.CurrentUser.CreateSubKey("Software\\rammelhof.at\\MousePointerReposition").SetValue(key, value);
        }

        // Computer\HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run

        private void SetAutostart(bool autostart)
        {
            if (autostart)
            {
                string path = Assembly.GetExecutingAssembly().Location;
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run").SetValue("MousePointerReposition", String.Format("\"{0}\"", path));
            }
            else
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run").DeleteValue("MousePointerReposition");
        }

        private bool GetAutostart()
        {
            var value = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run").GetValue("MousePointerReposition", null);
            return value != null;
        }


    }
}
