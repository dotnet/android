using System;

namespace Xamarin.Android.Tasks.LLVMIR;

class LlvmIrArraySection
{
	public Type DataType  { get; }
	public object? Data   { get; }
	public string? Header { get; }

	public LlvmIrArraySection (Type type, object? data, string? header = null)
	{
		DataType = type;
		Data = data;
		Header = header;
	}

	public LlvmIrArraySection (object? data, string? header = null)
		: this ((data ?? throw new ArgumentNullException (nameof (data))).GetType (), data, header)
	{}
}
