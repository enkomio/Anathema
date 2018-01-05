using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    public class SayHelloMonitor
    {
        public SayHelloMonitor(MethodBase m, object[] args)
        {
            if (m != null && m.Name.Equals("Speak"))
            {
                // modify hello message
                if (m.Name.Equals("Speak"))
                {
                    var t = args[0];
                    var f = t.GetType().GetField("_message", BindingFlags.NonPublic | BindingFlags.Instance);
                    f.SetValue(t, "World from hooked method >:-B");
                }
            }
        }
    }
}
