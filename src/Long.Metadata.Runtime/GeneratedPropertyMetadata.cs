namespace Long.Metadata;

public delegate object? GeneratedMetadataInvoker(object declaringInstance, string methodName);

/// <summary>
/// Describes a property decorated with a GeneratedMetadataAttribute-derived attribute.
/// </summary>
public readonly record struct GeneratedPropertyMetadata(
    string DeclaringTypeFullName,
    string DeclaringTypeDisplayName,
    string PropertyName,
    string PropertyTypeFullName,
    string PropertyTypeDisplayName,
    bool IsNullable,
    string AttributeTypeFullName);

/// <summary>
/// Strongly typed view of generated property metadata for a specific metadata attribute.
/// </summary>
/// <typeparam name="TAttribute">The metadata attribute type.</typeparam>
public readonly record struct GeneratedPropertyMetadata<TAttribute>(
    string DeclaringTypeFullName,
    string DeclaringTypeDisplayName,
    string PropertyName,
    string PropertyTypeFullName,
    string PropertyTypeDisplayName,
    bool IsNullable,
    GeneratedMetadataInvoker? Invoker = null)
    where TAttribute : GeneratedMetadataAttribute
{
    public object? Invoke(object declaringInstance, string methodName)
    {
        if (Invoker is null)
        {
            throw new NotSupportedException($"Property '{DeclaringTypeDisplayName}.{PropertyName}' does not expose generated method invocation metadata.");
        }

        return Invoker(declaringInstance, methodName);
    }
}

/// <summary>
/// Describes an abstract class or interface decorated with a GeneratedMetadataAttribute-derived attribute.
/// </summary>
public readonly record struct GeneratedTypeMetadata(
    Type Type,
    string TypeFullName,
    string TypeDisplayName,
    string Accessibility,
    bool IsAbstract,
    bool IsSealed,
    bool IsInterface,
    IReadOnlyList<Type> BaseTypes,
    IReadOnlyList<Type> Interfaces,
    IReadOnlyList<string> AttributeTypeFullNames)
{
    public bool IsAssignableTo(Type contractType)
    {
        return Type == contractType || BaseTypes.Contains(contractType) || Interfaces.Contains(contractType);
    }
}

/// <summary>
/// Strongly typed view of generated type metadata for a specific metadata attribute.
/// </summary>
/// <typeparam name="TAttribute">The metadata attribute type.</typeparam>
public readonly record struct GeneratedTypeMetadata<TAttribute>(
    Type Type,
    string TypeFullName,
    string TypeDisplayName,
    string Accessibility,
    bool IsAbstract,
    bool IsSealed,
    bool IsInterface,
    IReadOnlyList<Type> BaseTypes,
    IReadOnlyList<Type> Interfaces)
    where TAttribute : GeneratedMetadataAttribute
{
    public bool IsAssignableTo(Type contractType)
    {
        return Type == contractType || BaseTypes.Contains(contractType) || Interfaces.Contains(contractType);
    }
}
