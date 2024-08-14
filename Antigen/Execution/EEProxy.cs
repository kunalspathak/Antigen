// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Antigen.Config;
using ExecutionEngine;
using Newtonsoft.Json;
using Utils;

namespace Antigen.Execution
{
    // Each instance of class represents a corresponding instance of ExecutionEngine
    internal class EEProxy
    {
        public readonly Process _process;
        public Stopwatch LastUsedTime { get; } = new Stopwatch();
        private  ReadOnlyDictionary<string, string> _envVars;

        private EEProxy(string host, string executionEngine)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = host,
                Arguments = $"{executionEngine} {Environment.ProcessId}", // Optional: arguments for the process
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            _process = new Process
            {
                StartInfo = startInfo
            };
            SetEnvironmentVariables(startInfo);
            _process.Start();
        }

        private void SetEnvironmentVariables(ProcessStartInfo startInfo)
        {
            Dictionary<string, string> envVars = EnvVarOptions.TestVars(includeOsrSwitches: PRNG.Decide(0.3), false);

            envVars["DOTNET_TieredCompilation"] = "0";
            envVars["DOTNET_JitThrowOnAssertionFailure"] = "1";
            envVars["DOTNET_LegacyExceptionHandling"] = "1";

            foreach (var envVar in envVars)
            {
                startInfo.Environment[envVar.Key] = envVar.Value;
            }

            _envVars = new ReadOnlyDictionary<string, string>(envVars);
        }
        public override string ToString()
        {
            string result = "";
            if (_process.HasExited)
            {
                result += "~";
            }
            else
            {
                result += _process.Id;
            }
            result += $"  [{LastUsedTime.Elapsed}]";
            return result;
        }

        internal static EEProxy GetInstance(string host, string executionEngine)
        {
            if (!File.Exists(host) || !File.Exists(executionEngine))
            {
                throw new FileNotFoundException($"'{host}' or '{executionEngine}' not found.");
            }

            return new EEProxy(host, executionEngine);
        }

        public Response Execute(Request request)
        {
            _process.StandardInput.WriteLine(JsonConvert.SerializeObject(request));

            StringBuilder responseReader = new StringBuilder();
            while (true)
            {
                string line = _process.StandardOutput.ReadLine();
                if ((line == null) || (line == "Done"))
                {
                    break;
                }
                responseReader.AppendLine(line);
            }
            LastUsedTime.Restart();

            try
            {
                return JsonConvert.DeserializeObject<Response>(responseReader.ToString());
            }
            catch (JsonException)
            {
                return new()
                {
                    HasCrashed = true
                };
            }
        }

        /// <summary>
        ///     Returns environment variables used by this process.
        /// </summary>
        /// <returns></returns>
        public ReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _envVars;
        }

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
            }
        }
    }
}
