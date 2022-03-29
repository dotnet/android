using System;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class StructureMemberInfo<T>
	{
		public string IRType        { get; }
		public MemberInfo Info      { get; }
		public Type MemberType      { get; }

		/// <summary>
		/// Size of a variable with this IR type. May differ from <see cref="BaseTypeSize"/> because the field
		/// can be a pointer to type
		/// </summary>
		public ulong Size           { get; }

		public ulong ArrayElements  { get; }

		/// <summary>
		/// Size of the member's base IR type.  If the variable is a pointer, this property will represent
		/// the size of a base type, not the pointer.
		/// </summary>
		public ulong BaseTypeSize   { get; }
		public bool IsNativePointer { get; }
		public bool IsNativeArray   { get; }
		public bool IsInlineArray   { get; }
		public bool NeedsPadding    { get; }

		public StructureMemberInfo (MemberInfo mi, LlvmIrGenerator generator)
		{
			Info = mi;

			MemberType = mi switch {
				FieldInfo fi => fi.FieldType,
				PropertyInfo pi => pi.PropertyType,
				_ => throw new InvalidOperationException ($"Unsupported member type {mi}")
			};

			ulong size = 0;
			if (MemberType != typeof(string) && !MemberType.IsArray && (MemberType.IsStructure () || MemberType.IsClass)) {
				IRType = $"%struct.{MemberType.GetShortName ()}";
				// TODO: figure out how to get structure size if it isn't a pointer
			} else {
				IRType = generator.MapManagedTypeToIR (MemberType, out size);
			}
			IsNativePointer = IRType[IRType.Length - 1] == '*';

			if (!IsNativePointer) {
				IsNativePointer = mi.IsNativePointer ();
				if (IsNativePointer) {
					IRType += "*";
				}
			}

			BaseTypeSize = size;
			ArrayElements = 0;
			IsInlineArray = false;
			NeedsPadding = false;

			if (IsNativePointer) {
				size = (ulong)generator.PointerSize;
			} else if (mi.IsInlineArray ()) {
				IsInlineArray = true;
				IsNativeArray = true;
				NeedsPadding = mi.InlineArrayNeedsPadding ();
				int arrayElements = mi.GetInlineArraySize ();
				if (arrayElements < 0) {
					arrayElements = GetArraySizeFromProvider (typeof(T).GetDataProvider (), mi.Name);
				}

				if (arrayElements < 0) {
					throw new InvalidOperationException ($"Array cannot have negative size (got {arrayElements})");
				}

				IRType = $"[{arrayElements} x {IRType}]";
				ArrayElements = (ulong)arrayElements;
			}

			if (MemberType.IsArray && !IsInlineArray) {
				throw new InvalidOperationException ("Out of line arrays in structures aren't currently supported");
			}

			Size = size;
		}

		public object? GetValue (T instance)
		{
			if (Info is FieldInfo fi) {
				return fi.GetValue (instance);
			}

			var pi = Info as PropertyInfo;
			return pi.GetValue (instance);
		}

		int GetArraySizeFromProvider (NativeAssemblerStructContextDataProvider? provider, string fieldName)
		{
			if (provider == null) {
				return -1;
			}

			return (int)provider.GetMaxInlineWidth (null, fieldName);
		}
	}
}
