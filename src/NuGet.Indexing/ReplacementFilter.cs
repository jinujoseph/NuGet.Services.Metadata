// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NuGet.Indexing
{
    public class ReplacementFilter : TokenFilter
    {
        private readonly ITermAttribute _termAttribute;
        private readonly string _oldValue;
        private readonly string _newValue;

        public ReplacementFilter(TokenStream input, string oldValue, string newValue) : base(input)
        {
            _termAttribute = input.AddAttribute<ITermAttribute>();
            _oldValue = oldValue;
            _newValue = newValue;
        }

        public override bool IncrementToken()
        {
            if (!input.IncrementToken())
            {
                return false;
            }
            
            var term = _termAttribute.Term.Replace(_oldValue, _newValue);
            _termAttribute.SetTermBuffer(term);
            return true;
        }
    }
}