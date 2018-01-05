using ES.Anathema.Hook;
using ES.Anathema.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace MLogger
{    
    class Program
    {
        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                      .IsInRole(WindowsBuiltInRole.Administrator);
        }

        // Should run with Administrator privileges
        [STAThread]
        static unsafe void Main(string[] args)
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("In order to inspect other processes you must run MLogger as Administrator");
                Environment.Exit(-1);
            }

            if (args.Length == 0)
            {
                Console.WriteLine("Please specify a .NET program to inspect");
                Environment.Exit(-1);
            }

            var filename = Path.GetFullPath(args[0]);
            if (!File.Exists(filename))
            {
                Console.Error.WriteLine("Filename not found: " + filename);
                Environment.Exit(-2);
            }

            // try load all Assemblies located in the current directory
            var progDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var file in Directory.GetFiles(progDir, "*.dll"))
            {
                Assembly.LoadFile(file);
            }
                        
            var assembly = Assembly.LoadFile(filename);            
            var entryPoint = assembly.EntryPoint;

            // change location if necessary
            var assemblyDir = Path.GetDirectoryName(assembly.Location);
            Directory.SetCurrentDirectory(assemblyDir);

            // instrument
            var runtime = new RuntimeDispatcher();
            var jitHook = new JitHook();
            var hookDelegate = new Hook(runtime.CompileMethod);
            jitHook.InstallHook(hookDelegate);
            var filterName = entryPoint.DeclaringType.FullName.Split('.')[0];
            runtime.AddFilter(typeof(MethodLoggerMonitor), filterName);
            jitHook.Start();

            // finally invoke assembly  
            var parameters = entryPoint.GetParameters();
            if (parameters.Any())
            {
                var entryPointArgs = new String[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    entryPointArgs[i] = "Dummy_arg_" + Guid.NewGuid().ToString("N");
                }
                entryPoint.Invoke(null, new Object[] { entryPointArgs });
            }
            else
            {
                entryPoint.Invoke(null, new Object[] { });
            }

            
            jitHook.Stop();
        }
    }
}
