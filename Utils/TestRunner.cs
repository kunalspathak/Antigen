// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Antigen.Compilation;
using Antigen.Execution;
using ExecutionEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Utils
{
    public enum TestResult
    {
        RoslynException,
        CompileError,
        Overflow,
        DivideByZero,
        OutputMismatch,
        Assertion,
        OtherError,
        Pass,
        OOM
    }


    public class TestRunner
    {
        internal static readonly CSharpCompilationOptions ReleaseCompileOptions = new (
            OutputKind.ConsoleApplication,
            concurrentBuild: true,
            optimizationLevel: OptimizationLevel.Release);
        internal static readonly CSharpCompilationOptions DebugCompileOptions = new(
            OutputKind.ConsoleApplication,
            concurrentBuild: true,
            optimizationLevel: OptimizationLevel.Debug);

        private static TestRunner _testRunner;
        private static readonly bool s_useDotnet = false;
        private readonly string _coreRun;
        private readonly string _outputDirectory;
        private readonly EEDriver _driver;

        private static readonly string s_corelibPath = typeof(object).Assembly.Location;
        private static readonly MetadataReference[] s_references =
        {
             MetadataReference.CreateFromFile(s_corelibPath),
             MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(s_corelibPath), "System.Console.dll")),
             MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(s_corelibPath), "System.Runtime.dll")),
             MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location),
        };

        private TestRunner(string coreRun, string outputFolder)
        {
            _coreRun = coreRun;
            _outputDirectory = outputFolder;
            _driver = EEDriver.GetInstance(coreRun, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ExecutionEngine.dll"));
        }

        public static TestRunner GetInstance(string coreRun, string outputFolder)
        {
            lock (ReleaseCompileOptions)
            {
                if (_testRunner == null)
                {
                    _testRunner = new TestRunner(coreRun, outputFolder);
                }
            }
            return _testRunner;
        }

        /// <summary>
        ///     Compiles the generated <see cref="testCaseRoot"/>.
        /// </summary>
        /// <returns></returns>
        internal CompileResult Compile(SyntaxTree programTree, string assemblyName)
        {
            string assemblyFullPath = Path.Combine(_outputDirectory, $"{assemblyName}.exe");

            var cc = CSharpCompilation.Create($"{assemblyName}.exe", [programTree], s_references, ReleaseCompileOptions);

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
                    return new CompileResult(ex);
                }

                if (!result.Success)
                {
#if DEBUG
                    return new CompileResult(result.Diagnostics);
#else
                    return new CompileResult(null, null, null, null);
#endif
                }

                ms.Seek(0, SeekOrigin.Begin);
                File.WriteAllBytes(assemblyFullPath, ms.ToArray());
                //Console.WriteLine($"{ms.Length} bytes");

                return new CompileResult(assemblyName, assemblyFullPath, null, ms.ToArray());
            }
        }

        /// <summary>
        ///     Execute the compiled assembly in an environment that has <paramref name="environmentVariables"/>.
        /// </summary>
        /// <returns></returns>
        internal string Execute(CompileResult compileResult, Dictionary<string, string> environmentVariables, int timeoutInSecs = 30)
        {
            Debug.Assert(compileResult.AssemblyFullPath != null);

            if (s_useDotnet)
            {
                if (environmentVariables != null)
                {
                    foreach (var envVar in environmentVariables)
                    {
                        Environment.SetEnvironmentVariable(envVar.Key, envVar.Value, EnvironmentVariableTarget.Process);
                    }
                }

                //TODO: if execute in debug vs. release dotnet.exe
                Assembly asm = Assembly.LoadFrom(compileResult.AssemblyFullPath);
                Type testClassType = asm.GetType(compileResult.AssemblyName);
                MethodInfo mainMethodInfo = testClassType.GetMethod("Main");
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

                if (environmentVariables != null)
                {
                    foreach (var envVar in environmentVariables)
                    {
                        Environment.SetEnvironmentVariable(envVar.Key, null, EnvironmentVariableTarget.Process);
                    }
                }

                return Encoding.UTF8.GetString(ms.ToArray());
            }
            else
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = _coreRun,
                    Arguments = compileResult.AssemblyFullPath,
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                if (environmentVariables != null)
                {
                    foreach (var envVar in environmentVariables)
                    {
                        info.EnvironmentVariables[envVar.Key] = envVar.Value;
                    }
                }

                using (Process proc = new Process())
                {
                    proc.StartInfo = info;

                    bool started = proc.Start();
                    if (!started)
                    {
                        throw new Exception("Process not started");
                    }

                    StringBuilder output = new StringBuilder();
                    proc.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            output.AppendLine(e.Data);
                        }
                    });

                    proc.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            output.AppendLine(e.Data);
                        }
                    });

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    bool exited = proc.WaitForExit(timeoutInSecs * 1000); // 10 seconds
                    if (!exited)
                    {
                        try
                        {
                            proc.Kill(true);
                        }
                        catch { }
                        return "TIMEOUT";
                    }

                    string finalOutput = String.Empty;
                    try
                    {
                        finalOutput = output.ToString();
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                    return finalOutput.Trim();
                }
            }
        }

        internal ExecuteResult Execute(CompileResult compileResult)
        {
            var result = _driver.Execute(new()
            {
                Debug = compileResult.DebugAssembly,
                Release = compileResult.ReleaseAssembly
            });
            if (result.IsJitAssert)
            {
                return ExecuteResult.GetAssertionFailureResult(GetFailureOutput(result));
            }
            else if (result.IsTimeout)
            {
                return ExecuteResult.GetTimeoutResult();
            }
            else if (!string.IsNullOrEmpty(result.DebugError) || !string.IsNullOrEmpty(result.ReleaseError))
            {
                if (result.DebugError != result.ReleaseError)
                {
                    return ExecuteResult.GetOutputMismatchResult(GetFailureOutput(result));
                }
                else
                {
                    return ExecuteResult.GetOtherErrorResult(result.DebugError);
                }
            }
            else if (result.DebugOutput != result.ReleaseOutput)
            {
                return ExecuteResult.GetOutputMismatchResult(GetFailureOutput(result));
            }
            else
            {
                return ExecuteResult.GetSuccessResult();
            }
        }

        internal string GetFailureOutput(Response response)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Environment:");
            foreach (var envVar in response.EnvironmentVariables)
            {
                sb.AppendLine($"{envVar.Key}={envVar.Value}");
            }
            sb.AppendLine();
            sb.AppendLine($"Debug: {response.DebugOutput}");
            sb.AppendLine(response.DebugError);
            sb.AppendLine($"Release: {response.ReleaseOutput}");
            sb.AppendLine(response.ReleaseError);
            return sb.ToString();
        }
    }
}
