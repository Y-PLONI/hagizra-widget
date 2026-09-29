using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using HagizraWidget.Core;

namespace HagizraWidgetProvider
{
    class Program
    {
        [DllImport("ole32.dll")]
        static extern int CoRegisterClassObject(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            uint dwClsContext,
            uint flags,
            out uint lpdwRegister);

        [DllImport("ole32.dll")]
        static extern int CoRevokeClassObject(uint dwRegister);

        [DllImport("ole32.dll")]
        static extern int CoResumeClassObjects();

        const uint CLSCTX_LOCAL_SERVER = 0x4;
        const uint REGCLS_MULTIPLEUSE = 0x1;
        const uint REGCLS_SUSPENDED = 0x4;
        const uint S_OK = 0;

        static uint _cookie = 0;

        /// <summary>Set when the last widget is deleted; Windows relaunches the COM server when a widget is added again.</summary>
        internal static readonly ManualResetEvent ExitEvent = new(false);

        /// <summary>
        /// One executable, three roles. The manifest starts the COM server with "--com-server"; "--update [feed]" runs one
        /// update check and exits (0 updated, 10 already up to date, 1 failed); the startup task starts it with no
        /// arguments, which runs the notification poller (and exits at once when notifications are off).
        /// </summary>
        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--update")
                return RunUpdate(args.Length >= 2 ? args[1] : null);

            bool comServer = args.Any(a => a == "--com-server" || a == "-Embedding" || a.StartsWith("-ServerName", StringComparison.OrdinalIgnoreCase));
            if (comServer)
            {
                RunServer();
                return 0;
            }
            return Notifier.Run();
        }

        static int RunUpdate(string? feed) =>
            SelfUpdater.RunAsync(feed).GetAwaiter().GetResult() switch
            {
                SelfUpdater.Result.Updated => 0,
                SelfUpdater.Result.UpToDate => 10,
                _ => 1,
            };

        static void RunServer()
        {
            try
            {
                var widgetProviderFactory = new WidgetProviderFactory();
                Guid clsid = new Guid(WidgetProvider.WidgetProviderClassId);

                int hr = CoRegisterClassObject(
                    clsid,
                    widgetProviderFactory,
                    CLSCTX_LOCAL_SERVER,
                    REGCLS_MULTIPLEUSE | REGCLS_SUSPENDED,
                    out _cookie);

                if (hr == S_OK)
                {
                    CoResumeClassObjects();
                    ExitEvent.WaitOne();
                    CoRevokeClassObject(_cookie);
                }
                else
                {
                    Log.Info("Program", $"CoRegisterClassObject failed with HR: {hr}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Program", ex);
            }
        }
    }
}
