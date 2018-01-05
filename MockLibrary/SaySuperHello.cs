using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MockLibrary
{
    public class SaySuperHello : SayHello
    {
        private Int32 _times;

        public SaySuperHello(Int32 times, String txt) : base(txt)
        {
            this._times = times;
        }

        public override void Speak()
        {
            for (var i = 0; i < this._times; i++)
            {
                base.Speak();
            }
        }
    }
}
