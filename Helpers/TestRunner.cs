// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Antigen.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using Newtonsoft.Json;

namespace Antigen
{
    public class TestRunner
    {
        private static TestRunner _testRunner;
        private static readonly bool s_useDotnet = false;
        private RunOptions RunOptions;

        private static readonly string s_corelibPath = typeof(object).Assembly.Location;
        private static readonly MetadataReference[] s_references =
        {
             MetadataReference.CreateFromFile(s_corelibPath),
             MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(s_corelibPath), "System.Console.dll")),
             MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(s_corelibPath), "System.Runtime.dll")),
             MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location),
    };

        private TestRunner(RunOptions runOptions)
        {
            RunOptions = runOptions;
        }

        public static TestRunner GetInstance(RunOptions runOptions)
        {
            if (_testRunner == null)
            {
                _testRunner = new TestRunner(runOptions);
            }
            return _testRunner;
        }

        /// <summary>
        ///     Compiles the generated <see cref="testCaseRoot"/>.
        /// </summary>
        /// <returns></returns>
        internal CompileResult Compile(SyntaxTree programTree, string assemblyName)
        {
            string assemblyFullPath = Path.Combine(RunOptions.OutputDirectory, $"{assemblyName}.exe");

            var cc = CSharpCompilation.Create($"{assemblyName}.exe", new SyntaxTree[] { programTree }, s_references, Switches.CompileOptions);

            using (var ms = new MemoryStream())
            {
                EmitResult result;
                try
                {
                    result = cc.Emit(ms);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return new CompileResult(ex, ImmutableArray<Diagnostic>.Empty, null, null);
                }

                if (!result.Success)
                    return new CompileResult(null, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), null, null);

                ms.Seek(0, SeekOrigin.Begin);
                File.WriteAllBytes(assemblyFullPath, ms.ToArray());

                return new CompileResult(null, ImmutableArray<Diagnostic>.Empty, assemblyName, assemblyFullPath);
            }
        }

        /// <summary>
        ///     Execute the compiled assembly in an environment that has <paramref name="environmentVariables"/>.
        /// </summary>
        /// <returns></returns>
        internal string Execute(CompileResult compileResult, Dictionary<string, string> environmentVariables)
        {
            Debug.Assert(compileResult.AssemblyFullPath != null);

            if (s_useDotnet)
            {
                foreach (var envVar in environmentVariables)
                {
                    Environment.SetEnvironmentVariable(envVar.Key, envVar.Value, EnvironmentVariableTarget.Process);
                }

                //TODO: if execute in debug vs. release dotnet.exe
                Assembly asm = Assembly.LoadFrom(compileResult.AssemblyFullPath);
                Type testClassType = asm.GetType(compileResult.AssemblyName);
                MethodInfo mainMethodInfo = testClassType.GetMethod(RunOptions.MainMethodName);
                Action<string[]> entryPoint = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), mainMethodInfo);

                Exception ex = null;
                TextWriter origOut = Console.Out;

                MemoryStream ms = new MemoryStream();
                StreamWriter sw = new StreamWriter(ms, Encoding.UTF8);

                try
                {
                    Console.SetOut(sw);
                    entryPoint(null);
                }
                catch (Exception caughtEx)
                {
                    ex = caughtEx;
                    Console.WriteLine(caughtEx);
                }
                finally
                {
                    Console.SetOut(origOut);
                    sw.Close();
                }

                foreach (var envVar in environmentVariables)
                {
                    Environment.SetEnvironmentVariable(envVar.Key, null, EnvironmentVariableTarget.Process);
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
            else
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = RunOptions.CoreRun,
                    Arguments = compileResult.AssemblyFullPath,
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                foreach (var envVar in environmentVariables)
                {
                    info.EnvironmentVariables[envVar.Key] = envVar.Value;
                }

                using (Process proc = new Process())
                {
                    proc.StartInfo = info;

                    bool started = proc.Start();
                    if (!started)
                    {
                        throw new Exception("Process not started");
                    }

                    // proc.StandardInput.Write(JsonConvert.SerializeObject(compileResult.Assembly));
                    // proc.StandardInput.BaseStream.Write(compileResult.Assembly, 0, compileResult.Assembly.Length);
                    // proc.StandardInput.Close();

                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    bool exited = proc.WaitForExit(30 * 1000); // 10 seconds
                    return exited ? output + error : "TIMEOUT";
                }
            }
        }
    }

    internal class CompileResult
    {
        public CompileResult(Exception roslynException, ImmutableArray<Diagnostic> diagnostics, string assemblyName, string assemblyFullPath)
        {
            RoslynException = roslynException;
            List<Diagnostic> errors = new List<Diagnostic>();
            List<Diagnostic> warnings = new List<Diagnostic>();
            foreach (var diag in diagnostics)
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    errors.Add(diag);
                }
                else if (diag.Severity == DiagnosticSeverity.Warning)
                {
                    errors.Add(diag);
                }
            }
            CompileErrors = errors.ToImmutableArray();
            CompileWarnings = warnings.ToImmutableArray();
            AssemblyName = assemblyName;
            AssemblyFullPath = assemblyFullPath;
        }

        public string AssemblyName { get; }
        public Exception RoslynException { get; }
        public ImmutableArray<Diagnostic> CompileErrors { get; }
        public ImmutableArray<Diagnostic> CompileWarnings { get; }
        public string AssemblyFullPath { get; }
    }
}
