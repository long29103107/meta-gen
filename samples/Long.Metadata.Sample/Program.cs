using Long.Metadata;

var ignoredProperties = GeneratedMetadata.GetProperties<IgnorePropertyAttribute>();

foreach (var property in ignoredProperties)
{
    Console.WriteLine($"{property.DeclaringTypeDisplayName}.{property.PropertyName}: {property.PropertyTypeDisplayName}");
}

var accountMetadata = GeneratedMetadata.GetProperties<AuditedPropertyAttribute>().Single();
var user = new User { Account = new Account("active") };
var status = accountMetadata.Invoke(user, nameof(Account.GetStatus));

Console.WriteLine($"{accountMetadata.PropertyName}.GetStatus(): {status}");

var serviceContracts = GeneratedMetadata.GetAllTypes()
    .Where(type => type.IsInterface || type.IsAbstract);

foreach (var contract in serviceContracts)
{
    Console.WriteLine($"{contract.TypeDisplayName}: {contract.Accessibility} {GetTypeKind(contract)}");
}

var userServices = GeneratedMetadata.GetAllTypes()
    .Where(type => !type.IsAbstract && !type.IsInterface && type.IsAssignableTo(typeof(IUserService)));

foreach (var service in userServices)
{
    Console.WriteLine($"{service.Accessibility} {GetTypeKind(service)} {service.TypeDisplayName} implements IUserService");
}

var productHandlers = GeneratedMetadata.GetAllTypes()
    .Where(type => !type.IsAbstract && !type.IsInterface && type.IsAssignableTo(typeof(BaseHandler)));

foreach (var handler in productHandlers)
{
    Console.WriteLine($"{handler.Accessibility} {GetTypeKind(handler)} {handler.TypeDisplayName} inherits BaseHandler");
}

static string GetTypeKind(GeneratedTypeMetadata type)
{
    if (type.IsInterface)
    {
        return "interface";
    }

    if (type.IsAbstract)
    {
        return "abstract";
    }

    return type.IsSealed ? "sealed class" : "class";
}

public sealed class IgnorePropertyAttribute : GeneratedMetadataAttribute
{
}

public sealed class AuditedPropertyAttribute : GeneratedMetadataAttribute
{
}

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
