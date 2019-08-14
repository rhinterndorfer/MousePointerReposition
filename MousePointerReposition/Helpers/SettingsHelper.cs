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
        #region instance
        private static SettingsHelper _instance;

        /// <summary>
        /// Get single instance
        /// </summary>
        public static SettingsHelper Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SettingsHelper();
                return _instance;
            }
        }
        #endregion instance


        #region public properties
        
        /// <summary>
        /// Disable ALT+TAB key combination
        /// </summary>
        public bool DisableAltTab
        {
            get { return ReadRegValue<int>(nameof(DisableAltTab)) == 1; }
            set { WriteRegValue<int>(nameof(DisableAltTab), value ? 1 : 0); }
        }

        /// <summary>
        /// Disable WIN+LEFT or RIGHT key combination
        /// </summary>
        public bool DisableWinLeftRight
        {
            get { return ReadRegValue<int>(nameof(DisableWinLeftRight)) == 1; }
            set { WriteRegValue<int>(nameof(DisableWinLeftRight), value ? 1 : 0); }
        }

        /// <summary>
        /// Disable manual cursor reposition (2x CTRL key)
        /// </summary>
        public bool DisableManualCursorReposition
        {
            get { return ReadRegValue<int>(nameof(DisableManualCursorReposition)) == 1; }
            set { WriteRegValue<int>(nameof(DisableManualCursorReposition), value ? 1 : 0); }
        }
        #endregion public properties


        #region helpers
        /// <summary>
        /// Read value from registry
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Read value from registry
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        private void WriteRegValue<T>(string key, T value)
        {
            Registry.CurrentUser.CreateSubKey("Software\\rammelhof.at\\MousePointerReposition").SetValue(key, value);
        }
        #endregion helpers


        #region autostart
        /// <summary>
        /// Create or remove autostart entry
        /// </summary>
        /// <param name="autostart"></param>
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

        /// <summary>
        /// Exists autostart entry
        /// </summary>
        /// <returns></returns>
        private bool GetAutostart()
        {
            var value = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run").GetValue("MousePointerReposition", null);
            return value != null;
        }
        #endregion autostart
    }
}
