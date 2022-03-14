using System;

namespace Xamarin.Android.Tasks
{
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativeAssemblerAttribute : Attribute
	{
		public bool Ignore           { get; set; }
		public bool UsesDataProvider { get; set; }
		public bool InlineArray      { get; set; }
		public int  InlineArraySize  { get; set; } = -1;
		public string? Comment       { get; set; }
		public string? Name          { get; set; }
	}

	enum AssemblerStringFormat
	{
		/// <summary>
		/// Local string label is automatically generated for the string, this is the default behavior
		/// </summary>
		Automatic,

		/// <summary>
		/// String is output as an inline (no pointer) array/buffer filled directly with string data
		/// </summary>
		InlineArray,

		/// <summary>
		/// String is output as a pointer to symbol whose name is contents of the string being processed
		PointerToSymbol,
	}

	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativeAssemblerStringAttribute : NativeAssemblerAttribute
	{
		AssemblerStringFormat format;

		public bool Inline          => format == AssemblerStringFormat.InlineArray;
		public bool PadToMaxLength  { get; set; }
		public bool PointerToSymbol => format == AssemblerStringFormat.PointerToSymbol;

		public NativeAssemblerStringAttribute ()
		{
			format = AssemblerStringFormat.Automatic;
		}

		public NativeAssemblerStringAttribute (AssemblerStringFormat format)
		{
			this.format = format;
		}
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
