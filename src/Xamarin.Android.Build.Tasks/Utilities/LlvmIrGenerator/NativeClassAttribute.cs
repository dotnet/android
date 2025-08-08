#nullable enable
using System;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Attribute that marks a class as representing a native structure for LLVM IR generation.
	/// Classes decorated with this attribute will be treated as native structures during code generation.
	/// </summary>
	[AttributeUsage (AttributeTargets.Class, Inherited = true)]
	class NativeClassAttribute : Attribute
	{
	}
}
