using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Function calling convention, see https://llvm.org/docs/LangRef.html#callingconv for more detailed docs.
/// Not all conventions are included in this enumeration, only those we may potentially need.
/// </summary>
enum LlvmIrCallingConvention
{
	/// <summary>
	/// Outputs no keyword, making function use whatever is the compiler's default calling convention.
	/// </summary>
	Default,

	/// <summary>
	/// The C calling convention (`ccc`)
	/// </summary>
	Ccc,

	/// <summary>
	/// The fast calling convention (`fastcc`). This calling convention attempts to make calls as fast
	/// as possible (e.g. by passing things in registers).
	/// </summary>
	Fastcc,

	/// <summary>
	/// Tail callable calling convention (`tailcc`). This calling convention ensures that calls in tail
	/// position will always be tail call optimized. This calling convention is equivalent to fastcc,
	/// except for an additional guarantee that tail calls will be produced whenever possible.
	/// </summary>
	Tailcc,
}
