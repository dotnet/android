using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Base class for array sections used in sectioned arrays. Provides common functionality
/// for organizing array data into named sections with optional headers.
/// </summary>
abstract class LlvmIrArraySectionBase
{
	/// <summary>
	/// Gets the type of data stored in this array section.
	/// </summary>
	public Type DataType     { get; }
	/// <summary>
	/// Gets the list of data objects stored in this section.
	/// </summary>
	public List<object> Data { get; } = [];
	/// <summary>
	/// Gets the optional header comment for this section.
	/// </summary>
	public string? Header    { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrArraySectionBase"/> class.
	/// </summary>
	/// <param name="type">The type of data that will be stored in this section.</param>
	/// <param name="header">Optional header comment for the section.</param>
	protected LlvmIrArraySectionBase (Type type, string? header = null)
	{
		DataType = type;
		Header = header;
	}

	/// <summary>
	/// Adds a data object to this section.
	/// </summary>
	/// <param name="data">The data object to add to the section.</param>
	protected void Add (object data) => Data.Add (data);
}

/// <summary>
/// Represents a typed array section that stores elements of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of elements stored in this array section.</typeparam>
class LlvmIrArraySection<T> : LlvmIrArraySectionBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LlvmIrArraySection{T}"/> class.
	/// </summary>
	/// <param name="header">Optional header comment for the section.</param>
	public LlvmIrArraySection (string? header = null)
		: base (typeof(T), header)
	{}

	/// <summary>
	/// Adds a typed data element to this section.
	/// </summary>
	/// <param name="data">The data element to add to the section.</param>
	public void Add (T data) => base.Add (data!);
}
