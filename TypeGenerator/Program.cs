using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace TypeGenerator
{
    class Program
    {
        private const string AssemblyPath = "C:\\Battlestate Games\\Escape from Tarkov\\EscapeFromTarkov_Data\\Managed\\Assembly-CSharp-cleaned-remapped-stripped.dll";
        private const string OutputPath = "C:\\Battlestate Games\\Escape from Tarkov\\EscapeFromTarkov_Data\\Managed";

        static void Main(string[] args)
        {
            /*
             * get assembly
             * remove methods that are not property get-setters
             * remove ctors
             * removed compiled classes
             */
            
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(OutputPath);
            
            resolver.RemoveSearchDirectory(".");
            resolver.RemoveSearchDirectory("bin");
            
            var readerParameters = new ReaderParameters { AssemblyResolver = resolver };
            var assemblyPath = Path.Combine(Directory.GetCurrentDirectory(), AssemblyPath);
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), OutputPath, "Assembly-CSharp-eft.dll");

            var oldAssembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
            var types = oldAssembly.MainModule.GetTypes().ToList();
            var typesToRemove = new List<TypeDefinition>();

            foreach (var type in types)
            {
                if (type.Name.Contains("<") || type.Name.Contains(">"))
                {
                    typesToRemove.Add(type);
                    continue;
                }

                if (type.Name.Contains("Struct"))
                {
                    type.Attributes = TypeAttributes.Public;
                }

                if (type.IsInterface)
                {
                    continue;
                }

                if (type.HasProperties)
                {
                    foreach (var prop in type.Properties)
                    {
                        // if set is private and get is protected, make get none
                        if (prop.GetMethod != null && prop.SetMethod != null)
                        {
                            if (prop.SetMethod.Attributes == (MethodAttributes.Private | MethodAttributes.HideBySig |
                                                              MethodAttributes.SpecialName)  &&
                                prop.GetMethod.Attributes == (MethodAttributes.Family | MethodAttributes.HideBySig |
                                                              MethodAttributes.SpecialName))
                            {
                                prop.GetMethod.Attributes = MethodAttributes.HideBySig | MethodAttributes.SpecialName;
                            }

                            if (prop.SetMethod.Attributes ==
                                (MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig |
                                 MethodAttributes.SpecialName) && prop.GetMethod.Attributes ==
                                (MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Static |
                                 MethodAttributes.SpecialName))
                            {
                                prop.GetMethod.Attributes = MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static;
                            }
                        }
                    }
                }

                if (type.HasNestedTypes)
                {
                    foreach (var nestedType in type.NestedTypes)
                    {
                        if (nestedType.Name.Contains("Struct"))
                        {
                            nestedType.Attributes = TypeAttributes.Public;
                        }

                        if (nestedType.HasFields)
                        {
                            var nestedFieldsToRemove = new List<FieldDefinition>();
                            foreach (var field in nestedType.Fields)
                            {
                                if (field.Name.Contains("<") || field.Name.Contains(">"))
                                {
                                    nestedFieldsToRemove.Add(field);
                                }
                            }

                            foreach (var field in nestedFieldsToRemove)
                            {
                                nestedType.Fields.Remove(field);
                            }
                        }
                        
                        if (nestedType.HasMethods)
                        {
                            var nestedMethodsToRemove = new List<MethodDefinition>();
                            foreach (var method in nestedType.Methods)
                            {
                                if (method.HasParameters)
                                {
                                    var paramsToChange = new List<ParameterDefinition>();
                                    foreach (var param in method.Parameters)
                                    {
                                        if (string.IsNullOrEmpty(param.ParameterType.Name))
                                        {
                                            paramsToChange.Add(param);
                                        }
                                    }

                                    foreach (var param in paramsToChange)
                                    {
                                        var parameter = method.Parameters.First(x => x.Name == param.Name);
                                        method.Parameters.Remove(parameter);
                                        var random = new Random();
                                        method.Parameters.Add(new ParameterDefinition($"P_{random.Next(1, 20)}", parameter.Attributes, oldAssembly.MainModule.ImportReference(typeof(object))));
                                    }
                                }

                                if (method.Name.Contains("<") || method.Name.Contains(">") || method.Name.Contains("ctor") || method.Name.Contains("op_"))
                                {
                                    nestedMethodsToRemove.Add(method);
                                }
                            }

                            foreach (var nestedMethod in nestedMethodsToRemove)
                            {
                                nestedType.Methods.Remove(nestedMethod);
                            }
                        }
                    }
                }
                
                if (type.HasFields)
                {
                    foreach (var field in type.Fields)
                    {
                        if (field.CustomAttributes.Any(x => x.AttributeType == null))
                        {
                            field.CustomAttributes.Clear();
                        }
                    }
                }
                
                // if type is a compiler Generated type - remove
                if (type.HasCustomAttributes && type.CustomAttributes.All(x => x.AttributeType != null))
                {
                    if (type.CustomAttributes.Any(x =>
                            x.AttributeType.FullName ==
                            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).FullName))
                    {
                        typesToRemove.Add(type);
                        continue;
                    }
                }
                var methodsToRemove = new List<MethodDefinition>();
                // get methods and remove all apart from get-set for props
                if (type.HasMethods)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.HasParameters)
                        {
                            var paramsToChange = new List<ParameterDefinition>();
                            foreach (var param in method.Parameters)
                            {
                                if (string.IsNullOrEmpty(param.ParameterType.Name))
                                {
                                    paramsToChange.Add(param);
                                }
                            }

                            foreach (var param in paramsToChange)
                            {
                                var parameter = method.Parameters.First(x => x.Name == param.Name);
                                method.Parameters.Remove(parameter);
                                var random = new Random();
                                method.Parameters.Add(new ParameterDefinition($"P_{random.Next(1, 20)}", parameter.Attributes, oldAssembly.MainModule.ImportReference(typeof(object))));
                            }
                        }

                        if (method.Name.Contains("<") || method.Name.Contains(">") || method.Name.Contains("ctor") || method.Name.Contains("op_"))
                        {
                            methodsToRemove.Add(method);
                        }
                    }
                }

                foreach (var method in methodsToRemove)
                {
                    type.Methods.Remove(method);
                }
            }

            foreach (var type in typesToRemove)
            {
                types.Remove(type);
            }
            
            // --------------------- check for issue
            foreach (var type in types)
            {
                if (type.HasCustomAttributes)
                {
                    if (type.CustomAttributes.Any(x => x.AttributeType == null))
                    {
                        type.CustomAttributes.Clear();
                    }
                }

                if (type.HasProperties)
                {
                    foreach (var prop in type.Properties)
                    {
                        if (prop.HasCustomAttributes && prop.CustomAttributes.Any(x => x.AttributeType == null))
                        {
                            prop.CustomAttributes.Clear();
                        }
                    }
                }
                
                if (type.HasFields)
                {
                    foreach (var field in type.Fields)
                    {
                        if (field.CustomAttributes.Any(x => x.AttributeType == null))
                        {
                            field.CustomAttributes.Clear();
                        }
                    }
                }
            }
            
            oldAssembly.Write(outputPath);
        }
    }
}