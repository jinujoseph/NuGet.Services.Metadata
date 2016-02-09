// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
