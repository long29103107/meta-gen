# Long.Metadata

`Long.Metadata` is a .NET incremental source generator library that builds strongly typed metadata registries at compile time. The main goal is to support Native AOT scenarios by removing runtime reflection scanning for attributed properties and application types.

Instead of scanning assemblies, types, properties, and attributes at runtime, the generator uses Roslyn during compilation and emits direct lookup code into the consumer assembly.

## Free For Everyone

This project is intended to be free for everyone to use, copy, learn from, adapt, package, and reuse in personal, internal, or commercial projects.

If you publish it publicly or redistribute packages from it, add a `LICENSE` file to the repository so the legal terms are explicit for downstream users.

## What This Repository Contains

- `src/Long.Metadata.Abstractions`
  - Defines `GeneratedMetadataAttribute`.
  - Developers create custom metadata attributes by deriving from this base type.

- `src/Long.Metadata.Runtime`
  - Defines metadata models such as `GeneratedPropertyMetadata<TAttribute>` and `GeneratedTypeMetadata`.
  - Exposes the generated metadata result shape used by consuming applications.

- `src/Long.Metadata.Generator`
  - Implements the incremental source generator.
  - Uses Roslyn semantic analysis to find properties decorated with attributes derived from `GeneratedMetadataAttribute`.
  - Builds a compile-time inventory of source-declared classes and interfaces, including accessibility, type kind, base types, and implemented interfaces.
  - Emits AOT-safe lookup and invocation code.

- `samples/Long.Metadata.Sample`
  - Demonstrates consuming the generator.
  - Shows metadata lookup and method invocation on class properties.

- `tests/Long.Metadata.Tests`
  - Validates generator output with Roslyn in-memory compilation.

## Problem Solved

Runtime reflection scanning is risky or unavailable in Native AOT applications because metadata can be trimmed and reflection access needs explicit preservation. `Long.Metadata` moves the discovery step to compile time:

1. Developer marks properties with custom metadata attributes.
2. Source generator finds those properties during compilation.
3. Source generator also records source-declared class/interface metadata for DI and business rules.
4. Generator emits static arrays and switch-based lookup code.
5. Runtime code reads generated metadata without reflection scanning.

## Basic Usage

Reference runtime normally and the generator as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\Long.Metadata.Runtime\Long.Metadata.Runtime.csproj" />
  <ProjectReference Include="..\Long.Metadata.Generator\Long.Metadata.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Create a custom attribute for property metadata:

```csharp
using Long.Metadata;

public sealed class IgnorePropertyAttribute : GeneratedMetadataAttribute
{
}
```

Apply it to a property:

```csharp
public sealed class User
{
    [IgnoreProperty]
    public string Password { get; set; } = "";
}
```

Read generated property metadata:

```csharp
var ignored = GeneratedMetadata.GetProperties<IgnorePropertyAttribute>();

foreach (var property in ignored)
{
    Console.WriteLine($"{property.DeclaringTypeDisplayName}.{property.PropertyName}");
}
```

## Adopt It In Another Project

There are two practical ways to reuse this repository from another application.

### Option 1: reference the source projects

Use this while developing the library or while the consuming app lives in the same solution or repository. A common setup is to copy or vendor the `src/Long.Metadata.*` projects into a shared `tools`, `libs`, or `submodules` folder, then reference them from the application project. The code is intended to be free for everyone to reuse.

```xml
<ItemGroup>
  <ProjectReference Include="..\Long.Metadata\src\Long.Metadata.Runtime\Long.Metadata.Runtime.csproj" />
  <ProjectReference Include="..\Long.Metadata\src\Long.Metadata.Generator\Long.Metadata.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

The runtime project brings in `GeneratedMetadataAttribute`, `GeneratedPropertyMetadata`, and `GeneratedTypeMetadata`. The generator project must be referenced as an analyzer so it runs during compilation instead of becoming a normal runtime dependency.

### Option 2: pack and consume NuGet packages

Pack the reusable projects:

```bash
dotnet pack src/Long.Metadata.Abstractions/Long.Metadata.Abstractions.csproj -c Release -o artifacts/packages
dotnet pack src/Long.Metadata.Runtime/Long.Metadata.Runtime.csproj -c Release -o artifacts/packages
dotnet pack src/Long.Metadata.Generator/Long.Metadata.Generator.csproj -c Release -o artifacts/packages
```

For local testing, add the folder as a NuGet source:

```bash
dotnet nuget add source ./artifacts/packages --name LongMetadataLocal
```

Then reference the packages from a consumer project:

```bash
dotnet add package Long.Metadata.Runtime --version 0.1.0 --source ./artifacts/packages
dotnet add package Long.Metadata.Generator --version 0.1.0 --source ./artifacts/packages
```

Make sure the generator package is treated as an analyzer in the final project file:

```xml
<ItemGroup>
  <PackageReference Include="Long.Metadata.Runtime" Version="0.1.0" />
  <PackageReference Include="Long.Metadata.Generator"
                    Version="0.1.0"
                    PrivateAssets="all"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

If you publish to a private feed such as Azure Artifacts, GitHub Packages, or a company NuGet server, use the same package references and configure the feed in `NuGet.config` or through `dotnet nuget add source`. The packages produced from this repository are intended to be reusable by any project that references them.

When you publish an updated package, bump the package version and update the consumer project with `dotnet add package Long.Metadata.Runtime --version <new-version>` and `dotnet add package Long.Metadata.Generator --version <new-version>`.

Before publishing the generator package, inspect the `.nupkg` and verify that the generator assembly is available as an analyzer. A correct package should expose `Long.Metadata.Generator.dll` to consuming projects at compile time and should not require the consumer app to load the generator at runtime.

### Consumer checklist

- Reference `Long.Metadata.Runtime` normally.
- Reference `Long.Metadata.Generator` as an analyzer.
- Define custom metadata attributes by deriving from `GeneratedMetadataAttribute`.
- Use `GeneratedMetadata.GetProperties<TAttribute>()` for attributed property metadata.
- Use `GeneratedMetadata.GetAllTypes()` for class/interface inventory and DI-style filtering.
- Use `GeneratedMetadata.GetTypes<TAttribute>()` only when you intentionally mark types with metadata attributes.

## Generated API

The generator emits a `Long.Metadata.GeneratedMetadata` class into the consuming assembly.

```csharp
GeneratedMetadata.GetProperties<TAttribute>();
GeneratedMetadata.GetAllProperties();
GeneratedMetadata.GetTypes<TAttribute>();
GeneratedMetadata.GetAllTypes();
```

`GetProperties<TAttribute>()` returns:

```csharp
IReadOnlyList<GeneratedPropertyMetadata<TAttribute>>
```

Each property metadata entry includes:

- `DeclaringTypeFullName`
- `DeclaringTypeDisplayName`
- `PropertyName`
- `PropertyTypeFullName`
- `PropertyTypeDisplayName`
- `IsNullable`

The non-generic `GeneratedPropertyMetadata` model also includes `AttributeTypeFullName`.

`GetAllTypes()` returns the full generated type inventory:

```csharp
IReadOnlyList<GeneratedTypeMetadata>
```

`GetTypes<TAttribute>()` returns the attributed type view:

```csharp
IReadOnlyList<GeneratedTypeMetadata<TAttribute>>
```

Each type metadata entry includes:

- `Type`
- `TypeFullName`
- `TypeDisplayName`
- `Accessibility`
- `IsAbstract`
- `IsSealed`
- `IsInterface`
- `BaseTypes`
- `Interfaces`

The non-generic `GeneratedTypeMetadata` model returned by `GetAllTypes()` also includes `AttributeTypeFullNames`.

## Type Metadata For DI Or Business Registries

The generator tracks all source-declared classes and interfaces at compile time. Each generated type entry includes its access modifier, whether it is abstract, sealed, or an interface, plus its base-type chain and implemented interfaces.

```csharp
public interface IUserService
{
}

public sealed class UserService : IUserService
{
}

public abstract class BaseHandler
{
}

public class ProductHandler : BaseHandler
{
}

var services = GeneratedMetadata.GetAllTypes()
    .Where(type => type.Accessibility == "public" &&
        !type.IsAbstract &&
        !type.IsInterface &&
        type.IsAssignableTo(typeof(IUserService)));

foreach (var service in services)
{
    services.AddScoped(service.Type);
}
```

You can still apply `GeneratedMetadataAttribute`-derived attributes to types and read them through `GetTypes<TAttribute>()`, but attribute usage is optional for inheritance/interface discovery. `GetAllTypes()` is the full generated type inventory for the current compilation.

Useful type filters include:

```csharp
var publicServices = GeneratedMetadata.GetAllTypes()
    .Where(type => type.Accessibility == "public" &&
        !type.IsAbstract &&
        !type.IsInterface &&
        type.IsAssignableTo(typeof(IUserService)));

var concreteHandlers = GeneratedMetadata.GetAllTypes()
    .Where(type => !type.IsAbstract &&
        !type.IsInterface &&
        type.IsAssignableTo(typeof(BaseHandler)));

var sealedTypes = GeneratedMetadata.GetAllTypes()
    .Where(type => type.IsSealed);
```

## Method Invocation On Class Properties

If the decorated property is a reference type, the generator can also emit direct method invokers for public instance methods that are:

- parameterless
- non-generic
- not static
- not inherited from `object`

Example:

```csharp
public sealed class AuditedPropertyAttribute : GeneratedMetadataAttribute
{
}

public sealed class User
{
    [AuditedProperty]
    public Account Account { get; set; } = new("active");
}

public sealed class Account(string status)
{
    public string GetStatus() => status;
}

var metadata = GeneratedMetadata.GetProperties<AuditedPropertyAttribute>().Single();
var result = metadata.Invoke(new User(), "GetStatus");
```

The generated code is a direct call, conceptually:

```csharp
return ((User)declaringInstance).Account.GetStatus();
```

No runtime reflection is used. If the property value is `null`, invocation returns `null`. If the target method returns `void`, invocation returns `null`. If the method name was not generated, invocation throws `MissingMethodException`.

## How It Works Internally

At compile time:

1. The generator filters syntax nodes to properties with attributes and type declarations.
2. It asks Roslyn for the `IPropertySymbol` or `INamedTypeSymbol`.
3. It checks each metadata attribute symbol and walks its base types.
4. If the attribute derives from `Long.Metadata.GeneratedMetadataAttribute`, the property or attributed type is captured.
5. It groups captured properties and attributed types by attribute type, while keeping a full type inventory.
6. It emits:
   - typed static arrays of `GeneratedPropertyMetadata<TAttribute>`
   - typed static arrays of `GeneratedTypeMetadata<TAttribute>` for attributed types
   - a non-generic generated type inventory for all source-declared classes and interfaces
   - a generic `GetProperties<TAttribute>()` lookup
   - a generic `GetTypes<TAttribute>()` lookup
   - `GetAllProperties()`
   - `GetAllTypes()`
   - optional direct method invokers for class properties

At runtime:

1. `GeneratedMetadata.GetProperties<TAttribute>()` compares `typeof(TAttribute)` against known generated attribute types.
2. `GeneratedMetadata.GetTypes<TAttribute>()` returns attributed type metadata when type attributes are used.
3. `GeneratedMetadata.GetAllTypes()` returns a generated static inventory of source-declared classes and interfaces.
4. `Invoke(instance, methodName)` dispatches through generated switch statements and direct method calls.

This means runtime behavior is deterministic, trim-friendly, and AOT-safe.

## Target Frameworks

- `Long.Metadata.Abstractions`: `net8.0`, `net10.0`
- `Long.Metadata.Runtime`: `net8.0`, `net10.0`
- `Long.Metadata.Generator`: `netstandard2.0`
- Sample: `net8.0`, `net10.0`
- Tests: `net8.0`

## Run The Sample

```bash
dotnet run --project samples/Long.Metadata.Sample/Long.Metadata.Sample.csproj --framework net8.0
```

Expected output:

```text
User.Password: string
Account.GetStatus(): active
BaseHandler: public abstract
IUserService: public interface
public sealed class UserService implements IUserService
public class ProductHandler inherits BaseHandler
```

## Run Tests

```bash
dotnet test Long.Metadata.slnx
```

## Result Achieved

The repository now provides a working source generator solution that:

- supports .NET 8 and .NET 10 runtime projects
- discovers custom attributes derived from `GeneratedMetadataAttribute`
- generates strongly typed metadata registries
- avoids runtime reflection scanning
- supports Native AOT-friendly lookup
- supports direct method invocation on decorated class properties
- supports generated class/interface metadata for DI or business registries
- includes a sample consumer
- includes unit tests validating generated output
- documents the implementation and runtime behavior
