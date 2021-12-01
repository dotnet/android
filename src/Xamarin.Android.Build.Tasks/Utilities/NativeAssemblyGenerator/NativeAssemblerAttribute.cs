using System;

namespace Xamarin.Android.Tasks
{
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativeAssemblerAttribute : Attribute
	{
		public bool Ignore			 { get; set; }
		public bool UsesDataProvider { get; set; }
		public string? Comment		 { get; set; }
		public string? Name			 { get; set; }
	}

	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativeAssemblerStringAttribute : NativeAssemblerAttribute
	{
		public bool Inline				 { get; set; }
		public bool PadToMaxLength       { get; set; }
		public bool PointerToSymbol	     { get; set; }
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
