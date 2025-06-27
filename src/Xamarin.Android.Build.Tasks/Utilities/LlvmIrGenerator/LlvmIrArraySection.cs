using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVMIR;

abstract class LlvmIrArraySectionBase
{
	public Type DataType     { get; }
	public List<object> Data { get; } = [];
	public string? Header    { get; }

	protected LlvmIrArraySectionBase (Type type, string? header = null)
	{
		DataType = type;
		Header = header;
	}

	protected void Add (object data) => Data.Add (data);
}

class LlvmIrArraySection<T> : LlvmIrArraySectionBase
{
	public LlvmIrArraySection (string? header = null)
		: base (typeof(T), header)
	{}

	public void Add (T data) => base.Add (data!);
}
