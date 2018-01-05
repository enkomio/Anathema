using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockLibrary
{
    public class SayHello
    {
        private String _message;

        public Double Prop1 { get; set; }

        public SayHello(String txt)
        {
            var arry = new Object[] { "Ciao", 1, new Object() };
            this._message = txt;
        }

        private String FormatMessage()
        {
            return String.Format("Hello {0}!", this._message);
        }

        private String FormatMessage(String p)
        {
            return String.Format("Hello {0}!" + p, this._message);
        }

        public virtual void Speak()
        {
            var message = this.FormatMessage();
            Console.WriteLine(message);
        }

        public Boolean IsEven(Double n)
        {
            var r = n % 2 == 0;
            Console.WriteLine("IsEven");
            return r;
        }

        public void PassEnum()
        {
            ReceiveEnum(MyEnum.Value2);
        }

        private void ReceiveEnum(MyEnum e)
        {
            Console.WriteLine("Enum: " + e.ToString());
        }
    }    
}
