using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.Build.Utilities;

/// <para>
/// When bundle configuration uses standard settings for split configs, the per-ABI library
/// directory (which contains all of our DSOs/assemblies/blobs etc) will be placed in a per-ABI
/// split config file named `split_config.{ARCH}.apk` and we use the fact to optimize startup
/// time.
/// </para>
///
/// <para>
/// However, if a custom build config file with the following settings is found, Android bundletool
/// doesn't create the per-ABI split config file, and so we need to search all the files in order
/// to find shared libraries, assemblies/blobs etc:
/// <code>
///   {
///     "optimizations": {
///       "splitsConfig": {
///         "splitDimension": [
///           {
///             "value": "ABI",
///             "negate": true
///           }
///         ],
///       }
///     }
///   }
///</code></para>
///
/// <para>
/// The presence or absence of split config files is checked in our Java startup code which will
/// notice that split configs are present, but will not check (for performance reasons, to avoid
/// string comparisons) whether the per-ABI split config is present.  We, therefore, need to let
/// our native runtime know in some inexpensive way that the split configs should be ignored and
/// that the DSOs/assemblies/blobs should be searched for in the usual, non-split config, way.
/// </para>
///
/// <para>
/// Since we know at build time whether this is the case, it's best to record the fact then and
/// let the native runtime merely check a boolean flag instead of dynamic detection at each app
/// startup.
/// </para>
static class BundleConfigSplitConfigsChecker
{
	enum BundleConfigObject
	{
		None,
		Root,
		Other,
		Optimizations,
		SplitsConfig,
		SplitDimension,
	}

	ref struct Strings {
		public readonly ReadOnlySpan<byte> UTF8BOM;
		public readonly ReadOnlySpan<byte> ValuePropertyName;
		public readonly ReadOnlySpan<byte> NegatePropertyName;

		public Strings ()
		{
			UTF8BOM                 = new byte[] { 0xEF, 0xBB, 0xBF };
			ValuePropertyName       = Encoding.ASCII.GetBytes ("value");
			NegatePropertyName      = Encoding.ASCII.GetBytes ("negate");
		}
	}

	public static bool ShouldIgnoreSplitConfigs (TaskLoggingHelper log, string configFilePath)
	{
		try {
			return DoShouldIgnoreSplitConfigs (log, configFilePath);
		} catch (Exception ex) {
			log.LogWarning ($"Failed to process bundle config file '{configFilePath}', split config files will be ignored at run time.");
			log.LogWarningFromException (ex);
			return true;
		}
	}

	static bool DoShouldIgnoreSplitConfigs (TaskLoggingHelper log, string configFilePath)
	{
		var options = new JsonReaderOptions {
			AllowTrailingCommas = true,
			CommentHandling = JsonCommentHandling.Skip
		};

		Strings strings = new ();
		ReadOnlySpan<byte> json = File.ReadAllBytes (configFilePath);
		if (json.StartsWith (strings.UTF8BOM)) {
			json = json.Slice (strings.UTF8BOM.Length);
		}

		var state = new Stack<BundleConfigObject> ();
		state.Push (BundleConfigObject.None);

		bool? valueIsAbi = null;
		bool? negate = null;
		string? lastPropertyName = null;
		var reader = new Utf8JsonReader (json, options);
		while (reader.Read ()) {
			JsonTokenType tokenType = reader.TokenType;

			switch (tokenType) {
				case JsonTokenType.StartObject:
					TransitionState (strings, reader, state, lastPropertyName);
					lastPropertyName = null;
					break;

				case JsonTokenType.EndObject:
					if (state.Peek () != BundleConfigObject.None) {
						BundleConfigObject popped = state.Pop ();
					}
					lastPropertyName = null;
					break;

				case JsonTokenType.PropertyName:
					lastPropertyName = reader.GetString ();
					if (state.Peek () == BundleConfigObject.SplitDimension) {
						CheckSplitDimensionProperty (reader, strings, ref valueIsAbi, ref negate);
					}
					break;
			}
		}

		if (!valueIsAbi.HasValue || !negate.HasValue) {
			return false;
		}

		return valueIsAbi.Value && negate.Value;
	}

	static void CheckSplitDimensionProperty (Utf8JsonReader reader, Strings strings, ref bool? valueIsAbi, ref bool? negate)
	{
		if (!valueIsAbi.HasValue) {
			if (reader.ValueTextEquals (strings.ValuePropertyName)) {
				reader.Read ();
				string v = reader.GetString ();
				valueIsAbi = String.CompareOrdinal ("ABI", v) == 0;
				return;
			}
		}

		if (negate.HasValue) {
			return;
		}

		if (reader.ValueTextEquals (strings.NegatePropertyName)) {
			reader.Read ();
			negate = reader.GetBoolean ();
		}
	}

	static void TransitionState (Strings strings, Utf8JsonReader reader, Stack<BundleConfigObject> state, string? objectName)
	{
		BundleConfigObject current = state.Peek ();
		if (current == BundleConfigObject.None) {
			state.Push (BundleConfigObject.Root);
			return;
		}

		BundleConfigObject need = current switch {
			BundleConfigObject.Root          => BundleConfigObject.Optimizations,
			BundleConfigObject.Optimizations => BundleConfigObject.SplitsConfig,
			BundleConfigObject.SplitsConfig  => BundleConfigObject.SplitDimension,
			_                                => BundleConfigObject.Other
		};

		if (need == BundleConfigObject.Other) {
			state.Push (need);
			return;
		}

		string needName = need switch {
			BundleConfigObject.Optimizations  => "optimizations",
			BundleConfigObject.SplitsConfig   => "splitsConfig",
			BundleConfigObject.SplitDimension => "splitDimension",
			_                                 => throw new InvalidOperationException ($"Internal error: unsupported state transition to '{need}'")
		};

		if (!String.IsNullOrEmpty (objectName) && String.CompareOrdinal (needName, objectName) == 0) {
			state.Push (need);
		} else {
			state.Push (BundleConfigObject.Other);
		}
	}
}
