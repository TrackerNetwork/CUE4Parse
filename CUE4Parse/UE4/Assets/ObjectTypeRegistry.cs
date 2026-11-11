using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CUE4Parse.UE4.Assets.Exports;

namespace CUE4Parse.UE4.Assets;

public sealed class SkipObjectRegistrationAttribute : Attribute;

public static class ObjectTypeRegistry
{
    // Classes are constructed with Activator.CreateInstance (UClass.ConstructObject); the annotations keep each
    // registered type's parameterless constructor when the consumer is trimmed or compiled with Native AOT.
    private const DynamicallyAccessedMemberTypes Constructed = DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    private static readonly Type _propertyHolderType = typeof(IPropertyHolder);
    private static readonly Dictionary<string, Type> _classes = [];

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CUE4Parse's own types; consumers that trim root the CUE4Parse assembly.")]
    static ObjectTypeRegistry()
    {
        RegisterEngine(_propertyHolderType.Assembly);
    }

    [RequiresUnreferencedCode("Registers every UObject type the assembly defines; trimming may remove them or their constructors. Root the assembly or use RegisterClass.")]
    public static void RegisterEngine(Assembly assembly)
    {
        var skipAttributeType = typeof(SkipObjectRegistrationAttribute);

        foreach (var definedType in assembly.DefinedTypes)
        {
            if (definedType.IsAbstract ||
                definedType.IsInterface ||
                !_propertyHolderType.IsAssignableFrom(definedType))
            {
                continue;
            }

            if (definedType.GetCustomAttributes(skipAttributeType, false).Length != 0)
            {
                continue;
            }

            RegisterClass(definedType);
        }
    }

    public static void RegisterClass([DynamicallyAccessedMembers(Constructed)] Type type)
    {
        var name = type.Name;
        if ((name[0] == 'U' || name[0] == 'A') && char.IsUpper(name[1]))
            name = name[1..];
        RegisterClass(name, type);
    }

    public static void RegisterClass(string serializedName, [DynamicallyAccessedMembers(Constructed)] Type type)
    {
        lock (_classes)
        {
            _classes[serializedName] = type;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "Only RegisterClass adds types, and it requires Constructed.")]
    [return: DynamicallyAccessedMembers(Constructed)]
    public static Type? GetClass(string serializedName)
    {
        lock (_classes)
        {
            if (!_classes.TryGetValue(serializedName, out var type) && serializedName.EndsWith("_C"))
            {
                _classes.TryGetValue(serializedName[..^2], out type);
            }
            return type;
        }
    }

    [return: DynamicallyAccessedMembers(Constructed)]
    public static Type? Get(string serializedName)
    {
        return GetClass(serializedName);
        // TODO add script structs
    }
}
