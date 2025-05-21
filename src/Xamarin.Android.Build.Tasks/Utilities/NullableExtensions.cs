using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Xamarin.Android.Tasks;

static class NullableExtensions
{
	// The static methods in System.String are not NRT annotated in netstandard2.0,
	// so we need to add our own extension methods to make them nullable aware.
	public static bool IsNullOrEmpty ([NotNullWhen (false)] this string? str)
	{
		return string.IsNullOrEmpty (str);
	}

	public static bool IsNullOrWhiteSpace ([NotNullWhen (false)] this string? str)
	{
		return string.IsNullOrWhiteSpace (str);
	}

	/// <summary>
	/// Removes null elements from an enumerable collection.
	/// </summary>
	public static IEnumerable<T> WhereNotNull<T> (this IEnumerable<T?> source) where T : class
	{
		foreach (var item in source) {
			if (item is not null)
				yield return item;
		}
	}
}
