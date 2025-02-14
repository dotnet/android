using System;

namespace Xamarin.Android.Tasks.LLVMIR;

class StringHolder : IComparable, IComparable<StringHolder>, IEquatable<StringHolder>
{
	public LlvmIrStringEncoding Encoding { get; }
	public string? Data { get; }

	StringComparison comparison;

	public StringHolder (string? data, LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
	{
		Data = data;
		Encoding = encoding;
		this.comparison = comparison;
	}

	public static StringHolder AsHolder (object? value, LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
	{
		if (value == null) {
			return new StringHolder ((string)value);
		}

		StringHolder holder;
		if (value is string) {
			holder = new StringHolder ((string)value, encoding, comparison);
		} else if (value is StringHolder) {
			holder = (StringHolder)value;
		} else {
			throw new InvalidOperationException ($"Internal error: expected 'string' type, got '{value.GetType ()}' instead.");
		}

		return holder;
	}

	public int CompareTo (object obj) => CompareTo (obj as StringHolder);

	public int CompareTo (StringHolder? other)
	{
		if (other == null) {
			return 1;
		}

		int encodingCompare = Encoding.CompareTo (other.Encoding);
		if (Data == null) {
			if (other.Data != null) {
				// We are "smaller", because the other holder actually has a valid string
				return -1;
			}

			// Both strings are null, so we care only about the encoding
			return encodingCompare;
		}

		int dataCompare = other.Data == null ? Data.CompareTo (other.Data) : String.Compare (Data, other.Data, comparison);
		// If encodings are identical, we compare strings, allowing any result of the comparisons
		if (encodingCompare == 0) {
			return dataCompare;
		}

		// However if encodings aren't the same, we mustn't allow Data comparison to return 0
		// as the strings are **not** equal, even though their Data values are.  In this case,
		// we fall back to encoding for comparison result.
		return dataCompare != 0 ? dataCompare : encodingCompare;
	}

	public override int GetHashCode ()
	{
		int hc = 0;
		if (Data != null) {
			hc ^= Data.GetHashCode ();
		}

		return hc ^ Encoding.GetHashCode ();
	}

	public override bool Equals (object obj) => Equals (obj as StringHolder);

	public bool Equals (StringHolder? other)
	{
		if (other == null || Encoding != other.Encoding) {
			return false;
		}

		return String.Compare (Data, other.Data, comparison) == 0;
	}

	public static bool operator > (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) > 0;
	}

	public static bool operator < (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) < 0;
	}

	public static bool operator >= (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) >= 0;
	}

	public static bool operator <= (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) <= 0;
	}
}
