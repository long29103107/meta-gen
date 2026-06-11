namespace Long.Metadata;

/// <summary>
/// Base attribute for metadata discovered by the Long.Metadata source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public abstract class GeneratedMetadataAttribute : Attribute
{
}
