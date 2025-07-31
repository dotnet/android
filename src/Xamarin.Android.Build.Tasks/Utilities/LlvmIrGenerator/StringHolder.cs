using System;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Holds a string with its encoding information and comparison semantics for use in LLVM IR generation.
/// </summary>
class StringHolder : IComparable, IComparable<StringHolder>, IEquatable<StringHolder>
{
	/// <summary>
	/// Gets the encoding used for this string.
	/// </summary>
	public LlvmIrStringEncoding Encoding { get; }
	/// <summary>
	/// Gets the string data.
	/// </summary>
	public string? Data { get; }

	StringComparison comparison;

	/// <summary>
	/// Initializes a new instance of the <see cref="StringHolder"/> class.
	/// </summary>
	/// <param name="data">The string data to hold.</param>
	/// <param name="encoding">The encoding to use for the string.</param>
	/// <param name="comparison">The string comparison method to use.</param>
	public StringHolder (string? data, LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
	{
		Data = data;
		Encoding = encoding;
		this.comparison = comparison;
	}

	/// <summary>
	/// Converts an object to a StringHolder, handling both string and StringHolder inputs.
	/// </summary>
	/// <param name="value">The value to convert (string or StringHolder).</param>
	/// <param name="encoding">The encoding to use if creating a new StringHolder.</param>
	/// <param name="comparison">The comparison method to use if creating a new StringHolder.</param>
	/// <returns>A StringHolder containing the value.</returns>
	/// <exception cref="InvalidOperationException">Thrown when value is not a string or StringHolder.</exception>
	public static StringHolder AsHolder (object? value, LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8, StringComparison comparison = StringComparison.Ordinal)
	{
		if (value == null) {
			return new StringHolder ((string?)value);
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

	/// <summary>
	/// Compares this StringHolder to an object.
	/// </summary>
	/// <param name="obj">The object to compare to.</param>
	/// <returns>A signed integer that indicates the relative values of this instance and value.</returns>
	public int CompareTo (object obj) => CompareTo (obj as StringHolder);

	/// <summary>
	/// Compares this StringHolder to another StringHolder.
	/// </summary>
	/// <param name="other">The StringHolder to compare to.</param>
	/// <returns>A signed integer that indicates the relative values of this instance and other.</returns>
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

	/// <summary>
	/// Returns a hash code for this StringHolder.
	/// </summary>
	/// <returns>A hash code for the current object.</returns>
	public override int GetHashCode ()
	{
		int hc = 0;
		if (Data != null) {
			hc ^= Data.GetHashCode ();
		}

		return hc ^ Encoding.GetHashCode ();
	}

	/// <summary>
	/// Determines whether the specified object is equal to the current StringHolder.
	/// </summary>
	/// <param name="obj">The object to compare with the current StringHolder.</param>
	/// <returns>true if the specified object is equal to the current StringHolder; otherwise, false.</returns>
	public override bool Equals (object obj) => Equals (obj as StringHolder);

	/// <summary>
	/// Determines whether the specified StringHolder is equal to the current StringHolder.
	/// </summary>
	/// <param name="other">The StringHolder to compare with the current StringHolder.</param>
	/// <returns>true if the specified StringHolder is equal to the current StringHolder; otherwise, false.</returns>
	public bool Equals (StringHolder? other)
	{
		if (other == null || Encoding != other.Encoding) {
			return false;
		}

		return MonoAndroidHelper.StringEquals (Data, other.Data, comparison);
	}

	/// <summary>
	/// Determines whether one StringHolder is greater than another.
	/// </summary>
	/// <param name="a">The first StringHolder to compare.</param>
	/// <param name="b">The second StringHolder to compare.</param>
	/// <returns>true if a is greater than b; otherwise, false.</returns>
	public static bool operator > (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) > 0;
	}

	/// <summary>
	/// Determines whether one StringHolder is less than another.
	/// </summary>
	/// <param name="a">The first StringHolder to compare.</param>
	/// <param name="b">The second StringHolder to compare.</param>
	/// <returns>true if a is less than b; otherwise, false.</returns>
	public static bool operator < (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) < 0;
	}

	/// <summary>
	/// Determines whether one StringHolder is greater than or equal to another.
	/// </summary>
	/// <param name="a">The first StringHolder to compare.</param>
	/// <param name="b">The second StringHolder to compare.</param>
	/// <returns>true if a is greater than or equal to b; otherwise, false.</returns>
	public static bool operator >= (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) >= 0;
	}

	/// <summary>
	/// Determines whether one StringHolder is less than or equal to another.
	/// </summary>
	/// <param name="a">The first StringHolder to compare.</param>
	/// <param name="b">The second StringHolder to compare.</param>
	/// <returns>true if a is less than or equal to b; otherwise, false.</returns>
	public static bool operator <= (StringHolder a, StringHolder b)
	{
		return a.CompareTo (b) <= 0;
	}
}
