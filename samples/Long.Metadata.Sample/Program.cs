using Long.Metadata;

var ignoredProperties = GeneratedMetadata.GetProperties<IgnorePropertyAttribute>();

foreach (var property in ignoredProperties)
{
    Console.WriteLine($"{property.DeclaringTypeDisplayName}.{property.PropertyName}: {property.PropertyTypeDisplayName}");
}

var accountMetadata = GeneratedMetadata.GetProperties<AuditedPropertyAttribute>().Single();
var user = new User { Account = new Account("active") };
var status = accountMetadata.Invoke(user, "GetStatus");

Console.WriteLine($"{accountMetadata.PropertyName}.GetStatus(): {status}");

public sealed class IgnorePropertyAttribute : GeneratedMetadataAttribute
{
}

public sealed class AuditedPropertyAttribute : GeneratedMetadataAttribute
{
}

public sealed class User
{
    public string UserName { get; set; } = "";

    [IgnoreProperty]
    public string Password { get; set; } = "";

    [AuditedProperty]
    public Account Account { get; set; } = new Account("new");
}

public sealed class Account(string status)
{
    public string GetStatus()
    {
        return status;
    }
}
