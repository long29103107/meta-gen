using System.Collections.Immutable;
using System.Reflection;
using Long.Metadata.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Long.Metadata.Tests;

public sealed class GeneratedMetadataGeneratorTests
{
    [Fact]
    public void GeneratesStronglyTypedLookupForDerivedMetadataAttributes()
    {
        const string source = """
            using Long.Metadata;

            public sealed class IgnorePropertyAttribute : GeneratedMetadataAttribute
            {
            }

            public sealed class InspectPropertyAttribute : GeneratedMetadataAttribute
            {
            }

            public sealed class User
            {
                [IgnoreProperty]
                public string? Password { get; set; }

                [InspectProperty]
                public Profile Profile { get; set; } = new Profile();

                public string Name { get; set; } = "";
            }

            public sealed class Profile
            {
                public string Normalize()
                {
                    return "ok";
                }
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "Consumer",
            new[] { syntaxTree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new GeneratedMetadataGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var generated = driver.GetRunResult().GeneratedTrees.Single(static tree => tree.FilePath.EndsWith("Long.Metadata.GeneratedMetadata.g.cs"));
        var generatedText = generated.GetText().ToString();

        Assert.Contains("public static class GeneratedMetadata", generatedText);
        Assert.Contains("GetProperties<TAttribute>()", generatedText);
        Assert.Contains("typeof(TAttribute) == typeof(global::IgnorePropertyAttribute)", generatedText);
        Assert.Contains("new global::Long.Metadata.GeneratedPropertyMetadata<global::IgnorePropertyAttribute>", generatedText);
        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<global::Long.Metadata.GeneratedPropertyMetadata<TAttribute>> GetProperties<TAttribute>()", generatedText);
        Assert.Contains("__User_Profile_Invoker", generatedText);
        Assert.Contains("case @\"Normalize\":", generatedText);
        Assert.Contains("return propertyValue.Normalize();", generatedText);
        Assert.Contains("@\"Password\"", generatedText);
        Assert.DoesNotContain("@\"Name\"", generatedText);
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Attribute).Assembly,
            typeof(Enumerable).Assembly,
            typeof(GeneratedMetadataAttribute).Assembly,
            typeof(GeneratedPropertyMetadata).Assembly,
            Assembly.Load("System.Runtime")
        };

        return assemblies
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToImmutableArray<MetadataReference>();
    }
}
