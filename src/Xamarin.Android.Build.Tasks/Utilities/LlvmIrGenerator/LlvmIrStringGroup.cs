using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Represents a group of string variables that will be emitted together in the LLVM IR output.
/// String groups allow organizing related strings with optional comments for better code organization.
/// </summary>
sealed class LlvmIrStringGroup
{
	/// <summary>
	/// Gets the number of strings in this group.
	/// </summary>
	public ulong Count;
	/// <summary>
	/// Gets the optional comment associated with this string group.
	/// </summary>
	public readonly string? Comment;
	/// <summary>
	/// Gets the list of string variables in this group.
	/// </summary>
	public readonly List<LlvmIrStringVariable> Strings = new List<LlvmIrStringVariable> ();

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrStringGroup"/> class.
	/// </summary>
	/// <param name="comment">Optional comment to associate with this string group.</param>
	public LlvmIrStringGroup (string? comment = null)
	{
		Comment = comment;
		Count = 0;
	}
}
