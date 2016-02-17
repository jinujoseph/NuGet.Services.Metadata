// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 

namespace NuGet.Search.Common
{
    /// <summary>
    /// Class to parse command line arguments.  
    /// </summary>
    /// <remarks>
    /// Command line arguments are expected in the following
    /// format:  /f:Filename
    /// </remarks>
    public class ArgumentParser
    {
        private Dictionary<String, List<String>> m_items;

        /// <summary>
        /// Constructs a new ArgParser with the given valid switches.
        /// </summary>
        /// <param name="validKeys"></param>
        public ArgumentParser(params string[] validKeys)
        {
            if (validKeys == null || validKeys.Length == 0)
            {
                throw new ArgumentNullException("validKeys", "validKeys cannot be null or emtpy.");
            }

            if (validKeys.Where(key => string.IsNullOrWhiteSpace(key)).Count() > 0)
            {
                throw new ArgumentOutOfRangeException("validKeys", "The values in the validKeys list cannot be null or empty.");
            }

            this.m_items = new Dictionary<string, List<String>>();
            this.ValidKeys = new HashSet<string>(validKeys.Select(key => this.TrimArgumentPrefix(key).ToLowerInvariant()));
        }

        /// <summary>
        /// Parses the given command line arguments.
        /// </summary>
        /// <param name="args">The command line arguments to parse.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when an invalid argument is entered.  i.e. The argument
        /// does not meet the specification set in the Constructor.
        /// </exception>
        public void ParseArgs(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            this.Args = args;

            foreach (string arg in args)
            {
                if (String.IsNullOrWhiteSpace(arg))
                {
                    throw new ArgumentOutOfRangeException("args", "The values in the argument list cannot be null or empty.");
                }

                if (!arg.StartsWith("/") && !arg.StartsWith("-"))
                {
                    throw new ArgumentOutOfRangeException("Invalid input. Command line arguments must start with '/' or '-': " + arg);
                }

                // Remove the leading '/' or '-' from the argument.
                string trimmedArg = this.TrimArgumentPrefix(arg);

                // Split the argument into a key/value pair.
                string[] argParts = trimmedArg.Split(new char[] { ':' }, 2);

                string key = argParts[0];
                string value = string.Empty;

                if (argParts.Length > 1)
                {
                    value = argParts[1];
                }

                AddValue(key, value);
            }
        }

        /// <summary>
        /// Adds an argument.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void AddValue(string key, string value)
        {
            string keyToLower = key.ToLowerInvariant();

            if (ValidKeys.Contains(keyToLower))
            {
                if (!m_items.ContainsKey(keyToLower))
                {
                    m_items.Add(keyToLower, new List<string>());
                }

                m_items[keyToLower].Add(value.Trim());
            }
            else
            {
                throw new ArgumentOutOfRangeException("/" + key, "Invalid input: /" + key + ":" + value);
            }
        }

        /// <summary>
        /// Indexer to retrieve the values for a switch.  If no command line
        /// arguments were passed in for the switch, null is returned.
        /// </summary>
        public string[] this[string name]
        {
            get
            {
                string key = name.ToLower();

                if (m_items.ContainsKey(key))
                {
                    return m_items[key].ToArray();
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// The valid keys accepted by the application.
        /// </summary>
        /// <remarks>
        /// ValidKeys should only be set by the constructor.
        /// </remarks>
        public HashSet<string> ValidKeys
        {
            get;
            private set;
        }

        /// <summary>
        /// The command line arguments for this instance of ArgParser.
        /// </summary>
        public string[] Args
        {
            get;
            private set;
        }

        private string TrimArgumentPrefix(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            return value.TrimStart('/', '-');
        }
    }
}