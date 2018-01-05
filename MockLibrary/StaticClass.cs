using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockLibrary
{
    public sealed class StaticClass
    {
        public static void Method1()
        {   
            Console.WriteLine("Orig Method1");
        }

        public static void Method2(Object o)
        {
            Console.WriteLine("Orig Method2: " + o.ToString());
        }

        public static void Method3(String o1, Int32 o2)
        {
            Console.WriteLine("Orig Method3: " + o1 + " " + o2.ToString());
        }

        public static void Method2Monitored(Object o)
        {
            Console.WriteLine("Orig Method2: " + o.ToString());
        }

        public static void Method4()
        {
            Console.WriteLine("Orig START Method 4");
            try
            {
                var zero = Int32.Parse("0");
                var i = 1 / zero;                
            }
            catch { Console.WriteLine("Orig END Method 4"); }
        }

        public static Boolean Method5()
        {
            Console.WriteLine("Orig Method5");
            return true;
        }

        public static void Method6_A()
        {
            var helloClass = new SayHello("Hello");
            Method6_B(helloClass);
        }

        public static void Method6_B(SayHello hello)
        {
            hello.Speak();
        }

        public static void Method7(MyStruct s)
        {
            Console.WriteLine("Price: " + s.price);
        }
    }
}
