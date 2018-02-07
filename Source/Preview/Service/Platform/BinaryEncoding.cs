using System;
using System.Collections.Generic;
using System.IO;
using Outracks.Simulator;
using Uno;
using Uno.Collections;
using StringSplitting = Outracks.StringSplitting;

namespace Fuse.Preview
{
	static class BinaryEncoding
	{
		public static void WriteArray<T>(this BinaryWriter streamWriter, IEnumerable<T> list, Action<BinaryWriter, T> write)
		{
			var array = list.ToArray();
			streamWriter.Write(array.Length);
			foreach (var i in array)
			{
				write(streamWriter, i);
			}
		}

		public static void WriteTaggedValue(this BinaryWriter writer, object value)
		{
			WriteValue(writer, value, tagType: writer.Write);
		}

		public static void WriteValue(this BinaryWriter writer, object value, Action<string> tagType = null)
		{
			tagType = tagType ?? (typeTag => { });

			if (value == null)
			{
				tagType("");
				return;
			}

			var valueType = value.GetType();
			if (valueType.IsArray)
			{
				tagType(valueType.GetElementType().FullName + "[]");

				var array = ((Array) value);
				writer.Write(array.Length);
				for (int i = 0; i < array.Length; i++)
					writer.WriteValue(array.GetValue(i));

				return;
			}

			tagType(valueType.FullName);

			
			if (value is int) writer.Write((int)value);
			else if (value is bool) writer.Write((bool)value);
			else if (value is string) writer.Write((string)value);
			else if (value is Guid) writer.Write(((Guid)value).ToByteArray());
			else if (value is SourceReference) SourceReference.Write(writer, ((SourceReference)value));
			else if (value is ObjectIdentifier) ((ObjectIdentifier)value).Write(writer);
			else throw new NotSupportedException("Unsopported argument type: " + value.GetType());
		}

		public static object ReadTaggedValue(this BinaryReader reader)
		{
			return reader.ReadValue(typeTag: reader.ReadString());
		}

		public static object ReadValue(this BinaryReader reader, string typeTag)
		{
			if (string.IsNullOrEmpty(typeTag)) return null;
			if (typeTag == typeof(int).FullName) return reader.ReadInt32();
			if (typeTag == typeof(bool).FullName) return reader.ReadBoolean();
			if (typeTag == typeof(string).FullName) return reader.ReadString();
			if (typeTag == typeof(Guid).FullName) return new Guid(reader.ReadBytes(16));
			if (typeTag == typeof(SourceReference).FullName) return SourceReference.Read(reader);
			if (typeTag == typeof(ObjectIdentifier).FullName) return ObjectIdentifier.Read(reader);

			if (typeTag.EndsWith("[]"))
			{
				var elementTypeTag = StringSplitting.BeforeLast(typeTag, "[]");
				var elementType = GetTypeFromTag(elementTypeTag);
				var array = Array.CreateInstance(elementType, reader.ReadInt32());
				for (int i = 0; i < array.Length; i++)
					array.SetValue(ReadValue(reader, elementTypeTag), i);
				return array;
			}
			throw new NotSupportedException("Unsupported parameter type: " + typeTag);
		}

		static Type GetTypeFromTag(string typeTag)
		{
			if (string.IsNullOrEmpty(typeTag)) return typeof(object);
			if (typeTag == typeof(int).FullName) return typeof(int);
			if (typeTag == typeof(bool).FullName) return typeof(bool);
			if (typeTag == typeof(string).FullName) return typeof(string);
			if (typeTag == typeof(Guid).FullName) return typeof(Guid);
			if (typeTag == typeof(SourceReference).FullName) return typeof(SourceReference);
			if (typeTag == typeof(ObjectIdentifier).FullName) return typeof(ObjectIdentifier);
			throw new NotSupportedException("Unsupported parameter type: " + typeTag);
		}
	}
}