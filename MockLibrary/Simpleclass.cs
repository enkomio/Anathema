using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockLibrary
{
    public class TestClass
    {
        public String _message;

        public TestClass(String txt)
        {
            this._message = txt;
        }

        private String FormatMessage()
        {
            return "Hello " + this._message;
        }

        public void SayHello()
        {
            var message = this.FormatMessage();
            Console.WriteLine(message);
        }
    }
}
