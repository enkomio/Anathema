namespace ES.Anathema.Hook

open System
open System.Reflection
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open ES.Anathema.Core.Headers
open ES.Anathema.Core.Native

type Hook = delegate of CompileMethodDeclaration * IntPtr * IntPtr * nativeptr<CorMethodInfo> * CorJitFlag * IntPtr * IntPtr -> Int32

type JitHook() as this =     
    let mutable _hookedCompileMethod: Hook option = None
    let mutable _realCompileMethod : CompileMethodDeclaration option = None
    let mutable _pCompileMethod = new IntPtr()

    let hookedCompileMethod (thisPtr: IntPtr) (corJitInfo: IntPtr) (methodInfoPtr: nativeptr<CorMethodInfo>) (flags: CorJitFlag) (nativeEntry: IntPtr) (nativeSizeOfCode: IntPtr) =        
        match _hookedCompileMethod with
        | Some hook -> hook.Invoke(_realCompileMethod.Value, thisPtr, corJitInfo, methodInfoPtr, flags, nativeEntry, nativeSizeOfCode)
        | None -> _realCompileMethod.Value.Invoke(thisPtr, corJitInfo, methodInfoPtr, flags, nativeEntry, nativeSizeOfCode)

    let _hookedCompileMethodDelegate = new CompileMethodDeclaration(hookedCompileMethod)

    do
        let pVTable = getJit()
        _pCompileMethod <- Marshal.ReadIntPtr(pVTable)    
        // compile methods, this is important in order to avoid infinite loop      
        ["InstallHook"; "Stop"; "hookedCompileMethod"]
        |> List.iter(fun methodName ->
            let m = this.GetType().GetMethod(methodName,  BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.NonPublic)
            RuntimeHelpers.PrepareMethod(m.MethodHandle)
        )        

    member this.Start() =
        if _realCompileMethod.IsNone then
            // make memory writable
            let mutable oldProtection = uint32 0
            if not <| VirtualProtect(_pCompileMethod, uint32 IntPtr.Size, Protection.PAGE_EXECUTE_READWRITE, &oldProtection) then
                Environment.Exit(-1)
            let protection = Enum.Parse(typeof<Protection>, oldProtection.ToString()) :?> Protection
            
            // save original compile method
            _realCompileMethod <- Some (Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(_pCompileMethod), typeof<CompileMethodDeclaration>) :?> CompileMethodDeclaration)
            RuntimeHelpers.PrepareDelegate(_realCompileMethod.Value)
            RuntimeHelpers.PrepareDelegate(_hookedCompileMethodDelegate)

            // install compileMethod hook        
            Marshal.WriteIntPtr(_pCompileMethod, Marshal.GetFunctionPointerForDelegate(_hookedCompileMethodDelegate))
        
            // repristinate memory protection flags        
            VirtualProtect(_pCompileMethod, uint32 IntPtr.Size, protection, &oldProtection) |> ignore

    member this.InstallHook(hook: Hook) =
        RuntimeHelpers.PrepareDelegate(hook)
        _hookedCompileMethod <- Some hook
        
    member this.Stop() =
        if _realCompileMethod.IsSome then
            // make memory writable
            let mutable oldProtection = uint32 0
            if not <| VirtualProtect(_pCompileMethod, uint32 IntPtr.Size, Protection.PAGE_EXECUTE_READWRITE, &oldProtection) then
                Environment.Exit(-1)
            let protection = Enum.Parse(typeof<Protection>, oldProtection.ToString()) :?> Protection

            // write back original method
            Marshal.WriteIntPtr(_pCompileMethod, Marshal.GetFunctionPointerForDelegate(_realCompileMethod.Value))

            // repristinate memory protection flags        
            VirtualProtect(_pCompileMethod, uint32 IntPtr.Size, protection, &oldProtection) |> ignore
            _realCompileMethod <- None
