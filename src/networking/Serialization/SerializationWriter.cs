using System;
using System.Text;

namespace H3MP.Networking.Serialization;

#nullable enable

public struct SerializationWriter
{
    private readonly byte[] _buffer;
    public int Offset { get; private set; }

    public SerializationWriter(byte[] buffer, int offset)
    {
        _buffer = buffer;
        Offset = offset;
    }

    public unsafe void WriteNative<T>(in T value) where T : unmanaged
    {
        fixed (byte* p = &_buffer[Offset]) *(T*)p = value;
        Offset += sizeof(T);
    }

    public unsafe void WriteNative<T>(in T value, int position) where T : unmanaged
    {
        fixed (byte* p = &_buffer[position]) *(T*)p = value;
    }

    public void WriteNative<T>(in T[] values) where T : unmanaged
    {
        WriteNative((byte)values.Length);
        foreach (T value in values) WriteNative(value);
    }

    public void WriteComplex<T>(in T value, TypeSerializer<T>? serializer = null)
    {
        serializer ??= TypeSerializer.Get<T>();
        serializer.Serialize(value, ref this);
    }

    public void WriteComplex<T>(in T[] values, TypeSerializer<T>? serializer = null)
    {
        serializer ??= TypeSerializer.Get<T>();
        WriteNative((byte)values.Length);
        foreach (T value in values) WriteComplex(value, serializer);
    }

    public void WriteString(in string value, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        ushort length = (ushort)encoding.GetBytes(value, 0, value.Length, _buffer, Offset + sizeof(ushort));
        WriteNative(length);
        Offset += length;
    }

    public void WriteString(in string[] values, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        WriteNative((byte)values.Length);
        foreach (string value in values) WriteString(value, encoding);
    }

    public void WriteBytes(in byte[] values)
    {
        WriteNative((ushort)values.Length);
        Array.Copy(values, 0, _buffer, Offset, values.Length);
        Offset += values.Length;
    }
}
