namespace ES.Anathema.Core

open System
open System.Runtime.InteropServices

module Native =
    [<DllImport("Clrjit.dll", CallingConvention = CallingConvention.StdCall, PreserveSig = true)>]
    extern IntPtr getJit()

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern Boolean VirtualProtect(IntPtr lpAddress, UInt32 dwSize, Protection flNewProtect, UInt32& lpflOldProtect)

    [<UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)>]    
    type CompileMethodDeclaration = delegate of IntPtr * IntPtr * nativeptr<CorMethodInfo> * CorJitFlag * IntPtr * IntPtr -> Int32

