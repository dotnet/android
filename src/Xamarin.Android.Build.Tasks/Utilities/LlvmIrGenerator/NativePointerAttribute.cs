using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	[AttributeUsage (AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
	class NativePointerAttribute : Attribute
	{
		public bool PointsToPreAllocatedBuffer { get; set; }
		public string? PointsToSymbol { get; set; }
	}
}
