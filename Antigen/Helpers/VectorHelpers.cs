// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using Antigen.Config;
using Antigen.Tree;

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

                RecordVectorMethods(typeof(Vector2));
                RecordVectorMethods(typeof(Vector3));
                RecordVectorMethods(typeof(Vector4));
                RecordVectorCtors(typeof(Vector2));
                RecordVectorCtors(typeof(Vector3));
                RecordVectorCtors(typeof(Vector4));

                if (Vector64<byte>.IsSupported)
                {
                    RecordVectorMethods(typeof(Vector64));
                }
                if (Vector128<byte>.IsSupported)
                {
                    RecordVectorMethods(typeof(Vector128));
                }
                if (Vector256<byte>.IsSupported)
                {
                    RecordVectorMethods(typeof(Vector256));
                }
                //if (Vector512<byte>.IsSupported)
                //{
                //    RecordVectorMethods(typeof(Vector512));
                //}
                isVectorMethodsInitialized = true;
            }

            foreach (var vectorMethod in vectorMethods)
            {
                if (PRNG.Decide(TC.Config.NumberOfVectorMethodsProbability) || vectorMethod.IsVectorCreateMethod)
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
        private static void RecordVectorMethods(Type vectorType)
        {
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
                    fullMethodName.Contains("Vector`1") || fullMethodName.Contains("Divide"))
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
                            vectorMethods.Add(CreateMethodSignature(vectorType.Name, genericInitVectorMethod));
                        }
                    }
                }
                else
                {
                    vectorMethods.Add(CreateMethodSignature(vectorType.Name, method));
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
