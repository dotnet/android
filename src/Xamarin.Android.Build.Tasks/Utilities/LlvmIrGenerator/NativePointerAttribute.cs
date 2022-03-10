using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativePointerAttribute : Attribute
	{
		public bool PointsToPreAllocatedBuffer { get; set; }
		public ulong PreAllocatedBufferSize { get; set; } = 0;
		public string? PointsToSymbol { get; set; }
	}
}
