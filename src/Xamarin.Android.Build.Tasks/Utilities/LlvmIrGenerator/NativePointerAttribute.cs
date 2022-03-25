using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativePointerAttribute : Attribute
	{
		/// <summary>
		/// Indicates that the member points to a buffer pre-allocated at the compilation time, with size
		/// provided by <see cref="NativeAssemblerStructContextDataProvider.GetBufferSize"/> or, if present,
		/// by the <see cref="PreAllocatedBufferSize"/> property.
		/// </summary>
		public bool PointsToPreAllocatedBuffer { get; set; }

		/// <summary>
		/// Indicates that the member points to a buffer pre-allocated at the compilation time, with size
		/// specified by the value of this property, unless the value is <c>0</c> in which case
		/// <see cref="NativeAssemblerStructContextDataProvider.GetBufferSize"/> is used.
		/// </summary>
		public ulong PreAllocatedBufferSize { get; set; } = 0;

		/// <summary>
		/// Indicates the symbol to point to. If <c>null</c> the attribute is ignored, if <c>String.Empty</c>
		/// the data context provider is queried (if the type uses it)
		/// </summary>
		public string? PointsToSymbol { get; set; }

		/// <summary>
		/// A shortcut way to initialize a pointer to <c>null</c> without having to involve context data provider
		/// </summary>
		public bool IsNull { get; set; }
	}
}
