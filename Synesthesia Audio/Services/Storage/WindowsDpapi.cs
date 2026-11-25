using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

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

    /// <summary>
    /// App-specific entropy (SEC-m-03). Without it any process running as this user could decrypt these blobs,
    /// because DPAPI's default scope is "the same user". With it, only code that knows this value can — so a
    /// stolen container file is not enough on its own.
    /// </summary>
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
            // A blob written before entropy was introduced cannot be decrypted with it. Falling back keeps the
            // user's existing credential instead of making them retype it; the next save stores it protected
            // (SEC-m-03). This is a migration path, not the normal one.
            return UnprotectCore(data, useEntropy: false);
        }
    }

    private static byte[] UnprotectCore(byte[] data, bool useEntropy)
    {
        var input = DataBlob.FromBytes(data);
        // A default blob has a null pointer, which is exactly the "no optional entropy" argument.
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

    /// <summary>
    /// A DATA_BLOB with ownership that matches its allocation: input buffers are allocated with
    /// <see cref="Marshal.AllocHGlobal(Int32)"/> and must be released with <see cref="Marshal.FreeHGlobal"/>,
    /// while the output buffer is allocated by crypt32 with LocalAlloc and must be released with LocalFree.
    /// The old code used LocalFree for both, which is the wrong pairing for the input (SEC-m-03).
    /// </summary>
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

        /// <summary>An output blob, whose buffer crypt32 will allocate.</summary>
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
