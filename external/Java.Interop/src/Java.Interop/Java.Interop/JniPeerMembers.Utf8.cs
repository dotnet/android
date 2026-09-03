#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Java.Interop {

	partial class JniPeerMembers {

		internal delegate TValue Utf8ValueFactory<TState, TValue> (ReadOnlySpan<byte> key, TState state);

		internal sealed class Utf8ValueCache<TValue>
		{
			sealed class Entry
			{
				public Entry (nint identity, byte [] key, TValue value)
				{
					Identity = identity;
					Key   = key;
					Value = value;
				}

				public nint Identity { get; }
				public byte [] Key { get; }
				public TValue Value { get; }
			}

			readonly Dictionary<int, List<Entry>> values = new Dictionary<int, List<Entry>> ();
			Entry? []? identityEntries;
			int identityCount;

			public unsafe TValue GetOrAdd<TState> (ReadOnlySpan<byte> key, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				nint identity;
				fixed (byte* pointer = key)
					identity = (nint) pointer;

				var identityEntry = GetIdentityEntry (identity, key);
				if (identityEntry != null)
					return identityEntry.Value;

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
					var newEntry = new Entry (identity, newKey, newValue);
					entries.Add (newEntry);
					AddIdentityEntry (newEntry);
					return newValue;
				}
			}

			public void Clear ()
			{
				lock (values) {
					values.Clear ();
					Volatile.Write (ref identityEntries, null);
					identityCount = 0;
				}
			}

			Entry? GetIdentityEntry (nint identity, ReadOnlySpan<byte> key)
			{
				var entries = Volatile.Read (ref identityEntries);
				if (entries != null) {
					var index = GetIdentityHashCode (identity) & (entries.Length - 1);
					for (var i = 0; i < entries.Length; i++) {
						var candidate = Volatile.Read (ref entries [index]);
						if (candidate == null)
							break;
						if (candidate.Identity == identity && key.SequenceEqual (candidate.Key))
							return candidate;
						index = (index + 1) & (entries.Length - 1);
					}
				}

				return null;
			}

			void AddIdentityEntry (Entry entry)
			{
				var entries = identityEntries;
				if (entries == null || identityCount * 2 >= entries.Length) {
					var newEntries = new Entry? [entries?.Length * 2 ?? 8];
					if (entries != null) {
						foreach (var existingEntry in entries) {
							if (existingEntry != null)
								AddIdentityEntry (newEntries, existingEntry);
						}
					}
					entries = newEntries;
					Volatile.Write (ref identityEntries, entries);
				}

				if (AddIdentityEntry (entries, entry))
					identityCount++;
			}

			static bool AddIdentityEntry (Entry? [] entries, Entry entry)
			{
				var index = GetIdentityHashCode (entry.Identity) & (entries.Length - 1);
				for (var i = 0; i < entries.Length; i++) {
					var existingEntry = entries [index];
					if (existingEntry == null) {
						Volatile.Write (ref entries [index], entry);
						return true;
					}
					if (existingEntry.Identity == entry.Identity)
						return false;
					index = (index + 1) & (entries.Length - 1);
				}

				return false;
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

			static int GetIdentityHashCode (nint identity)
			{
				var value = (long) identity;
				return (int) value ^ (int) (value >> 32);
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

	}
}
