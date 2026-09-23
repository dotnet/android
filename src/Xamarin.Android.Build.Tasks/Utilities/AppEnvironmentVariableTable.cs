#nullable enable
using System;
using System.Collections.Generic;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Name:value pairs (environment variables or system properties) stored in an array of
/// <c>AppEnvironmentVariable</c> structures, which refer to NUL-terminated strings in a
/// separate character array.
/// </summary>
sealed class AppEnvironmentVariableTable
{
	// Must be identical to the AppEnvironmentVariable structure in src/native/clr/include/xamarin-app.hh
	const ulong StructureSize = 8;
	const ulong StructureAlignment = 4;

	readonly List<(string Name, string Value, int NameIndex, int ValueIndex)> entries = new ();
	readonly LlvmIrStringBlob contents = new ();

	public int Count => entries.Count;

	public AppEnvironmentVariableTable (TaskLoggingHelper log, IDictionary<string, string>? variables)
	{
		if (variables == null) {
			return;
		}

		foreach (var kvp in variables) {
			string name = kvp.Key.Trim ();
			if (name.Length == 0) {
				log.LogDebugMessage ($"Not adding environment variable without a name. Value: '{kvp.Value}'");
				continue;
			}

			int nameIndex = contents.Add (name);
			int valueIndex = contents.Add (kvp.Value);
			entries.Add ((name, kvp.Value, nameIndex, valueIndex));
		}
	}

	public static void WriteDeclaration (LlvmIrWriter w)
	{
		w.Write ($$"""

			%struct.AppEnvironmentVariable = type {
				i32, ; uint32_t name_index
				i32 ; uint32_t value_index
			}

			""");
	}

	public void Write (LlvmIrWriter w, string arrayName, string contentsName, string comment)
	{
		var elements = new List<string> (entries.Count);
		foreach (var e in entries) {
			elements.Add ($$"""
					%struct.AppEnvironmentVariable {
						i32 {{e.NameIndex}}, {{w.Comment ($" '{e.Name}'")}}
						i32 {{e.ValueIndex}}{{w.Comment ($" '{e.Value}'")}}
					}
				""");
		}

		w.WriteGlobal (
			arrayName,
			LlvmIrWriter.GlobalConstant,
			$"[{elements.Count} x %struct.AppEnvironmentVariable]",
			w.ArrayValue (elements, i => $" {i}"),
			w.GetAggregateAlignment (StructureAlignment, (ulong)elements.Count * StructureSize),
			comment
		);
		contents.Write (w, contentsName);
	}
}
