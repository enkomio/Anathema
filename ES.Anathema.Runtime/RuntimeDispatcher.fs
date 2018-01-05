namespace ES.Anathema.Runtime
#nowarn "9"

open System
open System.IO
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Reflection
open System.Reflection.Emit
open System.Diagnostics
open System.Text
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Microsoft.FSharp.NativeInterop
open ES.Anathema.Core.Headers
open ES.Anathema.Core.Native

type internal TrampolineMethodInfo = {
    Builder: MethodBuilder
    MethodName: String
    PatchOffset: Int32
}

module Dispatcher =
    let dispatchCallback(assemblyLocation: String, argv: Object array) =
        if File.Exists(assemblyLocation) then         
            let callingMethod =
                try
                    // retrieve the calling method
                    let stackTrace = new StackTrace()
                    let frames = stackTrace.GetFrames()
                    frames.[2].GetMethod()
                with _ -> null

            // invoke all the monitors
            let bytes = File.ReadAllBytes(assemblyLocation)
            for t in Assembly.Load(bytes).GetTypes() do
                try
                    if t.Name.EndsWith("Monitor") && not t.IsAbstract then
                        let monitorConstructor = t.GetConstructor([|typeof<MethodBase>; typeof<Object array>|])                
                        if monitorConstructor <> null then
                            monitorConstructor.Invoke([|callingMethod; argv|]) |> ignore
                with _ -> ()

type RuntimeDispatcher() as this =     
    let _dynamicAssembly  = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.RunAndCollect)
    let _dynamicModule = _dynamicAssembly.DefineDynamicModule("dispatcherMethods", "DispatcherMethods.dll")    
    let _inCompileMethod = ref 0
    let _index = ref 0
    let _filters = new List<MethodFilter>()
    let _signatures = new Dictionary<String, MethodInfo>()

    let generateTypeBuilder() =
        let dynamicModule = _dynamicAssembly.DefineDynamicModule("MODULE" + string(!_index) + _dynamicAssembly.FullName)
        incr _index
        dynamicModule.DefineType("TYPE" +  Guid.NewGuid().ToString())
            
    let createMethodSignature(methodBase: MethodBase) =
        let parameters = String.Join(",", methodBase.GetParameters() |> Array.map(fun p -> p.ParameterType.GetHashCode().ToString()))
        let instanceType = (if methodBase.IsStatic then typeof<Void> else typeof<Object>).GetHashCode().ToString()
        
        let returnType =
            (match methodBase with
            | :? MethodInfo as mi -> mi.ReturnType
            | :? ConstructorInfo -> typeof<Object>
            | _ -> typeof<Void>).GetHashCode().ToString()
        returnType + instanceType + parameters

    let tryResolveDispatcherMethod(methodBase: MethodBase) =
        let signature = createMethodSignature(methodBase)
        if _signatures.ContainsKey(signature) then
            Some(_signatures.[signature])
        else None            
              
    /// This function will create a dynamic method that is called by the trampoline. Its purpose is lo invoke the dispatchCallback function
    let createDispatcherMethod(filteredMethod: FilteredMethod) =
        match tryResolveDispatcherMethod(filteredMethod.Method) with
        | Some mi -> mi
        | None ->
            let dynamicMethodName = filteredMethod.Method.Name + "_Dispatcher" + string(!_index)
            let argumentTypes = [|
                if not filteredMethod.Method.IsStatic then
                    yield typeof<Object>
                yield! filteredMethod.Method.GetParameters() |> Array.map(fun p -> p.ParameterType)
            |]
            
            let dynamicType = _dynamicModule.DefineType(filteredMethod.Method.Name + "_Type" + string(!_index))        
            let dynamicMethod = 
                dynamicType.DefineMethod(
                    dynamicMethodName, 
                    MethodAttributes.Static ||| MethodAttributes.HideBySig ||| MethodAttributes.Public,
                    CallingConventions.Standard,
                    typeof<System.Void>, 
                    argumentTypes
                )
            incr _index

            // generate method IL
            let ilGenerator = dynamicMethod.GetILGenerator()        
            ilGenerator.Emit(OpCodes.Nop)
        
            // push the location of the assembly to load containing the monitors
            let assemblyLocation =
                if filteredMethod.Filter.Invoker <> null then filteredMethod.Filter.Invoker.Assembly.Location
                else String.Empty
            ilGenerator.Emit(OpCodes.Ldstr, assemblyLocation)            

            // create argv array
            ilGenerator.Emit(OpCodes.Ldc_I4, filteredMethod.NumOfArgumentsToPushInTheStack)
            ilGenerator.Emit(OpCodes.Newarr, typeof<Object>)
                    
            // fill the argv array
            let parameters = filteredMethod.Method.GetParameters() |> Seq.map(fun pi -> pi.ParameterType) |> Seq.toList
            for i=0 to filteredMethod.NumOfArgumentsToPushInTheStack-1 do
                ilGenerator.Emit(OpCodes.Dup)
                ilGenerator.Emit(OpCodes.Ldc_I4, i)
                ilGenerator.Emit(OpCodes.Ldarg, i)
            
                // chyeck if I have to box the value
                if filteredMethod.Method.IsStatic  || i > 0 then
                    let paramIndex = if filteredMethod.Method.IsStatic then i else i - 1
                    if parameters.[paramIndex].IsEnum then             
                        // consider all enum as Int32 type to avoid access problems     
                        ilGenerator.Emit(OpCodes.Box, typeof<Int32>)     

                    elif parameters.[paramIndex].IsValueType then                    
                        ilGenerator.Emit(OpCodes.Box, parameters.[paramIndex])   
                    
                // store the element in the array
                ilGenerator.Emit(OpCodes.Stelem_Ref)
            
            // emit call to dispatchCallback
            let dispatchCallbackMethod = Type.GetType("ES.Anathema.Runtime.Dispatcher").GetMethod("dispatchCallback", BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            ilGenerator.EmitCall(OpCodes.Call, dispatchCallbackMethod, null)
                        
            ilGenerator.Emit(OpCodes.Ret)

            let createdType = dynamicType.CreateType()
            let createdMethod = createdType.GetMethod(dynamicMethodName, BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            
            let signature = createMethodSignature(filteredMethod.Method)
            _signatures.Add(signature, createdMethod)
            
            // compile the method
            RuntimeHelpers.PrepareMethod(createdMethod.MethodHandle)
            GC.KeepAlive(createdMethod)

            createdMethod

    /// this method is in charge for the creation of the MSIL code to be added to the MSIL that will be compiled. 
    /// It is in charge to call the dynamic method wich in turn will dispatch the call to the method monitors
    let generateTrampolineMethod(filteredMethod: FilteredMethod, typeBuilder: TypeBuilder, dispatcherMethod: MethodInfo) =
        let dispatcherArgs = [|for i in [0..filteredMethod.NumOfArgumentsToPushInTheStack-1] -> typeof<Object>|]
        let functionAddress = dispatcherMethod.MethodHandle.GetFunctionPointer().ToInt32()

        // retrieve the necessary object to create the new IL        
        let methodBuilder = typeBuilder.DefineMethod("CONTAINER_" + Guid.NewGuid().ToString(), MethodAttributes.Static, CallingConventions.Standard, typeof<System.Void>, dispatcherArgs)
        let ilGenerator = methodBuilder.GetILGenerator()
                
        // create method body 
        ilGenerator.Emit(OpCodes.Nop)

        // load all arguments in the stack    
        for i=0 to filteredMethod.NumOfArgumentsToPushInTheStack-1 do                                
            ilGenerator.Emit(OpCodes.Ldarg, i) 
                                          
        // emit calli instruction with a pointer to the hook method. The token used by the calli is not important as I'll modify it soon                   
        ilGenerator.Emit(OpCodes.Ldc_I4, functionAddress)                
        ilGenerator.EmitCalli(OpCodes.Calli, CallingConvention.StdCall, dispatcherMethod.ReturnType, dispatcherArgs)   
                        
        // this index allow to modify the right byte
        let patchOffset = ilGenerator.ILOffset - 4
        ilGenerator.Emit(OpCodes.Nop)
        
        match filteredMethod.Method with
        | :? MethodInfo as mi -> 
            if mi.ReturnType <> typeof<System.Void> then 
                ilGenerator.Emit(OpCodes.Pop)
        | _ -> ()
                                
        // end method
        ilGenerator.Emit(OpCodes.Ret)

        // return a TrampolineMethodInfo
        { 
            Builder = methodBuilder
            MethodName = methodBuilder.Name
            PatchOffset = patchOffset 
        }

    let patchMethodBody(methodBody: MethodBody, filteredMethod: FilteredMethod, patchOffset: Int32) =
        // modify calli token
        let trampolineMsil = methodBody.GetILAsByteArray()            
                
        // craft MethodDef metadata token index
        let b1 = (filteredMethod.TokenNum &&& int16 0xFF00) >>> 8
        let b2 = filteredMethod.TokenNum &&& int16 0xFF

        // calli instruction accept 0x11 as table index (StandAloneSig), 
        // but seems that also other tables are allowed.
        // In particular the following ones seem to be accepted as 
        // valid: TypeSpec, Field and Method (most important)
        trampolineMsil.[patchOffset] <- byte b2
        trampolineMsil.[patchOffset+1] <- byte b1
        trampolineMsil.[patchOffset + 3] <- 6uy // 6(0x6): MethodDef Table
        trampolineMsil    
        
    let mergeCode(methodInfo: CorMethodInfo, trampolineMsil: Byte array) =
        // get the original msil bytes
        let origMsil = Array.zeroCreate<Byte>(int32(methodInfo.IlCodeSize))
        Marshal.Copy(methodInfo.IlCode, origMsil, 0, origMsil.Length)       
            
        // compose final IL                
        let newLen = origMsil.Length + trampolineMsil.Length
        let newMsil = Array.zeroCreate<Byte>(newLen)
        Array.Copy(trampolineMsil, newMsil, trampolineMsil.Length)
        Array.Copy(origMsil, 0, newMsil, trampolineMsil.Length, origMsil.Length)

        // if necessary strip the ret instruction (0x2A) from the hooked method in order to execute the two merged methods
        if newMsil.[trampolineMsil.Length-1] = byte OpCodes.Ret.Value then
            // replace ret with nop instruction
            newMsil.[trampolineMsil.Length-1] <- byte OpCodes.Nop.Value
       
        newMsil

    let fixEHClausesIfNecessary(methodInfo: CorMethodInfo, methodBase: MethodBase, additionalCodeLength: Int32) =
        let clauses = methodBase.GetMethodBody().ExceptionHandlingClauses
        if clauses.Count > 0 then
            let codeSizeAligned = 
                if (int32 methodInfo.IlCodeSize) % 4 = 0 then 0
                else 4 - (int32 methodInfo.IlCodeSize) % 4
            let mutable startEHClauses = methodInfo.IlCode + new IntPtr(int32 methodInfo.IlCodeSize + codeSizeAligned)
            
            let kind = Marshal.ReadByte(startEHClauses)
            // try to identify FAT header              
            let isFat = (int32 kind &&& 0x40) <> 0

            // it is always plus 3 because even if it is small it is padded with two bytes
            // See: Expert .NET 2.0 IL Assembler p. 296
            // See: https://github.com/dotnet/coreclr/blob/32f0f9721afb584b4a14d69135bea7ddc129f755/src/inc/corhlpr.h#L311
            startEHClauses <- startEHClauses + new IntPtr(4)

            for i=0 to clauses.Count-1 do
                if isFat then
                    let ehFatClausePointer = box(startEHClauses.ToPointer()) :?> nativeptr<CorILMethodSectEhFat>
                    let mutable ehFatClause = NativePtr.read(ehFatClausePointer)
                    
                    // modify the offset value
                    ehFatClause.HandlerOffset <- ehFatClause.HandlerOffset + uint32 additionalCodeLength
                    ehFatClause.TryOffset <- ehFatClause.TryOffset + uint32 additionalCodeLength
                    
                    // write back the result
                    let mutable oldProtection = uint32 0
                    let memSize = Marshal.SizeOf(typeof<CorILMethodSectEhFat>)
                    if not <| VirtualProtect(startEHClauses, uint32 memSize, Protection.PAGE_READWRITE, &oldProtection) then
                        Environment.Exit(-1)
                    let protection = Enum.Parse(typeof<Protection>, oldProtection.ToString()) :?> Protection
                    NativePtr.write ehFatClausePointer ehFatClause
                    
                    // repristinate memory protection flags        
                    VirtualProtect(startEHClauses, uint32 memSize, protection, &oldProtection) |> ignore

                    startEHClauses <- startEHClauses + new IntPtr(memSize)
                else                    
                    let ehSmallClausePointer = box(startEHClauses.ToPointer()) :?> nativeptr<CorILMethodSectEhSmall>
                    let mutable ehSmallClause = NativePtr.read(ehSmallClausePointer)

                    // modify the offset value
                    ehSmallClause.HandlerOffset <- ehSmallClause.HandlerOffset + uint16 additionalCodeLength
                    ehSmallClause.TryOffset <- ehSmallClause.TryOffset + uint16 additionalCodeLength
                    
                    // write back the result
                    let mutable oldProtection = uint32 0
                    let memSize = Marshal.SizeOf(typeof<CorILMethodSectEhSmall>)
                    if not <| VirtualProtect(startEHClauses, uint32 memSize, Protection.PAGE_READWRITE, &oldProtection) then
                        Environment.Exit(-1)
                    let protection = Enum.Parse(typeof<Protection>, oldProtection.ToString()) :?> Protection
                    NativePtr.write ehSmallClausePointer ehSmallClause

                    // repristinate memory protection flags        
                    VirtualProtect(startEHClauses, uint32 memSize, protection, &oldProtection) |> ignore

                    // go to next clause
                    startEHClauses <- startEHClauses + new IntPtr(memSize)

    do
        let compileMethod = this.GetType().GetMethod("CompileMethod")
        RuntimeHelpers.PrepareMethod(compileMethod.MethodHandle)
        
    member this.CompileMethod(realCompileMethod: CompileMethodDeclaration, thisPtr: IntPtr, corJitInfo: IntPtr, methodInfoPtr: nativeptr<CorMethodInfo>, flags: CorJitFlag, nativeEntry: IntPtr, nativeSizeOfCode: IntPtr) =
        if Interlocked.CompareExchange(_inCompileMethod, 1, 0) = 0 then
            let mutable methodInfo = NativePtr.read(methodInfoPtr)
            
            match
                _filters
                |> Seq.map(fun methodFilter -> methodFilter.GetMethod(methodInfo))
                |> Seq.tryFind(Option.isSome)
                |> fun v -> defaultArg v None 
                with
            | Some filteredMethod ->
                try
                    let dispatcherMethod = createDispatcherMethod(filteredMethod)

                    // generate trampoline method
                    let typeBuilder = generateTypeBuilder()
                    let trampolineMethodInfo = generateTrampolineMethod(filteredMethod, typeBuilder, dispatcherMethod)
                    
                    // get info on the newly created method
                    let methodBody = 
                        let trampolineType = typeBuilder.CreateType()
                        let mi = trampolineType.GetMethod(trampolineMethodInfo.MethodName, BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
                        mi.GetMethodBody()

                    // apply patch
                    let patchedMsilCode = patchMethodBody(methodBody, filteredMethod, trampolineMethodInfo.PatchOffset)

                    // update methodInfo
                    let code = mergeCode(methodInfo, patchedMsilCode)

                    // fix EHClause table if necessary
                    fixEHClausesIfNecessary(methodInfo, filteredMethod.Method, patchedMsilCode.Length)
                
                    // modify MethodInfo with new msil code
                    let mutable tmpMethodInfo = methodInfo
                    let ilMem = GCHandle.Alloc(code, GCHandleType.Pinned)
                    methodInfo.IlCode <- ilMem.AddrOfPinnedObject()
                    methodInfo.IlCodeSize <- uint32 code.Length

                    // I have to ensure that the maxstack size is large enough, this because I have to push two arguments for the calli
                    methodInfo.MaxStack <- methodInfo.MaxStack + uint16 10
                
                    NativePtr.write methodInfoPtr methodInfo                        
                with _ ->
                    ()

            | None -> ()            
            Interlocked.Exchange(_inCompileMethod, 0) |> ignore
                    
        realCompileMethod.Invoke(thisPtr, corJitInfo, methodInfoPtr, flags, nativeEntry, nativeSizeOfCode)
            
    member this.AddFilter(invokerType: Type, methodName: string) =
        _filters.Add(new MethodFilter(invokerType, methodName))