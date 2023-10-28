using System;
using System.Text;

namespace H3MP.Networking.Serialization;

#nullable enable

public struct SerializationReader
{
    private readonly byte[] _buffer;
    public int Offset;

    public SerializationReader(byte[] buffer, int offset)
    {
        _buffer = buffer;
        Offset = offset;
    }
    
    public unsafe void ReadNative<T>(out T value) where T : unmanaged
    {
        fixed (byte* p = &_buffer[Offset]) value = *(T*)p;
        Offset += sizeof(T);
    }
    
    public void ReadNative<T>(out T[] values) where T : unmanaged
    {
        ReadNative(out byte length);
        values = new T[length];

        for (int i = 0; i < length; i++)
        {
            ReadNative(out values[i]);
        }
    }
    
    public void ReadComplex<T>(out T value, TypeSerializer<T>? serializer = null)
    {
        serializer ??= TypeSerializer.Get<T>();
        serializer.Deserialize(out value, ref this);
    }

    public void ReadComplex<T>(out T[] values, TypeSerializer<T>? serializer = null)
    {
        serializer ??= TypeSerializer.Get<T>();
        
        ReadNative(out byte length);
        values = new T[length];

        for (int i = 0; i < length; i++)
        {
            ReadComplex(out values[i], serializer!);
        }
    }
    
    public void ReadString(out string value, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        ReadNative(out ushort length);
        value = encoding.GetString(_buffer, Offset, length);
        Offset += length;
    }
    
    public void ReadString(out string[] values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        ReadNative(out byte length);
        values = new string[length];

        for (int i = 0; i < length; i++)
        {
            ReadString(out values[i], encoding);
        }
    }

    public void ReadBytes(out byte[] value)
    {
        ReadNative(out ushort length);
        value = new byte[length];
        Array.Copy(_buffer, Offset, value, 0, length);
        Offset += length;
    }
}
