using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class TestMethodMonitor
    {
        public TestMethodMonitor(MethodBase m, object[] args)
        {
            if (m != null)
            {
                Console.WriteLine("HOOK METHOD: " + m.Name);
            }

            if (args != null && args.Length > 0)
            {
                foreach (var arg in args)
                {
                    Console.WriteLine("\tARG: " + arg.ToString());
                }
            }
        }
    }
}
