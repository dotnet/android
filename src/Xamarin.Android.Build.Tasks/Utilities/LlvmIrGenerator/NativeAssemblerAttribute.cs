using System;

namespace Xamarin.Android.Tasks
{
	enum NativeAssemblerValidTarget
	{
		Any,
		ThirtyTwoBit,
		SixtyFourBit,
	}

	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativeAssemblerAttribute : Attribute
	{
		/// <summary>
		/// Tells the generator to ignore structure/class member if set to <c>true</c>
		/// </summary>
		public bool Ignore           { get; set; }

		/// <summary>
		/// Indicates that structure/class member uses a data context provider to obtain some data dynamically,
		/// based on instances of the given structure/class. <see cref="NativeAssemblerStructContextDataProvider"/>
		/// </summary>
		public bool UsesDataProvider { get; set; }

		/// <summary>
		/// Indicates that the member is an array contained directly in the structure, as opposed to a pointer to array.
		/// Size of the array is either specified statically in <see cref="InlineArraySize"/> or by the data context
		/// provider's <see cref="NativeAssemblerStructContextDataProvider.GetBufferSize"/> method.
		/// </summary>
		public bool InlineArray      { get; set; }

		/// <summary>
		/// Size of <see cref="InlineArray"/>, if set to a positive value.
		/// </summary>
		public int  InlineArraySize  { get; set; } = -1;

		/// <summary>
		/// Indicates that the <see cref="InlineArray"/> member needs to be padded to a certain size.  Maximum array
		/// size to which the member must be padded is specified by <see cref="NativeAssemblerStructContextDataProvider.GetMaxInlineWidth"/>
		/// </summary>
		public bool NeedsPadding     { get; set; }

		public LLVMIR.LlvmIrVariableNumberFormat NumberFormat { get; set; } = LLVMIR.LlvmIrVariableNumberFormat.Default;

		/// <summary>
		/// Taken into account only for fields of string types.  If set to <c>true</c>, the string is output as an UTF16 in
		/// the native assembly file.
		/// </summary>
		public bool IsUTF16 { get; set; }

		/// <summary>
		/// Allows choosing that a structure/class field/property will be considered only for the target of
		/// specific bitness. Mainly useful when dealing with hash fields.
		/// </summary>
		public NativeAssemblerValidTarget ValidTarget { get; set; } = NativeAssemblerValidTarget.Any;

		/// <summary>
		/// Allows overriding of the field name when mapping the structure. This is purely cosmetic, but allows to avoid
		/// confusion when dealing with fields/properties that have a different size for 32-bit and 64-bit targets. In such
		/// case the structure being mapped will have a separate member per bitness, with the members requiring different names
		/// while in reality they have the same name in the native world, regardless of bitness.
		///
		/// This field is only considered only when <see cref="ValidTarget" /> is not set to <see cref="NativeAssemblerValidTarget.Any"/>
		/// </summary>
		public string? MemberName { get; set; }
	}

	[AttributeUsage (AttributeTargets.Class, Inherited = true)]
	class NativeAssemblerStructContextDataProviderAttribute : Attribute
	{
		public Type Type { get; }

		public NativeAssemblerStructContextDataProviderAttribute (Type type)
		{
			if (type != null && !type.IsSubclassOf (typeof(NativeAssemblerStructContextDataProvider))) {
				throw new ArgumentException (nameof (type), "Must be derived from the AssemblerStructContextDataProvider class");
			}

			Type = type;
		}
	}
}
