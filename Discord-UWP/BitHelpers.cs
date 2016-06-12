using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord_UWP
{
    public static class BitHelpers
    {
        public static uint BitsIn(int bytes)
        {
            return (uint) bytes * 8;
        }
    }
}
