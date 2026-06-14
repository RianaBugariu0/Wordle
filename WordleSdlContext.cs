using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;

namespace Wordle;

public class WordleSdlContext : INativeContext
{
    private readonly IntPtr _nativeLibrary;

    public WordleSdlContext()
    {
        _nativeLibrary = LoadLibrary();
    }

    public IntPtr GetProcAddress(string proc, int? slot = null)
    {
        return NativeLibrary.GetExport(_nativeLibrary, proc);
    }

    public bool TryGetProcAddress(string proc, [UnscopedRef] out IntPtr addr, int? slot = null)
    {
        try
        {
            addr = NativeLibrary.GetExport(_nativeLibrary, proc);
        }
        catch (EntryPointNotFoundException)
        {
            addr = IntPtr.Zero;
        }

        return addr != IntPtr.Zero;
    }

    private static IntPtr LoadLibrary()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return NativeLibrary.Load("SDL2.dll");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return NativeLibrary.Load("libSDL2-2.0.dylib");
        }

        return NativeLibrary.Load("libSDL2-2.0.so");
    }

    private void ReleaseUnmanagedResources()
    {
        NativeLibrary.Free(_nativeLibrary);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~WordleSdlContext()
    {
        ReleaseUnmanagedResources();
    }
}

