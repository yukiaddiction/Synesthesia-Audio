using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Synesthesia_Audio.Services.Storage;

#if WINDOWS
/// <summary>
/// Minimal DPAPI wrapper over crypt32.dll. MAUI's SecureStorage cannot be used for this: desktop
/// builds here are unpackaged (WindowsPackageType=None), so its ApplicationData.Current container
/// has no package identity. DPAPI itself needs no identity, and a file container does not either.
/// </summary>
internal static class WindowsDpapi
{
    private const int CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(byte[] data)
    {
        var input = new DataBlob(data);
        var output = new DataBlob();
        try
        {
            if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, ref output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptProtectData failed");
            return output.ToArray();
        }
        finally
        {
            input.Free();
            output.Free();
        }
    }

    public static byte[] Unprotect(byte[] data)
    {
        var input = new DataBlob(data);
        var output = new DataBlob();
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, ref output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUnprotectData failed");
            return output.ToArray();
        }
        finally
        {
            input.Free();
            output.Free();
        }
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref DataBlob pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int CbData;
        public IntPtr PbData;

        public DataBlob(byte[] data)
        {
            CbData = data.Length;
            PbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, PbData, data.Length);
        }

        public byte[] ToArray()
        {
            if (PbData == IntPtr.Zero || CbData <= 0) return Array.Empty<byte>();
            var result = new byte[CbData];
            Marshal.Copy(PbData, result, 0, CbData);
            return result;
        }

        public void Free()
        {
            if (PbData == IntPtr.Zero) return;
            LocalFree(PbData);
            PbData = IntPtr.Zero;
            CbData = 0;
        }
    }
}
#endif
