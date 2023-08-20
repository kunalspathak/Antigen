// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Antigen
{
    public partial class TestClass
    {

        private static readonly List<Type> s_vectorGenericArgs = new() { typeof(byte), typeof(sbyte), 
            typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double) };

        private void GenerateVectorMethods()
        {
            if (!isVectorMethodsInitialized)
            {
                vectorMethods = new List<MethodSignature>();

                RecordIntrinsicMethods(typeof(Vector2));
                RecordIntrinsicMethods(typeof(Vector3));
                RecordIntrinsicMethods(typeof(Vector4));
                RecordVectorCtors(typeof(Vector2));
                RecordVectorCtors(typeof(Vector3));
                RecordVectorCtors(typeof(Vector4));

                if (Vector64<byte>.IsSupported)
                {
                    RecordIntrinsicMethods(typeof(Vector64));
                }
                if (Vector128<byte>.IsSupported)
                {
                    RecordIntrinsicMethods(typeof(Vector128));
                }
                if (Vector256<byte>.IsSupported)
                {
                    RecordIntrinsicMethods(typeof(Vector256));
                }
                //if (Vector512<byte>.IsSupported)
                //{
                //    RecordVectorMethods(typeof(Vector512));
                //}

                if (PRNG.Decide(TC.Config.TraditionalMethodsProbability))
                {
                    if (System.Runtime.Intrinsics.X86.Aes.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(System.Runtime.Intrinsics.X86.Aes));
                    }

                    if (Bmi1.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Bmi1));
                    }
                    if (Bmi1.X64.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Bmi1.X64), "Bmi1.X64");
                    }
                    if (Bmi2.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Bmi2));
                    }
                    if (Bmi2.X64.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Bmi2.X64), "Bmi2.X64");
                    }
                    if (Fma.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Fma));
                    }
                    if (Lzcnt.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Lzcnt));
                    }
                    if (Lzcnt.X64.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Lzcnt.X64), "Lzcnt.X64");
                    }
                    if (Pclmulqdq.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Pclmulqdq));
                    }
                    if (Popcnt.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Popcnt));
                    }
                    if (Popcnt.X64.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Popcnt.X64), "Popcnt.X64");
                    }
                }

                if (PRNG.Decide(TC.Config.AvxMethodsProbability))
                {
                    if (Avx.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Avx));
                    }
                    if (Avx2.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Avx2));
                    }
                }

                if (PRNG.Decide(TC.Config.SSEMethodsProbability))
                {
                    if (Sse.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse));
                    }
                    if (Sse2.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse2));
                    }
                    if (Sse3.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse3));
                    }
                    if (Sse41.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse41));
                    }
                    if (Sse42.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse42));
                    }
                    if (Ssse3.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(Sse));
                    }
                }

                if (PRNG.Decide(TC.Config.AdvSimdMethodsProbability))
                {
                    if (AdvSimd.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(AdvSimd));
                    }

                    if (AdvSimd.Arm64.IsSupported)
                    {
                        RecordIntrinsicMethods(typeof(AdvSimd.Arm64), "AdvSimd.Arm64");
                    }
                }

                isVectorMethodsInitialized = true;
            }

            foreach (var vectorMethod in vectorMethods)
            {
                if (PRNG.Decide(TC.Config.RegisterIntrinsicMethodsProbability) || vectorMethod.IsVectorCreateMethod)
                {
                    // Add all vector create methods
                    RegisterMethod(vectorMethod);
                }
            }
        }

        /// <summary>
        ///     Record the vector methods as well as the ones that creates the Vector.
        ///     Applicable for Vector64, Vector128, Vector256, Vector512.
        /// </summary>
        /// <param name="vectorType"></param>
        private static void RecordIntrinsicMethods(Type vectorType, string vectorTypeName = null)
        {
            if (string.IsNullOrEmpty(vectorTypeName))
            {
                vectorTypeName = vectorType.Name;
            }
            var methods = vectorType.GetMethods(BindingFlags.Public | BindingFlags.Static);

            foreach (var method in methods)
            {
                if (method.IsSpecialName)
                {
                    // special methods like properties / operators
                    continue;
                }

                string fullMethodName = method.ToString();

                if (fullMethodName.Contains("IntPtr") || fullMethodName.Contains("ValueTuple") ||
                    fullMethodName.Contains("Matrix") || fullMethodName.Contains("Span") ||
                    fullMethodName.Contains("Quaternion") || fullMethodName.Contains("[]") ||
                    fullMethodName.Contains("*") || fullMethodName.Contains("ByRef") ||
                    fullMethodName.Contains("Vector`1") || fullMethodName.Contains("Divide") ||
                    fullMethodName.Contains("FloatComparisonMode"))
                {
                    // We do not support these types, so ignore these methods.
                    continue;
                }

                if (method.IsGenericMethod)
                {
                    if (method.GetGenericArguments().Count() == 1)
                    {
                        // Only instantiate generic single argument methods
                        foreach (var genericArgument in s_vectorGenericArgs)
                        {
                            var genericInitVectorMethod = method.MakeGenericMethod(genericArgument);
                            vectorMethods.Add(CreateMethodSignature(vectorTypeName, genericInitVectorMethod));
                        }
                    }
                }
                else
                {
                    vectorMethods.Add(CreateMethodSignature(vectorTypeName, method));
                }
            }
        }

        /// <summary>
        ///     Create method signature for the vectorTypeName that has the MethodInfo.
        /// </summary>
        /// <param name="vectorTypeName"></param>
        /// <param name="method"></param>
        /// <returns>Method signature</returns>
        private static MethodSignature CreateMethodSignature(string vectorTypeName, MethodInfo method)
        {
            var ms = new MethodSignature($"{vectorTypeName}.{method.Name}", isVectorGeneric: method.IsGenericMethod, isVectorMethod: true)
            {
                ReturnType = Tree.ValueType.ParseType(method.ReturnType.ToString())
            };
            var containsVectorParam = false;

            foreach (var methodParameter in method.GetParameters())
            {
                if (methodParameter.ParameterType.Name.StartsWith("Vector"))
                {
                    containsVectorParam = true;
                }
                ms.Parameters.Add(new MethodParam()
                {
                    ParamName = methodParameter.Name,
                    ParamType = Tree.ValueType.ParseType(methodParameter.ParameterType.ToString()),
                    PassingWay = ParamValuePassing.None
                });
            }

            if ((method.Name == "Create" || method.Name == "CreateScalar") && !containsVectorParam)
            {
                // Ignore vector param for Create methods because we might not have those variables
                // available.
                ms.IsVectorCreateMethod = true;
            }

            return ms;
        }

        /// <summary>
        ///     Record the vector methods as well as the ones that creates the Vector.
        ///     Applicable for Vector2, Vector3, Vector4.
        /// </summary>
        /// <param name="vectorType"></param>
        private static void RecordVectorCtors(Type vectorType)
        {
            var ctors = vectorType.GetConstructors();

            foreach (var ctor in ctors)
            {
                string fullMethodName = ctor.ToString();

                if (fullMethodName.Contains("IntPtr") || fullMethodName.Contains("ValueTuple") ||
                    fullMethodName.Contains("Matrix") || fullMethodName.Contains("Span") ||
                    fullMethodName.Contains("Quaternion") || fullMethodName.Contains("Vector"))
                {
                    continue;
                }

                var ms = new MethodSignature($"new {vectorType.Name}", isVectorGeneric: false, isVectorMethod: true, isVectorCreateMethod: true)
                {
                    ReturnType = Tree.ValueType.ParseType(vectorType.ToString())
                };

                foreach (var methodParameter in ctor.GetParameters())
                {
                    ms.Parameters.Add(new MethodParam()
                    {
                        ParamName = methodParameter.Name,
                        ParamType = Tree.ValueType.ParseType(methodParameter.ParameterType.ToString()),
                        PassingWay = ParamValuePassing.None
                    });
                }

                vectorMethods.Add(ms);
            }
        }

    }
}
