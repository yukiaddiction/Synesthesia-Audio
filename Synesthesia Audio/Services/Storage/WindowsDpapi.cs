using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Synesthesia_Audio.Services.Storage;

#if WINDOWS
/// <summary>
/// Minimal crypt32 DPAPI wrapper. Unpackaged desktop builds have no package identity for MAUI SecureStorage,
/// but DPAPI and a plain file need none.
/// </summary>
internal static class WindowsDpapi
{
    private const int CryptProtectUiForbidden = 0x1;

    // DPAPI's default scope is "same user", so without app-specific entropy any process of this user could decrypt
    // the blobs; with it a stolen container file is not enough on its own.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SynesthesiaAudio.SecureStore.v1");

    public static byte[] Protect(byte[] data)
    {
        var input = DataBlob.FromBytes(data);
        var entropy = DataBlob.FromBytes(Entropy);
        var output = DataBlob.ForOutput();
        try
        {
            if (!CryptProtectData(ref input, null, entropy.PbData, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, ref output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptProtectData failed");
            return output.ToArray();
        }
        finally
        {
            input.Free();
            entropy.Free();
            output.Free();
        }
    }

    public static byte[] Unprotect(byte[] data)
    {
        try
        {
            return UnprotectCore(data, useEntropy: true);
        }
        catch (Win32Exception)
        {
            // Migration path: blobs written before entropy was added; the next save re-protects them with it.
            return UnprotectCore(data, useEntropy: false);
        }
    }

    private static byte[] UnprotectCore(byte[] data, bool useEntropy)
    {
        var input = DataBlob.FromBytes(data);
        // A default blob's null pointer is exactly the "no optional entropy" argument.
        var entropy = useEntropy ? DataBlob.FromBytes(Entropy) : default;
        var output = DataBlob.ForOutput();
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, entropy.PbData, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, ref output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CryptUnprotectData failed");
            return output.ToArray();
        }
        finally
        {
            input.Free();
            entropy.Free();
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

    // Frees with the allocator that made the buffer: input via AllocHGlobal/FreeHGlobal, crypt32's output via
    // LocalAlloc/LocalFree.
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int CbData;
        public IntPtr PbData;

        private bool _ownedByOperatingSystem;

        public static DataBlob FromBytes(byte[] data)
        {
            var blob = default(DataBlob);
            blob._ownedByOperatingSystem = false;
            blob.CbData = data.Length;
            blob.PbData = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, blob.PbData, data.Length);
            return blob;
        }

        public static DataBlob ForOutput()
        {
            var blob = default(DataBlob);
            blob._ownedByOperatingSystem = true;
            return blob;
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

            if (_ownedByOperatingSystem) LocalFree(PbData);
            else Marshal.FreeHGlobal(PbData);

            PbData = IntPtr.Zero;
            CbData = 0;
        }
    }
}
#endif
