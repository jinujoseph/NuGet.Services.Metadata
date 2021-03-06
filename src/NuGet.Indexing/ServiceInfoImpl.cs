﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;

namespace NuGet.Indexing
{
    public static class ServiceInfoImpl
    {
        public static async Task TargetFrameworks(IOwinContext context, HashSet<string> targetFrameworks)
        {
            JArray result = new JArray();
            foreach (string targetFramework in targetFrameworks)
            {
                result.Add(targetFramework);
            }

            await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
        }

        public static async Task Segments(IOwinContext context, Dictionary<string, int> segments)
        {
            JArray result = new JArray();
            foreach (var segment in segments)
            {
                JObject segmentInfo = new JObject();
                segmentInfo.Add("segment", segment.Key);
                segmentInfo.Add("documents", segment.Value);
                result.Add(segmentInfo);
            }

            await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
        }

        public static async Task Stats(IOwinContext context, ISearchIndexInfo temp)
        {
            JObject result = new JObject();
            result.Add("numDocs", temp.NumDocs);
            result.Add("indexName", temp.IndexName);
            result.Add("lastReopen", temp.LastReopen);
            result.Add("commitUserData", GetCommitUserData(temp));

            await ServiceHelpers.WriteResponse(context, System.Net.HttpStatusCode.OK, result);
        }

        static JObject GetCommitUserData(ISearchIndexInfo temp)
        {
            JObject obj = new JObject();
            IDictionary<string, string> commitUserData = temp.CommitUserData;
            if (commitUserData != null)
            {
                foreach (var item in commitUserData)
                {
                    obj.Add(item.Key, item.Value);
                }
            }
            return obj;
        }
    }
}