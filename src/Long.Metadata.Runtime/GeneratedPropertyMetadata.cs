namespace Long.Metadata;

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
    bool IsNullable)
    where TAttribute : GeneratedMetadataAttribute;
