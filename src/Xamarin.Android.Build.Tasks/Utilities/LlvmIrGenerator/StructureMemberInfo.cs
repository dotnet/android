using System;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	sealed class StructureMemberInfo
	{
		public string IRType        { get; }
		public MemberInfo Info      { get; }
		public Type MemberType      { get; }

		/// <summary>
		/// Size of a variable with this IR type. May differ from <see cref="BaseTypeSize"/> because the field
		/// can be a pointer to type or a struct
		/// </summary>
		public ulong Size           { get; }
		public ulong Alignment      { get; }
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

		public StructureMemberInfo (MemberInfo mi, LlvmIrModule module, LlvmIrTypeCache cache)
		{
			Info = mi;

			MemberType = mi switch {
				FieldInfo fi => fi.FieldType,
				PropertyInfo pi => pi.PropertyType,
				_ => throw new InvalidOperationException ($"Unsupported member type {mi}")
			};

			ulong size = 0;
			bool isPointer = false;
			if (MemberType != typeof(string) && !MemberType.IsArray && (MemberType.IsStructure () || MemberType.IsClass)) {
				IRType = $"%struct.{MemberType.GetShortName ()}";
				// TODO: figure out how to get structure size if it isn't a pointer
			} else {
				IRType = LlvmIrGenerator.MapToIRType (MemberType, cache, out size, out isPointer);
			}
			IsNativePointer = isPointer;

			if (!IsNativePointer) {
				IsNativePointer = mi.IsNativePointer (cache);
				if (IsNativePointer) {
					IRType = LlvmIrGenerator.IRPointerType;
				}
			}

			BaseTypeSize = size;
			ArrayElements = 0;
			IsInlineArray = false;
			NeedsPadding = false;
			Alignment = 0;

			if (IsNativePointer) {
				size = 0; // Real size will be determined when code is generated and we know the target architecture
			} else if (mi.IsInlineArray (cache)) {
				if (!MemberType.IsArray) {
					throw new InvalidOperationException ($"Internal error: member {mi.Name} of structure {mi.DeclaringType.Name} is marked as inline array, but is not of an array type.");
				}

				IsInlineArray = true;
				IsNativeArray = true;
				NeedsPadding = mi.InlineArrayNeedsPadding (cache);
				int arrayElements = mi.GetInlineArraySize (cache);
				if (arrayElements < 0) {
					arrayElements = GetArraySizeFromProvider (MemberType.GetDataProvider (), mi.Name);
				}

				if (arrayElements < 0) {
					throw new InvalidOperationException ($"Array cannot have negative size (got {arrayElements})");
				}

				IRType = $"[{arrayElements} x {IRType}]";
				ArrayElements = (ulong)arrayElements;
			} else if (this.IsIRStruct (cache)) {
				StructureInfo si = module.GetStructureInfo (MemberType);
				size = si.Size;
				Alignment = (ulong)si.MaxFieldAlignment;
			}

			if (MemberType.IsArray && !IsInlineArray) {
				throw new InvalidOperationException ("Out of line arrays in structures aren't currently supported");
			}

			Size = size;
			if (Alignment == 0) {
				Alignment = size;
			}
		}

		public object? GetValue (object instance)
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
