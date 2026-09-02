#nullable enable

using System;
using System.Collections.Generic;

namespace Java.Interop {

	partial class JniPeerMembers {

		internal delegate TValue Utf8ValueFactory<TState, TValue> (ReadOnlySpan<byte> key, TState state);

		internal sealed class Utf8ValueCache<TValue>
		{
			sealed class Entry
			{
				public Entry (byte [] key, TValue value)
				{
					Key   = key;
					Value = value;
				}

				public byte [] Key { get; }
				public TValue Value { get; }
			}

			readonly Dictionary<int, List<Entry>> values = new Dictionary<int, List<Entry>> ();

			public TValue GetOrAdd<TState> (ReadOnlySpan<byte> key, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				var hash = GetHashCode (key);
				lock (values) {
					var entry = GetEntry (hash, key);
					if (entry != null)
						return entry.Value;
				}

				var newValue = valueFactory (key, state);
				var newKey   = key.ToArray ();

				lock (values) {
					var entry = GetEntry (hash, key);
					if (entry != null)
						return entry.Value;

					if (!values.TryGetValue (hash, out var entries)) {
						entries = new List<Entry> ();
						values.Add (hash, entries);
					}
					entries.Add (new Entry (newKey, newValue));
					return newValue;
				}
			}

			public void Clear ()
			{
				lock (values)
					values.Clear ();
			}

			Entry? GetEntry (int hash, ReadOnlySpan<byte> key)
			{
				if (values.TryGetValue (hash, out var entries)) {
					foreach (var entry in entries) {
						if (key.SequenceEqual (entry.Key))
							return entry;
					}
				}

				return null;
			}

			static int GetHashCode (ReadOnlySpan<byte> key)
			{
				unchecked {
					var hash = (int) 2166136261;
					foreach (var value in key)
						hash = (hash ^ value) * 16777619;
					return hash;
				}
			}
		}

		internal static int GetSignatureSeparatorIndex (ReadOnlySpan<byte> encodedMember)
		{
			int n = encodedMember.IndexOf ((byte) '.');
			if (n < 0)
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' should be encoded as \"<NAME>.<SIGNATURE>\".",
						nameof (encodedMember));
			if (encodedMember.Length <= (n + 1))
				throw new ArgumentException (
						"Invalid encoding; 'encodedMember' is missing a JNI signature, and should be in the format \"<NAME>.<SIGNATURE>\".",
						nameof (encodedMember));
			return n;
		}

		internal static byte [] GetNullTerminatedUtf8 (ReadOnlySpan<byte> value)
		{
			var terminated = new byte [value.Length + 1];
			value.CopyTo (terminated);
			return terminated;
		}
	}
}
