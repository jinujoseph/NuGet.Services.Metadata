// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ng.Models
{
    /// <summary>
    /// Represents a fake package which is comprised of assemblies on disk. i.e. the assemblies live in
    /// the local file system rather than in a NuGet package.
    /// </summary>
    /// <remarks>This is used to create indexes for bundles such as the .NET Framework assemblies.</remarks>
    public class AssemblyPackage : RegistrationPackage
    {
    }
}