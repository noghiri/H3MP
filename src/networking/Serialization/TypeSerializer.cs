using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace H3MP.Networking.Serialization;

#nullable enable

public abstract class TypeSerializer
{
    private static readonly Dictionary<Type, TypeSerializer> Serializers = new();

    internal static void SearchAssemblies()
    {
        IEnumerable<Type?> allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
        });

        foreach (Type? type in allTypes)
        {
            if (type == null) continue;
            object[] attributes = type.GetCustomAttributes(typeof(MeatLinkSerializerAttribute), false);
            if (attributes.Length == 0) continue;
            MeatLinkSerializerAttribute attribute = (MeatLinkSerializerAttribute) attributes[0];
            var serializer = (TypeSerializer) Activator.CreateInstance(type)!;
            Serializers.Add(attribute.SerializedType, serializer);
        }
    }

    internal static TypeSerializer<T> Get<T>()
    {
        if (!Serializers.TryGetValue(typeof(T), out TypeSerializer? serializer))
            throw new Exception($"Serializer for the type {typeof(T)} not found.");
        return (TypeSerializer<T>)serializer;
    }
}

public abstract class TypeSerializer<T> : TypeSerializer
{
    public abstract void Serialize(in T value, ref SerializationWriter writer);

    public abstract void Deserialize(out T value, ref SerializationReader reader);
}

public class MeatLinkSerializerAttribute : Attribute
{
    public Type SerializedType { get; }

    public MeatLinkSerializerAttribute(Type serializedType)
    {
        SerializedType = serializedType;
    }
}
