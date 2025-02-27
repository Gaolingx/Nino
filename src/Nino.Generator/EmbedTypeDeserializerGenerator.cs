using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nino.Generator;

[Generator]
public class EmbedTypeDeserializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the syntax receiver
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(IsTypeSyntaxNode, TransformTypeSyntaxNode)
            .Where(type => type != null);

        // Combine the results and generate source code
        var compilationAndTypes = context.CompilationProvider.Combine(typeDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndTypes, GenerateSource!);
    }

    private static bool IsTypeSyntaxNode(SyntaxNode node, CancellationToken ct)
    {
        return node is TypeSyntax || node is StackAllocArrayCreationExpressionSyntax;
    }

    private static TypeSyntax? TransformTypeSyntaxNode(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.Node is TypeSyntax typeSyntax)
        {
            return typeSyntax;
        }

        return null;
    }

    private void GenerateSource(SourceProductionContext context,
        (Compilation Compilation, ImmutableArray<TypeSyntax> Types) input)
    {
        var (compilation, types) = input;
        var typeSymbols = types.Select(t =>
            {
                var model = compilation.GetSemanticModel(t.SyntaxTree);
                var typeInfo = model.GetTypeInfo(t);
                if (typeInfo.Type != null) return typeInfo.Type;

                return null;
            })
            .Where(s => s != null)
            .Select(s => s!)
            .Distinct(SymbolEqualityComparer.Default)
            .Select(s => (ITypeSymbol)s!)
            .Where(symbol =>
            {
                bool IsSerializableType(ITypeSymbol ts)
                {
                    //we dont want void
                    if (ts.SpecialType == SpecialType.System_Void) return false;
                    //we accept string
                    if (ts.SpecialType == SpecialType.System_String) return true;
                    //we want nino type
                    if (ts.IsNinoType()) return true;

                    //we also want unmanaged type
                    if (ts.IsUnmanagedType) return true;

                    //we also want KeyValuePair
                    if (ts.OriginalDefinition.ToDisplayString() ==
                        "System.Collections.Generic.KeyValuePair<TKey, TValue>")
                    {
                        if (ts is INamedTypeSymbol { TypeArguments.Length: 2 } namedTypeSymbol)
                        {
                            return IsSerializableType(namedTypeSymbol.TypeArguments[0]) &&
                                   IsSerializableType(namedTypeSymbol.TypeArguments[1]);
                        }
                    }

                    //if ts implements IList and type parameter is what we want
                    var i = ts.AllInterfaces.FirstOrDefault(namedTypeSymbol =>
                        namedTypeSymbol.Name == "ICollection" && namedTypeSymbol.TypeArguments.Length == 1);
                    if (i != null)
                        return IsSerializableType(i.TypeArguments[0]);

                    //if ts is Span of what we want
                    if (ts.OriginalDefinition.ToDisplayString() == "System.Span<T>")
                    {
                        if (ts is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
                        {
                            return IsSerializableType(namedTypeSymbol.TypeArguments[0]);
                        }
                    }

                    //if ts is nullable of what we want
                    if (ts.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                    {
                        //get type parameter
                        // Get the type argument of Nullable<T>
                        if (ts is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
                        {
                            return IsSerializableType(namedTypeSymbol.TypeArguments[0]);
                        }
                    }

                    //otherwise, we dont want it
                    return false;
                }

                return IsSerializableType(symbol);
            })
            .ToList();
        //for typeSymbols implements ICollection<KeyValuePair<T1, T2>>, add type KeyValuePair<T1, T2> to typeSymbols
        var kvps = typeSymbols.Select(ts =>
        {
            var i = ts.AllInterfaces.FirstOrDefault(namedTypeSymbol =>
                namedTypeSymbol.Name == "ICollection" && namedTypeSymbol.TypeArguments.Length == 1);
            if (i != null)
            {
                return i.TypeArguments[0];
            }

            return null;
        }).Where(ts => ts != null).Select(ts => ts!).ToList();
        typeSymbols.AddRange(kvps);
        typeSymbols = typeSymbols
            .Where(ts =>
            {
                //we dont want unmanaged
                if (ts.IsUnmanagedType) return false;
                //we dont want nino type
                if (ts.IsNinoType()) return false;
                //we dont want string
                if (ts.SpecialType == SpecialType.System_String) return false;
                //we dont want any of the type arguments to be a type parameter
                if (ts is INamedTypeSymbol s)
                {
                    bool IsTypeParameter(ITypeSymbol typeSymbol)
                    {
                        if (typeSymbol.TypeKind == TypeKind.TypeParameter) return true;
                        if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
                        {
                            return namedTypeSymbol.TypeArguments.Any(IsTypeParameter);
                        }

                        return false;
                    }

                    if (s.TypeArguments.Any(IsTypeParameter)) return false;
                }

                //we dont want IList of unmanaged
                var i = ts.AllInterfaces.FirstOrDefault(namedTypeSymbol =>
                    namedTypeSymbol.Name == "IList" && namedTypeSymbol.TypeArguments.Length == 1);
                if (i != null)
                {
                    if (i.TypeArguments[0].IsUnmanagedType) return false;
                }

                //we dont want array of unmanaged
                if (ts is IArrayTypeSymbol arrayTypeSymbol)
                {
                    if (arrayTypeSymbol.ElementType.TypeKind == TypeKind.TypeParameter) return false;
                    if (arrayTypeSymbol.ElementType.IsUnmanagedType) return false;
                }

                //we dont want nullable of unmanaged
                if (ts.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    //get type parameter
                    // Get the type argument of Nullable<T>
                    if (ts is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
                    {
                        if (namedTypeSymbol.TypeArguments[0].IsUnmanagedType) return false;
                    }
                }

                //we dont want span of unmanaged
                if (ts.OriginalDefinition.ToDisplayString() == "System.Span<T>")
                {
                    if (ts is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
                    {
                        if (namedTypeSymbol.TypeArguments[0].IsUnmanagedType) return false;
                    }
                }

                //we dont want IDictionary of unmanaged
                i = ts.AllInterfaces.FirstOrDefault(namedTypeSymbol =>
                    namedTypeSymbol.Name == "IDictionary" && namedTypeSymbol.TypeArguments.Length == 2);
                if (i != null)
                {
                    if (i.TypeArguments[0].IsUnmanagedType && i.TypeArguments[1].IsUnmanagedType) return false;
                }

                return true;
            }).ToList();
        typeSymbols.Sort((t1, t2) =>
            String.Compare(t1.ToDisplayString(), t2.ToDisplayString(), StringComparison.Ordinal));

        var sb = new StringBuilder();
        HashSet<string> addedType = new HashSet<string>();
        HashSet<string> addedElemType = new HashSet<string>();
        foreach (var type in typeSymbols)
        {
            var typeFullName = type.ToDisplayString();
            if (!addedType.Add(typeFullName)) continue;

            //if type is nullable
            if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                if (type is INamedTypeSymbol { TypeArguments.Length: 1 } namedTypeSymbol)
                {
                    var fullName = namedTypeSymbol.TypeArguments[0].ToDisplayString();
                    GenerateNullableStructMethods(sb, namedTypeSymbol.TypeArguments[0].GetDeserializePrefix(),
                        fullName);
                    sb.GenerateClassDeserializeMethods(typeFullName);
                    continue;
                }
            }

            //if type is KeyValuePair
            if (type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            {
                if (type is INamedTypeSymbol { TypeArguments.Length: 2 } namedTypeSymbol)
                {
                    var type1 = namedTypeSymbol.TypeArguments[0].ToDisplayString();
                    var type2 = namedTypeSymbol.TypeArguments[1].ToDisplayString();
                    GenerateKvpStructMethods(sb, namedTypeSymbol.TypeArguments[0].GetDeserializePrefix(), type1,
                        namedTypeSymbol.TypeArguments[1].GetDeserializePrefix(), type2);
                    sb.GenerateClassDeserializeMethods(typeFullName);
                    continue;
                }
            }

            //if type implements IDictionary only
            if (type.AllInterfaces.Any(namedTypeSymbol =>
                    namedTypeSymbol.Name == "IDictionary" && namedTypeSymbol.TypeArguments.Length == 2))
            {
                if (type is INamedTypeSymbol { TypeArguments.Length: 2 } namedTypeSymbol)
                {
                    var type1 = namedTypeSymbol.TypeArguments[0].ToDisplayString();
                    var type2 = namedTypeSymbol.TypeArguments[1].ToDisplayString();
                    sb.AppendLine(GenerateDictionarySerialization(type1, type2, typeFullName, "        "));
                    sb.GenerateClassDeserializeMethods(typeFullName);
                    continue;
                }
            }

            //if type is array
            if (type is IArrayTypeSymbol)
            {
                var elemType = ((IArrayTypeSymbol)type).ElementType.ToDisplayString();
                if (addedElemType.Add(elemType))
                    sb.AppendLine(GenerateArrayCollectionSerialization(
                        ((IArrayTypeSymbol)type).ElementType.GetDeserializePrefix(),
                        elemType, "        "));
                sb.GenerateClassDeserializeMethods(typeFullName);
                continue;
            }

            //ICollection<T>
            if (type is INamedTypeSymbol { TypeArguments.Length: 1 } s)
            {
                var elemType = s.TypeArguments[0].ToDisplayString();
                if (addedElemType.Add(elemType))
                    sb.AppendLine(GenerateArrayCollectionSerialization(s.TypeArguments[0].GetDeserializePrefix(),
                        elemType,
                        "        "));
                if (type.TypeKind != TypeKind.Interface)
                {
                    sb.AppendLine(GenerateCollectionSerialization(s.TypeArguments[0].GetDeserializePrefix(), elemType,
                        typeFullName, typeFullName, "        "));
                }
                else
                {
                    var newFullName = $"List<{elemType}>";
                    sb.AppendLine(GenerateCollectionSerialization(s.TypeArguments[0].GetDeserializePrefix(), elemType,
                        typeFullName, newFullName, "        "));
                }

                sb.GenerateClassDeserializeMethods(typeFullName);
                continue;
            }

            //otherwise we add a comment of the error type
            sb.AppendLine($"// Type: {typeFullName} is not supported");
        }

        var curNamespace = $"{compilation.AssemblyName!}";
        if (!string.IsNullOrEmpty(curNamespace))
            curNamespace = $"{curNamespace}_";
        if (!char.IsLetter(curNamespace[0]))
            curNamespace = $"_{curNamespace}";
        //replace special characters with _
        curNamespace = new string(curNamespace.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        curNamespace += "Nino";

        // generate code
        var code = $$"""
                     // <auto-generated/>

                     using System;
                     using global::Nino.Core;
                     using System.Buffers;
                     using System.Collections.Generic;
                     using System.Collections.Concurrent;
                     using System.Runtime.InteropServices;
                     using System.Runtime.CompilerServices;

                     namespace {{curNamespace}}
                     {
                         public static partial class Deserializer
                         {
                     {{sb}}    }
                     }
                     """;

        context.AddSource("NinoDeserializerExtension.Ext.g.cs", code);
    }

    private static string GenerateArrayCollectionSerialization(string prefix, string elemType, string indent)
    {
        var creationDecl = elemType.EndsWith("[]")
            ? elemType.Insert(elemType.IndexOf("[]", StringComparison.Ordinal), "[length]")
            : $"{elemType}[length]";
        var ret = $$"""
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public static void Deserialize(out {{elemType}}[] value, ref Reader reader)
                    {
                        reader.Read(out ushort typeId);
                        
                        switch (typeId)
                        {
                            case TypeCollector.NullTypeId:
                                value = null;
                                return;
                            case TypeCollector.CollectionTypeId:
                                reader.Read(out int length);
                                value = new {{creationDecl}};
                                for (int i = 0; i < length; i++)
                                {
                                    {{prefix}}(out value[i], ref reader);
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"Invalid type id {typeId}");
                        }
                    }

                    """;
        // indent
        ret = ret.Replace("\n", $"\n{indent}");
        return $"{indent}{ret}";
    }

    private static string GenerateDictionarySerialization(string type1, string type2, string typeFullName,
        string indent = "")
    {
        var ret = $$"""
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public static void Deserialize(out {{typeFullName}} value, ref Reader reader)
                    {
                        reader.Read(out ushort typeId);
                        
                        switch (typeId)
                        {
                            case TypeCollector.NullTypeId:
                                value = null;
                                return;
                            case TypeCollector.CollectionTypeId:
                                reader.Read(out int length);
                                value = new {{typeFullName}}();
                                for (int i = 0; i < length; i++)
                                {
                                    Deserialize(out KeyValuePair<{{type1}}, {{type2}}> kvp, ref reader);
                                    value[kvp.Key] = kvp.Value;
                                }
                                return;
                            default:
                                throw new InvalidOperationException($"Invalid type id {typeId}");
                        }
                    }

                    """;
        // indent
        ret = ret.Replace("\n", $"\n{indent}");
        return $"{indent}{ret}";
    }

    private static string GenerateCollectionSerialization(string prefix, string elemType, string sigTypeFullname,
        string typeFullname,
        string indent)
    {
        var ret = $$"""
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public static void Deserialize(out {{sigTypeFullname}} value, ref Reader reader)
                    {
                        {{elemType}}[] arr;
                        {{prefix}}(out arr, ref reader);
                        if (arr == null)
                        {
                            value = default;
                            return;
                        }
                        
                        value = new {{typeFullname}}(arr);
                    }

                    """;
        // indent
        ret = ret.Replace("\n", $"\n{indent}");
        return $"{indent}{ret}";
    }

    private static void GenerateNullableStructMethods(StringBuilder sb, string prefix, string typeFullName)
    {
        sb.AppendLine($$"""
                                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                                public static void Deserialize(out {{typeFullName}}? value, ref Reader reader)
                                {
                                    reader.Read(out ushort typeId);
                                    
                                    switch (typeId)
                                    {
                                        case TypeCollector.NullTypeId:
                                            value = null;
                                            return;
                                        case TypeCollector.NullableTypeId:
                                            {{prefix}}(out {{typeFullName}} ret, ref reader);
                                            value = ret;
                                            return;
                                        default:
                                            throw new InvalidOperationException($"Invalid type id {typeId}");
                                    }
                                }
                                
                        """);
    }

    private static void GenerateKvpStructMethods(StringBuilder sb, string prefix1, string type1, string prefix2,
        string type2)
    {
        sb.AppendLine($$"""
                                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                                public static void Deserialize(out KeyValuePair<{{type1}}, {{type2}}> value, ref Reader reader)
                                {
                                    {{type1}} key;
                                    {{type2}} val;
                                    {{prefix1}}(out key, ref reader);
                                    {{prefix2}}(out val, ref reader);
                                    value = new KeyValuePair<{{type1}}, {{type2}}>(key, val);
                                }
                                
                        """);
    }
}