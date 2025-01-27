using System;
using System.Collections.Generic;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks.LLVMIR;

partial class LlvmIrModule
{
	protected class LlvmIrStringManager
	{
		sealed class ByteArrayEqualityComparer : EqualityComparer<byte []>
		{
			public override bool Equals (byte [] x, byte [] y)
			{
				if (x == null || y == null) {
					return x == y;
				}

				if (ReferenceEquals (x, y)) {
					return true;
				}

				if (x.Length != y.Length) {
					return false;
				}

				if (x.Length == 0) {
					return true;
				}

				for (int i = 0; i < x.Length; i++) {
					if (x[i] != y[i]) {
						return false;
					}
				}

				return true;
			}

			public override int GetHashCode (byte [] obj)
			{
				return 0; // force use of Equals
			}
		}

		// A byte array is needed for caching since it's possible that we might have two distinct variables
		// with the same string, only encoded in different encodings.  Slow...
		Dictionary<byte[], LlvmIrStringVariable> stringSymbolCache = new Dictionary<byte[], LlvmIrStringVariable> (new ByteArrayEqualityComparer ());
		Dictionary<string, LlvmIrStringGroup> stringGroupCache = new Dictionary<string, LlvmIrStringGroup> (StringComparer.Ordinal);
		List<LlvmIrStringGroup> stringGroups = new List<LlvmIrStringGroup> ();

		LlvmIrStringGroup defaultGroup;
		TaskLoggingHelper log;

		public List<LlvmIrStringGroup> StringGroups => stringGroups;

		public LlvmIrStringManager (TaskLoggingHelper log)
		{
			this.log = log;
			defaultGroup = new LlvmIrStringGroup ();
			stringGroupCache.Add (String.Empty, defaultGroup);
			stringGroups.Add (defaultGroup);
		}

		public LlvmIrStringVariable Add (string value, string? groupName = null, string? groupComment = null, string? symbolSuffix = null,
			LlvmIrStringEncoding encoding = LlvmIrStringEncoding.UTF8)
		{
			if (value == null) {
				throw new ArgumentNullException (nameof (value));
			}

			byte[] valueBytes = GetStringBytes (value, encoding);
			LlvmIrStringVariable? stringVar;
			if (stringSymbolCache.TryGetValue (valueBytes, out stringVar) && stringVar != null) {
				return stringVar;
			}

			LlvmIrStringGroup? group;
			string groupPrefix;
			if (String.IsNullOrEmpty (groupName) || String.Compare ("str", groupName, StringComparison.Ordinal) == 0) {
				group = defaultGroup;
				groupPrefix = $".{defaultGroupName}";
			} else if (!stringGroupCache.TryGetValue (groupName, out group) || group == null) {
				group = new LlvmIrStringGroup (groupComment ?? groupName);
				stringGroups.Add (group);
				stringGroupCache[groupName] = group;
				groupPrefix = $".{groupName}";
			} else {
				groupPrefix = $".{groupName}";
			}

			string symbolName = $"{groupPrefix}.{group.Count++}";
			if (!String.IsNullOrEmpty (symbolSuffix)) {
				symbolName = $"{symbolName}_{symbolSuffix}";
			}

			stringVar = new LlvmIrStringVariable (symbolName, value, encoding);
			group.Strings.Add (stringVar);
			stringSymbolCache.Add (valueBytes, stringVar);

			return stringVar;
		}

		// TODO: introduce a "string holder" type which will keep the encoding alongside the actual value
		public LlvmIrStringVariable? Lookup (string value, LlvmIrStringEncoding encoding)
		{
			byte[] valueBytes = GetStringBytes (value, encoding);
			if (stringSymbolCache.TryGetValue (valueBytes, out LlvmIrStringVariable? sv)) {
				return sv;
			}

			return null;
		}

		byte[] GetStringBytes (string value, LlvmIrStringEncoding encoding)
		{
			return encoding switch {
				LlvmIrStringEncoding.UTF8    => MonoAndroidHelper.Utf8StringToBytes (value),
				LlvmIrStringEncoding.Unicode => MonoAndroidHelper.Utf16StringToBytes (value),
				_                            => throw new InvalidOperationException ($"Internal error: unsupported encoding '{encoding}'")
			};
		}
	}
}
