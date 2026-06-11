using Long.Metadata;

var ignoredProperties = GeneratedMetadata.GetProperties<IgnorePropertyAttribute>();

foreach (var property in ignoredProperties)
{
    Console.WriteLine($"{property.DeclaringTypeDisplayName}.{property.PropertyName}: {property.PropertyTypeDisplayName}");
}

public sealed class IgnorePropertyAttribute : GeneratedMetadataAttribute
{
}

public sealed class User
{
    public string UserName { get; set; } = "";

    [IgnoreProperty]
    public string Password { get; set; } = "";
}
