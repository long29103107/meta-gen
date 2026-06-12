using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Long.Metadata.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class GeneratedMetadataGenerator : IIncrementalGenerator
{
    private const string GeneratedMetadataAttributeName = "Long.Metadata.GeneratedMetadataAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, cancellationToken) => GetMetadataProperties(ctx, cancellationToken))
            .SelectMany(static (items, _) => items)
            .Collect();

        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, cancellationToken) => GetMetadataType(ctx, cancellationToken))
            .Where(static item => item is not null)
            .Select(static (item, _) => item!.Value)
            .Collect();

        context.RegisterSourceOutput(properties.Combine(types), static (context, items) =>
        {
            var distinctProperties = items.Left
                .GroupBy(static item => item.Key)
                .Select(static group => group.First())
                .OrderBy(static item => item.AttributeTypeFullName, StringComparer.Ordinal)
                .ThenBy(static item => item.DeclaringTypeFullName, StringComparer.Ordinal)
                .ThenBy(static item => item.PropertyName, StringComparer.Ordinal)
                .ToImmutableArray();

            var distinctTypes = items.Right
                .GroupBy(static item => item.Key)
                .Select(static group => group.First())
                .OrderBy(static item => item.TypeFullName, StringComparer.Ordinal)
                .ToImmutableArray();

            context.AddSource("Long.Metadata.GeneratedMetadata.g.cs", GenerateSource(distinctProperties, distinctTypes));
        });
    }

    private static ImmutableArray<MetadataProperty> GetMetadataProperties(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken) is not IPropertySymbol property)
        {
            return ImmutableArray<MetadataProperty>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<MetadataProperty>();

        foreach (var attribute in property.GetAttributes())
        {
            var attributeType = attribute.AttributeClass;
            if (attributeType is null || !InheritsFromGeneratedMetadata(attributeType))
            {
                continue;
            }

            builder.Add(new MetadataProperty(
                Key: $"{attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{property.Name}",
                AttributeTypeCodeName: attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                AttributeTypeFullName: attributeType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                DeclaringTypeCodeName: property.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                DeclaringTypeFullName: property.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                DeclaringTypeDisplayName: property.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                PropertyName: property.Name,
                PropertyTypeCodeName: property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                PropertyTypeFullName: property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                PropertyTypeDisplayName: property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                IsNullable: property.NullableAnnotation == NullableAnnotation.Annotated,
                Methods: GetInvokableMethods(property.Type)));
        }

        return builder.ToImmutable();
    }

    private static MetadataType? GetMetadataType(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not INamedTypeSymbol type ||
            !IsSupportedMetadataType(type))
        {
            return null;
        }

        var attributes = type.GetAttributes()
            .Select(static attribute => attribute.AttributeClass)
            .Where(static attributeType => attributeType is not null && InheritsFromGeneratedMetadata(attributeType))
            .Select(static attributeType => new AttributeKey(
                attributeType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                attributeType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)))
            .OrderBy(static attributeType => attributeType.FullName, StringComparer.Ordinal)
            .ToImmutableArray();

        return new MetadataType(
            Key: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            AttributeTypes: attributes,
            TypeCodeName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeFullName: type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            TypeDisplayName: type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            Accessibility: GetAccessibility(type.DeclaredAccessibility),
            IsAbstract: type.IsAbstract,
            IsSealed: type.IsSealed,
            IsInterface: type.TypeKind == TypeKind.Interface,
            BaseTypeCodeNames: GetBaseTypeCodeNames(type),
            InterfaceCodeNames: GetInterfaceCodeNames(type));
    }

    private static bool IsSupportedMetadataType(INamedTypeSymbol type)
    {
        return type.TypeKind == TypeKind.Interface ||
            type.TypeKind == TypeKind.Class;
    }

    private static bool InheritsFromGeneratedMetadata(INamedTypeSymbol attributeType)
    {
        for (var current = attributeType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == GeneratedMetadataAttributeName)
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateSource(ImmutableArray<MetadataProperty> properties, ImmutableArray<MetadataType> types)
    {
        var propertyAttributes = properties
            .GroupBy(static property => new AttributeKey(property.AttributeTypeCodeName, property.AttributeTypeFullName))
            .OrderBy(static group => group.Key.FullName, StringComparer.Ordinal)
            .ToArray();
        var typeAttributes = types
            .SelectMany(static type => type.AttributeTypes.Select(attributeType => new AttributedMetadataType(attributeType, type)))
            .GroupBy(static item => item.AttributeType)
            .OrderBy(static group => group.Key.FullName, StringComparer.Ordinal)
            .ToArray();

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Long.Metadata");
        source.AppendLine("{");
        source.AppendLine("    public static class GeneratedMetadata");
        source.AppendLine("    {");
        source.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedPropertyMetadata<TAttribute>> GetProperties<TAttribute>()");
        source.AppendLine("            where TAttribute : global::Long.Metadata.GeneratedMetadataAttribute");
        source.AppendLine("        {");

        foreach (var group in propertyAttributes)
        {
            source.Append("            if (typeof(TAttribute) == typeof(");
            source.Append(group.Key.CodeName);
            source.AppendLine("))");
            source.AppendLine("            {");
            source.Append("                return (global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedPropertyMetadata<TAttribute>>)(object)");
            source.Append(GetPropertyFieldName(group.Key.FullName));
            source.AppendLine(";");
            source.AppendLine("            }");
            source.AppendLine();
        }

        source.AppendLine("            return global::System.Array.Empty<global::Long.Metadata.GeneratedPropertyMetadata<TAttribute>>();");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedPropertyMetadata> GetAllProperties()");
        source.AppendLine("        {");
        source.AppendLine("            return __AllProperties;");
        source.AppendLine("        }");
        source.AppendLine();

        source.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedTypeMetadata<TAttribute>> GetTypes<TAttribute>()");
        source.AppendLine("            where TAttribute : global::Long.Metadata.GeneratedMetadataAttribute");
        source.AppendLine("        {");

        foreach (var group in typeAttributes)
        {
            source.Append("            if (typeof(TAttribute) == typeof(");
            source.Append(group.Key.CodeName);
            source.AppendLine("))");
            source.AppendLine("            {");
            source.Append("                return (global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedTypeMetadata<TAttribute>>)(object)");
            source.Append(GetTypeFieldName(group.Key.FullName));
            source.AppendLine(";");
            source.AppendLine("            }");
            source.AppendLine();
        }

        source.AppendLine("            return global::System.Array.Empty<global::Long.Metadata.GeneratedTypeMetadata<TAttribute>>();");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        public static global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedTypeMetadata> GetAllTypes()");
        source.AppendLine("        {");
        source.AppendLine("            return __AllTypes;");
        source.AppendLine("        }");
        source.AppendLine();

        foreach (var group in propertyAttributes)
        {
            source.Append("        private static readonly global::Long.Metadata.GeneratedPropertyMetadata<");
            source.Append(group.Key.CodeName);
            source.Append(">[] ");
            source.Append(GetPropertyFieldName(group.Key.FullName));
            source.AppendLine(" = new global::Long.Metadata.GeneratedPropertyMetadata<" + group.Key.CodeName + ">[]");
            source.AppendLine("        {");

            foreach (var property in group)
            {
                source.Append("            new global::Long.Metadata.GeneratedPropertyMetadata<");
                source.Append(group.Key.CodeName);
                source.Append(">(");
                AppendString(source, property.DeclaringTypeFullName);
                source.Append(", ");
                AppendString(source, property.DeclaringTypeDisplayName);
                source.Append(", ");
                AppendString(source, property.PropertyName);
                source.Append(", ");
                AppendString(source, property.PropertyTypeFullName);
                source.Append(", ");
                AppendString(source, property.PropertyTypeDisplayName);
                source.Append(", ");
                source.Append(property.IsNullable ? "true" : "false");
                if (property.Methods.Length > 0)
                {
                    source.Append(", ");
                    source.Append(GetInvokerName(property));
                }

                source.AppendLine("),");
            }

            source.AppendLine("        };");
            source.AppendLine();
        }

        foreach (var group in typeAttributes)
        {
            source.Append("        private static readonly global::Long.Metadata.GeneratedTypeMetadata<");
            source.Append(group.Key.CodeName);
            source.Append(">[] ");
            source.Append(GetTypeFieldName(group.Key.FullName));
            source.AppendLine(" = new global::Long.Metadata.GeneratedTypeMetadata<" + group.Key.CodeName + ">[]");
            source.AppendLine("        {");

            foreach (var item in group.OrderBy(static item => item.Type.TypeFullName, StringComparer.Ordinal))
            {
                var type = item.Type;
                source.Append("            new global::Long.Metadata.GeneratedTypeMetadata<");
                source.Append(group.Key.CodeName);
                source.Append(">(typeof(");
                source.Append(type.TypeCodeName);
                source.Append("), ");
                AppendString(source, type.TypeFullName);
                source.Append(", ");
                AppendString(source, type.TypeDisplayName);
                source.Append(", ");
                AppendString(source, type.Accessibility);
                source.Append(", ");
                source.Append(type.IsAbstract ? "true" : "false");
                source.Append(", ");
                source.Append(type.IsSealed ? "true" : "false");
                source.Append(", ");
                source.Append(type.IsInterface ? "true" : "false");
                source.Append(", ");
                AppendTypeArray(source, type.BaseTypeCodeNames);
                source.Append(", ");
                AppendTypeArray(source, type.InterfaceCodeNames);
                source.AppendLine("),");
            }

            source.AppendLine("        };");
            source.AppendLine();
        }

        foreach (var property in properties.Where(static property => property.Methods.Length > 0))
        {
            source.Append("        private static object? ");
            source.Append(GetInvokerName(property));
            source.AppendLine("(object declaringInstance, string methodName)");
            source.AppendLine("        {");
            source.Append("            var propertyValue = ((");
            source.Append(property.DeclaringTypeCodeName);
            source.Append(")declaringInstance).");
            source.Append(EscapeIdentifier(property.PropertyName));
            source.AppendLine(";");
            source.AppendLine("            if (propertyValue is null)");
            source.AppendLine("            {");
            source.AppendLine("                return null;");
            source.AppendLine("            }");
            source.AppendLine();
            source.AppendLine("            switch (methodName)");
            source.AppendLine("            {");

            foreach (var method in property.Methods)
            {
                source.Append("                case ");
                AppendString(source, method.Name);
                source.AppendLine(":");

                if (method.ReturnsVoid)
                {
                    source.Append("                    propertyValue.");
                    source.Append(EscapeIdentifier(method.Name));
                    source.AppendLine("();");
                    source.AppendLine("                    return null;");
                }
                else
                {
                    source.Append("                    return propertyValue.");
                    source.Append(EscapeIdentifier(method.Name));
                    source.AppendLine("();");
                }
            }

            source.AppendLine("                default:");
            source.AppendLine("                    throw new global::System.MissingMethodException(\"Generated method metadata was not found for method '\" + methodName + \"'.\");");
            source.AppendLine("            }");
            source.AppendLine("        }");
            source.AppendLine();
        }

        source.AppendLine("        private static readonly global::Long.Metadata.GeneratedPropertyMetadata[] __AllProperties = new global::Long.Metadata.GeneratedPropertyMetadata[]");
        source.AppendLine("        {");

        foreach (var property in properties)
        {
            source.Append("            new global::Long.Metadata.GeneratedPropertyMetadata(");
            AppendString(source, property.DeclaringTypeFullName);
            source.Append(", ");
            AppendString(source, property.DeclaringTypeDisplayName);
            source.Append(", ");
            AppendString(source, property.PropertyName);
            source.Append(", ");
            AppendString(source, property.PropertyTypeFullName);
            source.Append(", ");
            AppendString(source, property.PropertyTypeDisplayName);
            source.Append(", ");
            source.Append(property.IsNullable ? "true" : "false");
            source.Append(", ");
            AppendString(source, property.AttributeTypeFullName);
            source.AppendLine("),");
        }

        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("        private static readonly global::Long.Metadata.GeneratedTypeMetadata[] __AllTypes = new global::Long.Metadata.GeneratedTypeMetadata[]");
        source.AppendLine("        {");

        foreach (var type in types)
        {
            source.Append("            new global::Long.Metadata.GeneratedTypeMetadata(typeof(");
            source.Append(type.TypeCodeName);
            source.Append("), ");
            AppendString(source, type.TypeFullName);
            source.Append(", ");
            AppendString(source, type.TypeDisplayName);
            source.Append(", ");
            AppendString(source, type.Accessibility);
            source.Append(", ");
            source.Append(type.IsAbstract ? "true" : "false");
            source.Append(", ");
            source.Append(type.IsSealed ? "true" : "false");
            source.Append(", ");
            source.Append(type.IsInterface ? "true" : "false");
            source.Append(", ");
            AppendTypeArray(source, type.BaseTypeCodeNames);
            source.Append(", ");
            AppendTypeArray(source, type.InterfaceCodeNames);
            source.Append(", ");
            AppendStringArray(source, type.AttributeTypes.Select(static attributeType => attributeType.FullName));
            source.AppendLine("),");
        }

        source.AppendLine("        };");
        source.AppendLine("    }");
        source.AppendLine("}");

        return source.ToString();
    }

    private static ImmutableArray<InvokableMethod> GetInvokableMethods(ITypeSymbol propertyType)
    {
        if (propertyType is not INamedTypeSymbol namedType || !namedType.IsReferenceType || namedType.SpecialType == SpecialType.System_String)
        {
            return ImmutableArray<InvokableMethod>.Empty;
        }

        return namedType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method =>
                method.MethodKind == MethodKind.Ordinary &&
                method.DeclaredAccessibility == Accessibility.Public &&
                !method.IsStatic &&
                !method.IsGenericMethod &&
                !method.ReturnsByRef &&
                !method.ReturnsByRefReadonly &&
                method.Parameters.Length == 0 &&
                method.ContainingType.SpecialType != SpecialType.System_Object)
            .GroupBy(static method => method.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() == 1)
            .Select(static group => group.Single())
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .Select(static method => new InvokableMethod(
                method.Name,
                method.ReturnType.SpecialType == SpecialType.System_Void))
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetBaseTypeCodeNames(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        for (var current = type.BaseType; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            builder.Add(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> GetInterfaceCodeNames(INamedTypeSymbol type)
    {
        return type.AllInterfaces
            .OrderBy(static item => item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat), StringComparer.Ordinal)
            .Select(static item => item.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToImmutableArray();
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "unknown"
        };
    }

    private static string GetFieldName(string attributeTypeFullName)
    {
        var builder = new StringBuilder("__");
        foreach (var character in attributeTypeFullName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static string GetPropertyFieldName(string attributeTypeFullName)
    {
        return GetFieldName($"{attributeTypeFullName}.Properties");
    }

    private static string GetTypeFieldName(string attributeTypeFullName)
    {
        return GetFieldName($"{attributeTypeFullName}.Types");
    }

    private static string GetInvokerName(MetadataProperty property)
    {
        return GetFieldName($"{property.DeclaringTypeFullName}.{property.PropertyName}.Invoker");
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
            SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
    }

    private static void AppendString(StringBuilder source, string value)
    {
        source.Append("@\"");
        source.Append(value.Replace("\"", "\"\""));
        source.Append('"');
    }

    private static void AppendTypeArray(StringBuilder source, IEnumerable<string> typeCodeNames)
    {
        var items = typeCodeNames.ToArray();
        if (items.Length == 0)
        {
            source.Append("global::System.Array.Empty<global::System.Type>()");
            return;
        }

        source.Append("new global::System.Type[] { ");
        for (var index = 0; index < items.Length; index++)
        {
            if (index > 0)
            {
                source.Append(", ");
            }

            source.Append("typeof(");
            source.Append(items[index]);
            source.Append(")");
        }

        source.Append(" }");
    }

    private static void AppendStringArray(StringBuilder source, IEnumerable<string> values)
    {
        var items = values.ToArray();
        if (items.Length == 0)
        {
            source.Append("global::System.Array.Empty<string>()");
            return;
        }

        source.Append("new string[] { ");
        for (var index = 0; index < items.Length; index++)
        {
            if (index > 0)
            {
                source.Append(", ");
            }

            AppendString(source, items[index]);
        }

        source.Append(" }");
    }

    private readonly record struct AttributeKey(string CodeName, string FullName);

    private readonly record struct MetadataProperty(
        string Key,
        string AttributeTypeCodeName,
        string AttributeTypeFullName,
        string DeclaringTypeCodeName,
        string DeclaringTypeFullName,
        string DeclaringTypeDisplayName,
        string PropertyName,
        string PropertyTypeCodeName,
        string PropertyTypeFullName,
        string PropertyTypeDisplayName,
        bool IsNullable,
        ImmutableArray<InvokableMethod> Methods);

    private readonly record struct MetadataType(
        string Key,
        ImmutableArray<AttributeKey> AttributeTypes,
        string TypeCodeName,
        string TypeFullName,
        string TypeDisplayName,
        string Accessibility,
        bool IsAbstract,
        bool IsSealed,
        bool IsInterface,
        ImmutableArray<string> BaseTypeCodeNames,
        ImmutableArray<string> InterfaceCodeNames);

    private readonly record struct AttributedMetadataType(AttributeKey AttributeType, MetadataType Type);

    private readonly record struct InvokableMethod(string Name, bool ReturnsVoid);
}
