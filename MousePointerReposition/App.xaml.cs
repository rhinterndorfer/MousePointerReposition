using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;

namespace MousePointerReposition
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Logger logger;

        public App()
        {
            logger = new LoggerConfiguration()
                  .WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) ,"MousePointerReposition.log.txt"))
                  .CreateLogger();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            logger.Error("Application_DispatcherUnhandledException", e.Exception);
        }
    }
}
