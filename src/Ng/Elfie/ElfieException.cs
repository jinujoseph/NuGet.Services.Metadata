using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ng.Elfie
{

    [Serializable]
    public class ElfieException : Exception
    {
        public ElfieException() { }
        public ElfieException(string message) : base(message) { }
        public ElfieException(string message, Exception inner) : base(message, inner) { }
        protected ElfieException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}
