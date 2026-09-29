using System;
using System.Runtime.InteropServices;

namespace HagizraWidgetProvider
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("5C0D8E3A-7B21-4F6A-9E5D-2A8B1C4D6F90")]
    public class WidgetProviderFactory : IClassFactory
    {
        public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;

            if (pUnkOuter != IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
            }

            if (riid == typeof(Microsoft.Windows.Widgets.Providers.IWidgetProvider).GUID || riid == Guid.Parse("00000000-0000-0000-C000-000000000046"))
            {
                // A CsWinRT CCW answers QueryInterface for every projected interface, including IWidgetProvider2.
                ppvObject = WinRT.MarshalInspectable<Microsoft.Windows.Widgets.Providers.IWidgetProvider>.FromManaged(new WidgetProvider());
            }

            return ppvObject != IntPtr.Zero ? 0 : unchecked((int)0x80004002);
        }

        public int LockServer(bool fLock)
        {
            return 0;
        }

        private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    }

    [ComImport]
    [ComVisible(false)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000001-0000-0000-C000-000000000046")]
    internal interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

        [PreserveSig]
        int LockServer(bool fLock);
    }
}
