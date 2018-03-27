using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRTTestTask
{
    public class BufferPart
    {
        public Byte[] bytes;
        public bool isLastPart;
        public int count;

        public BufferPart(int size, bool isLastPart)
        {
            bytes = new Byte[size];
            this.isLastPart = isLastPart;
        }
    }
}
