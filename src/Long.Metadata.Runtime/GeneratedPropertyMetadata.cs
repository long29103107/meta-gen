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
