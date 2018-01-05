namespace ES.Anathema.Runtime

open System
open System.Reflection
open System.Runtime.InteropServices
open ES.Anathema.Core.Headers

type FilteredMethod = {
    TokenNum: Int16
    NumOfArgumentsToPushInTheStack: Int32
    Method: MethodBase
    IsConstructor: Boolean
    Filter: MethodFilter
}

and MethodFilter(invokerType: Type, methodNameFilter: String) as this =
    let isMonitoredMethod(methodBase: MethodBase) =
        let fullName = String.Format("{0}.{1}", methodBase.DeclaringType.FullName, methodBase.Name)
        fullName.StartsWith(methodNameFilter, StringComparison.OrdinalIgnoreCase)

    let getMethodInfoFromModule(methodInfo: CorMethodInfo, assemblyModule: Module) =
        let mutable info: FilteredMethod option = None
        
        try
            // dirty trick, is there a better way to know the module of the compiled method?
            let mPtr = assemblyModule.ModuleHandle.GetType().GetField("m_ptr", BindingFlags.NonPublic ||| BindingFlags.Instance)
            let mPtrValue = mPtr.GetValue(assemblyModule.ModuleHandle)
            let mpData = mPtrValue.GetType().GetField("m_pData", BindingFlags.NonPublic ||| BindingFlags.Instance)

            if mpData <> null then
                let mpDataValue = mpData.GetValue(mPtrValue) :?> IntPtr
                if mpDataValue = methodInfo.ModuleHandle then
                    // module found, get method name
                    let tokenNum = Marshal.ReadInt16(nativeint(methodInfo.MethodHandle))
                    let token = (0x06000000 + int32 tokenNum)
                    let methodBase = assemblyModule.ResolveMethod(token)
                    
                    if  methodBase.DeclaringType <> null && isMonitoredMethod(methodBase) then
                        let mutable numOfParameters = methodBase.GetParameters() |> Seq.length
                        if not methodBase.IsStatic then
                            // take in account the this parameter
                            numOfParameters <- numOfParameters + 1

                        // compose the result info
                        info <- Some {
                            TokenNum = tokenNum
                            NumOfArgumentsToPushInTheStack = numOfParameters
                            Method = methodBase
                            IsConstructor = methodBase :? ConstructorInfo
                            Filter = this
                        }
        with _ -> ()
        info

    member val Id = Guid.NewGuid() with get
    member val Invoker = invokerType with get

    member this.GetMethod(methodInfo: CorMethodInfo) =
        let mutable info: FilteredMethod option = None
        for assembly in AppDomain.CurrentDomain.GetAssemblies() do
            for assemblyModule in assembly.GetModules() do
                if info.IsNone then
                    info <- getMethodInfoFromModule(methodInfo, assemblyModule)
        info