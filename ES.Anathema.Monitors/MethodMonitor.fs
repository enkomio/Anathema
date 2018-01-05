namespace ES.Anathema.Monitors

open System
open System.Reflection


type BaseMonitor(callingMethod: MethodBase, args: Object array) =
    do
        if callingMethod <> null then
            Console.WriteLine("!!! HOOKED: " + callingMethod.Name)

        if args <> null then
            for arg in args do
                Console.WriteLine("\t ARG: " + arg.ToString())
