using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockLibrary
{
    public struct MyStruct
    {
        public double price;
        public string title;

        public MyStruct(double p, string t)
        {
            price = p;
            title = t;
        }
    }
}
