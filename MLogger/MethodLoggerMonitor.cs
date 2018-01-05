using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MLogger
{
    public class MethodLoggerMonitor
    {
        public MethodLoggerMonitor(MethodBase m, object[] args)
        {
            if (m != null)
            {
                var parameters = m.GetParameters();                
                var fullName = m.DeclaringType.FullName + "." + m.Name;
                Console.Write("[+] {0}(", fullName);
                var parametersString = new StringBuilder();

                foreach (var parameter in parameters)
                {
                    parametersString.AppendFormat(", {0}: {1}", parameter.Name, parameter.ParameterType);
                }

                if (parametersString.Length > 0)
                {
                    parametersString = new StringBuilder(parametersString.ToString().Substring(2));
                }
                Console.Write(parametersString);

                if (m is MethodInfo)
                {
                    Console.WriteLine(") : " + ((MethodInfo)m).ReturnType);
                }
                else
                {
                    Console.WriteLine(")");
                }
            }        
            else
            {
                Console.WriteLine("Unknow method called");
            }    
        }
    }
}
