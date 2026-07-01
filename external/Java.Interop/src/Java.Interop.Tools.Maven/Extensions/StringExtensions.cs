using System.Diagnostics.CodeAnalysis;

namespace Java.Interop.Tools.Maven.Extensions;

static class StringExtensions
{
	/// <summary>
	/// Shortcut for !string.IsNullOrWhiteSpace (s)
	/// </summary>
	public static bool HasValue ([NotNullWhen (true)] this string? s) => !string.IsNullOrWhiteSpace (s);

	/// <summary>
	/// Shortcut for s ?? string.Empty
	/// </summary>
	public static string OrEmpty (this string? str) => str ?? string.Empty;

	/// <summary>
	/// Removes the first subset of a delimited string. ("127.0.0.1" -> "0.0.1")
	/// </summary>
	[return: NotNullIfNotNull (nameof (s))]
	public static string? ChompFirst (this string? s, char separator)
	{
		if (!s.HasValue ())
			return s;

		var index = s.IndexOf (separator);

		if (index < 0)
			return string.Empty;

		return s.Substring (index + 1);
	}

	/// <summary>
	/// Removes the final subset of a delimited string. ("127.0.0.1" -> "127.0.0")
	/// </summary>
	[return: NotNullIfNotNull (nameof (s))]
	public static string? ChompLast (this string? s, char separator)
	{
		if (!s.HasValue ())
			return s;

		var index = s.LastIndexOf (separator);

		if (index < 0)
			return string.Empty;

		return s.Substring (0, index);
	}

	/// <summary>
	/// Returns the first subset of a delimited string. ("127.0.0.1" -> "127")
	/// </summary>
	[return: NotNullIfNotNull (nameof (s))]
	public static string? FirstSubset (this string? s, char separator)
	{
		if (!s.HasValue ())
			return s;

		var index = s.IndexOf (separator);

		if (index < 0)
			return s;

		return s.Substring (0, index);
	}

	/// <summary>
	/// Returns the final subset of a delimited string. ("127.0.0.1" -> "1")
	/// </summary>
	[return: NotNullIfNotNull (nameof (s))]
	public static string? LastSubset (this string? s, char separator)
	{
		if (!s.HasValue ())
			return s;

		var index = s.LastIndexOf (separator);

		if (index < 0)
			return s;

		return s.Substring (index + 1);
	}
}
