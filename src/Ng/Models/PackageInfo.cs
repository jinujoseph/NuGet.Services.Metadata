// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ng.Models
{
    /// <summary>
    /// PackageInfo contains data about the latest stable version of a package.
    /// </summary>
    public class PackageInfo
    {
        /// <summary>
        /// The package id.
        /// </summary>
        [JsonProperty("id")]
        public string PackageId
        {
            get;
            set;
        }

        /// <summary>
        /// The latest stable version of the package.
        /// </summary>
        [JsonProperty("latestStableVersion")]
        public string LatestStableVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates if the idx file has been created and stored.
        /// </summary>
        [JsonProperty("haveIdx")]
        public bool HaveIdx
        {
            get;
            set;
        }

        /// <summary>
        /// The commit id of the latest stable version.
        /// </summary>
        /// <remarks>Included for debugging purposes only.</remarks>
        [JsonProperty("commitId")]
        public Guid CommitId
        {
            get;
            set;
        }

        /// <summary>
        /// The commit timestamp of the latest stable version.
        /// </summary>
        [JsonProperty("commitTimeStamp")]
        public DateTime CommitTimeStamp
        {
            get;
            set;
        }

        /// <summary>
        /// The package download URL of the latest stable version.
        /// </summary>
        [JsonProperty("downloadUrl")]
        public Uri DownloadUrl
        {
            get;
            set;
        }
    }
}