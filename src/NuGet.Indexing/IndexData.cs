﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading;

namespace NuGet.Indexing
{
    public class IndexData<T> where T : class
    {
        private readonly Func<T> _loader;
        private readonly object _lock = new object();
        private T _value;

        public string Name { get; private set; }
        public string Path { get; private set; }
        public T Value { get { return _value; } }
        public DateTime LastUpdatedUtc { get; private set; }
        public TimeSpan UpdateInterval { get; private set; }
        public bool Updating { get; private set; }

        public IndexData(string name, string path, Func<T> loader, TimeSpan updateInterval)
        {
            _loader = loader;

            Name = name;
            Path = path;
            LastUpdatedUtc = DateTime.MinValue;
            UpdateInterval = updateInterval;
            Updating = false;
        }

        public void ReloadIfExpired()
        {
            bool shouldReload = false;
            lock (_lock)
            {
                if ((Value == null || ((DateTime.UtcNow - LastUpdatedUtc) > UpdateInterval)) && !Updating)
                {
                    // Start updating
                    Updating = true;
                    shouldReload = true;
                }
            }

            if (shouldReload)
            {
                Reload();
            }
        }

        public void Reload()
        {
            var newValue = _loader();
            lock (_lock)
            {
                Updating = false;
                LastUpdatedUtc = DateTime.UtcNow;

                // The lock doesn't cover Value, so we need to change it using Interlocked.Exchange.
                Interlocked.Exchange(ref _value, newValue);
            }
        }
    }
}