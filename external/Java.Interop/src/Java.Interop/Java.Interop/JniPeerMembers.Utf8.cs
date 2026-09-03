#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Java.Interop {

	public readonly ref struct JniUtf8EncodedMember
	{
		public JniUtf8EncodedMember (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
		{
			Name      = name;
			Signature = signature;
		}

		public JniUtf8EncodedMember (ReadOnlySpan<byte> encodedMember)
		{
			var separator = JniPeerMembers.GetSignatureSeparatorIndex (encodedMember);
			Name          = encodedMember.Slice (0, separator);
			Signature     = encodedMember.Slice (separator + 1);
		}

		public ReadOnlySpan<byte> Name { get; }

		public ReadOnlySpan<byte> Signature { get; }

		public static implicit operator JniUtf8EncodedMember (ReadOnlySpan<byte> encodedMember) => FromReadOnlySpan (encodedMember);

		public static JniUtf8EncodedMember FromReadOnlySpan (ReadOnlySpan<byte> encodedMember) => new JniUtf8EncodedMember (encodedMember);

		public override string ToString ()
		{
			return $"{Encoding.UTF8.GetString (Name)}.{Encoding.UTF8.GetString (Signature)}";
		}
	}

	partial class JniPeerMembers {

		internal delegate TValue Utf8ValueFactory<TState, TValue> (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, TState state);

		internal sealed class Utf8ValueCache<TValue>
		{
			sealed class Entry
			{
				public Entry (nint nameIdentity, nint signatureIdentity, byte [] name, byte [] signature, TValue value)
				{
					NameIdentity      = nameIdentity;
					SignatureIdentity = signatureIdentity;
					Name              = name;
					Signature         = signature;
					Value             = value;
				}

				public nint NameIdentity { get; }
				public nint SignatureIdentity { get; }
				public byte [] Name { get; }
				public byte [] Signature { get; }
				public TValue Value { get; }
			}

			readonly Dictionary<int, List<Entry>> values = new Dictionary<int, List<Entry>> ();
			Entry? []? identityEntries;
			int identityCount;

			public unsafe TValue GetOrAdd<TState> (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				nint nameIdentity;
				nint signatureIdentity;
				fixed (byte* pointer = name)
					nameIdentity = (nint) pointer;
				fixed (byte* pointer = signature)
					signatureIdentity = (nint) pointer;

				var identityEntry = GetIdentityEntry (nameIdentity, signatureIdentity, name, signature);
				if (identityEntry != null)
					return identityEntry.Value;

				var hash = GetHashCode (name, signature);
				lock (values) {
					var entry = GetEntry (hash, name, signature);
					if (entry != null)
						return entry.Value;
				}

				var newValue     = valueFactory (name, signature, state);
				var newName      = name.ToArray ();
				var newSignature = signature.ToArray ();

				lock (values) {
					var entry = GetEntry (hash, name, signature);
					if (entry != null)
						return entry.Value;

					if (!values.TryGetValue (hash, out var entries)) {
						entries = new List<Entry> ();
						values.Add (hash, entries);
					}
					var newEntry = new Entry (nameIdentity, signatureIdentity, newName, newSignature, newValue);
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

			Entry? GetIdentityEntry (nint nameIdentity, nint signatureIdentity, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				var entries = Volatile.Read (ref identityEntries);
				if (entries != null) {
					var index = GetIdentityHashCode (nameIdentity, signatureIdentity) & (entries.Length - 1);
					for (var i = 0; i < entries.Length; i++) {
						var candidate = Volatile.Read (ref entries [index]);
						if (candidate == null)
							break;
						if (candidate.NameIdentity == nameIdentity &&
								candidate.SignatureIdentity == signatureIdentity &&
								name.SequenceEqual (candidate.Name) &&
								signature.SequenceEqual (candidate.Signature))
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
				var index = GetIdentityHashCode (entry.NameIdentity, entry.SignatureIdentity) & (entries.Length - 1);
				for (var i = 0; i < entries.Length; i++) {
					var existingEntry = entries [index];
					if (existingEntry == null) {
						Volatile.Write (ref entries [index], entry);
						return true;
					}
					if (existingEntry.NameIdentity == entry.NameIdentity &&
							existingEntry.SignatureIdentity == entry.SignatureIdentity &&
							existingEntry.Name.AsSpan ().SequenceEqual (entry.Name) &&
							existingEntry.Signature.AsSpan ().SequenceEqual (entry.Signature))
						return false;
					index = (index + 1) & (entries.Length - 1);
				}

				return false;
			}

			Entry? GetEntry (int hash, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (values.TryGetValue (hash, out var entries)) {
					foreach (var entry in entries) {
						if (name.SequenceEqual (entry.Name) && signature.SequenceEqual (entry.Signature))
							return entry;
					}
				}

				return null;
			}

			static int GetHashCode (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				unchecked {
					var hash = (int) 2166136261;
					foreach (var value in name)
						hash = (hash ^ value) * 16777619;
					hash = (hash ^ 0xff) * 16777619;
					foreach (var value in signature)
						hash = (hash ^ value) * 16777619;
					return hash;
				}
			}

			static int GetIdentityHashCode (nint nameIdentity, nint signatureIdentity)
			{
				var nameValue      = (long) nameIdentity;
				var signatureValue = (long) signatureIdentity;
				var hash           = (int) nameValue ^ (int) (nameValue >> 32);
				return (hash * 16777619) ^ (int) signatureValue ^ (int) (signatureValue >> 32);
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
