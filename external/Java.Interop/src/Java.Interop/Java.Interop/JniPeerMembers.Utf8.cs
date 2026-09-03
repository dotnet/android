#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

namespace Java.Interop {

	/// <summary>Represents UTF-8 data whose address remains stable for the lifetime of the process.</summary>
	/// <remarks>The source span must be backed by static data, such as a UTF-8 string literal.</remarks>
	[EditorBrowsable (EditorBrowsableState.Never)]
	public readonly unsafe ref struct JniStaticUtf8String
	{
		JniStaticUtf8String (ReadOnlySpan<byte> value)
		{
			fixed (byte* pointer = value)
				Pointer = pointer;
			Length = value.Length;
		}

		internal byte* Pointer { get; }

		internal int Length { get; }

		/// <summary>Creates a pointer-backed UTF-8 value from static data.</summary>
		/// <remarks>The source span must be backed by static data and must never move or be released.</remarks>
		public static JniStaticUtf8String CreateStatic (ReadOnlySpan<byte> value) => new JniStaticUtf8String (value);
	}

	public readonly ref struct JniUtf8EncodedMember
	{
		public JniUtf8EncodedMember (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			: this (name, signature, hasStableIdentity: false)
		{
		}

		JniUtf8EncodedMember (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, bool hasStableIdentity)
		{
			Name              = name;
			Signature         = signature;
			HasStableIdentity = hasStableIdentity;
		}

		public JniUtf8EncodedMember (ReadOnlySpan<byte> encodedMember)
		{
			var separator = JniPeerMembers.GetSignatureSeparatorIndex (encodedMember);
			Name          = encodedMember.Slice (0, separator);
			Signature     = encodedMember.Slice (separator + 1);
		}

		public ReadOnlySpan<byte> Name { get; }

		public ReadOnlySpan<byte> Signature { get; }

		internal bool HasStableIdentity { get; }

		/// <summary>Creates a member descriptor whose name and signature have process-stable addresses.</summary>
		/// <remarks>Both spans must be backed by static data and must never move or be released.</remarks>
		[EditorBrowsable (EditorBrowsableState.Never)]
		public static JniUtf8EncodedMember CreateStatic (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature) =>
				new JniUtf8EncodedMember (name, signature, hasStableIdentity: true);

		public static implicit operator JniUtf8EncodedMember (ReadOnlySpan<byte> encodedMember) => FromReadOnlySpan (encodedMember);

		public static JniUtf8EncodedMember FromReadOnlySpan (ReadOnlySpan<byte> encodedMember) => new JniUtf8EncodedMember (encodedMember);

		public override string ToString ()
		{
			return $"{Encoding.UTF8.GetString (Name)}.{Encoding.UTF8.GetString (Signature)}";
		}
	}

	partial class JniPeerMembers {

		internal delegate TValue Utf8SingleValueFactory<TState, TValue> (ReadOnlySpan<byte> key, TState state);
		internal delegate TValue Utf8ValueFactory<TState, TValue> (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, TState state);

		internal sealed class Utf8ValueCache<TValue>
		{
			sealed class Entry
			{
				public Entry (nint identity, byte [] key, TValue value)
				{
					NameIdentity = identity;
					Name         = key;
					Signature    = [];
					Value        = value;
				}

				public Entry (nint nameIdentity, nint signatureIdentity, byte [] name, byte [] signature, TValue value, bool hasStableIdentity = false)
				{
					NameIdentity      = nameIdentity;
					SignatureIdentity = signatureIdentity;
					Name              = name;
					Signature         = signature;
					Value             = value;
					HasSignature      = true;
					HasStableIdentity = hasStableIdentity;
				}

				public nint NameIdentity { get; }
				public nint SignatureIdentity { get; }
				public byte [] Name { get; }
				public byte [] Signature { get; }
				public TValue Value { get; }
				public bool HasSignature { get; }
				public bool HasStableIdentity { get; }
			}

			readonly Dictionary<int, List<Entry>> values = new Dictionary<int, List<Entry>> ();
			Entry? []? identityEntries;
			int identityCount;

			public unsafe TValue GetOrAdd<TState> (ReadOnlySpan<byte> key, Utf8SingleValueFactory<TState, TValue> valueFactory, TState state)
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

			public unsafe TValue GetOrAdd<TState> (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				return GetOrAdd (name, signature, hasStableIdentity: false, valueFactory, state);
			}

			public TValue GetOrAdd<TState> (JniUtf8EncodedMember member, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				return GetOrAdd (member.Name, member.Signature, member.HasStableIdentity, valueFactory, state);
			}

			unsafe TValue GetOrAdd<TState> (ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, bool hasStableIdentity, Utf8ValueFactory<TState, TValue> valueFactory, TState state)
			{
				nint nameIdentity;
				nint signatureIdentity;
				fixed (byte* pointer = name)
					nameIdentity = (nint) pointer;
				fixed (byte* pointer = signature)
					signatureIdentity = (nint) pointer;

				var identityEntry = GetIdentityEntry (nameIdentity, signatureIdentity, name, signature, hasStableIdentity);
				if (identityEntry != null)
					return identityEntry.Value;

				var hash = GetHashCode (name, signature);
				lock (values) {
					var entry = GetEntry (hash, name, signature);
					if (entry != null) {
						if (hasStableIdentity)
							AddIdentityEntry (new Entry (nameIdentity, signatureIdentity, entry.Name, entry.Signature, entry.Value, hasStableIdentity: true));
						return entry.Value;
					}
				}

				var newValue     = valueFactory (name, signature, state);
				var newName      = name.ToArray ();
				var newSignature = signature.ToArray ();

				lock (values) {
					var entry = GetEntry (hash, name, signature);
					if (entry != null) {
						if (hasStableIdentity)
							AddIdentityEntry (new Entry (nameIdentity, signatureIdentity, entry.Name, entry.Signature, entry.Value, hasStableIdentity: true));
						return entry.Value;
					}

					if (!values.TryGetValue (hash, out var entries)) {
						entries = new List<Entry> ();
						values.Add (hash, entries);
					}
					var newEntry = new Entry (nameIdentity, signatureIdentity, newName, newSignature, newValue, hasStableIdentity);
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

			Entry? GetIdentityEntry (nint nameIdentity, nint signatureIdentity, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature, bool hasStableIdentity)
			{
				var entries = Volatile.Read (ref identityEntries);
				if (entries != null) {
					var index = GetIdentityHashCode (nameIdentity, name.Length, signatureIdentity, signature.Length) & (entries.Length - 1);
					for (var i = 0; i < entries.Length; i++) {
						var candidate = Volatile.Read (ref entries [index]);
						if (candidate == null)
							break;
						if (candidate.NameIdentity == nameIdentity &&
								candidate.SignatureIdentity == signatureIdentity &&
								candidate.Name.Length == name.Length &&
								candidate.Signature.Length == signature.Length &&
								(hasStableIdentity && candidate.HasStableIdentity ||
								(name.SequenceEqual (candidate.Name) && signature.SequenceEqual (candidate.Signature))))
							return candidate;
						index = (index + 1) & (entries.Length - 1);
					}
				}

				return null;
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
						if (!candidate.HasSignature && candidate.NameIdentity == identity && key.SequenceEqual (candidate.Name))
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
				var hash = entry.HasSignature
					? GetIdentityHashCode (entry.NameIdentity, entry.Name.Length, entry.SignatureIdentity, entry.Signature.Length)
					: GetIdentityHashCode (entry.NameIdentity);
				var index = hash & (entries.Length - 1);
				for (var i = 0; i < entries.Length; i++) {
					var existingEntry = entries [index];
					if (existingEntry == null) {
						Volatile.Write (ref entries [index], entry);
						return true;
					}
					if (existingEntry.HasSignature == entry.HasSignature &&
							existingEntry.NameIdentity == entry.NameIdentity &&
							existingEntry.Name.Length == entry.Name.Length &&
							(entry.HasStableIdentity && existingEntry.HasStableIdentity ||
							existingEntry.Name.AsSpan ().SequenceEqual (entry.Name))) {
						if (!entry.HasSignature ||
								(existingEntry.SignatureIdentity == entry.SignatureIdentity &&
								existingEntry.Signature.Length == entry.Signature.Length &&
								(entry.HasStableIdentity && existingEntry.HasStableIdentity ||
								existingEntry.Signature.AsSpan ().SequenceEqual (entry.Signature))))
							return false;
					}
					index = (index + 1) & (entries.Length - 1);
				}

				return false;
			}

			Entry? GetEntry (int hash, ReadOnlySpan<byte> name, ReadOnlySpan<byte> signature)
			{
				if (values.TryGetValue (hash, out var entries)) {
					foreach (var entry in entries) {
						if (entry.HasSignature && name.SequenceEqual (entry.Name) && signature.SequenceEqual (entry.Signature))
							return entry;
					}
				}

				return null;
			}

			Entry? GetEntry (int hash, ReadOnlySpan<byte> key)
			{
				if (values.TryGetValue (hash, out var entries)) {
					foreach (var entry in entries) {
						if (!entry.HasSignature && key.SequenceEqual (entry.Name))
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

			static int GetIdentityHashCode (nint nameIdentity, int nameLength, nint signatureIdentity, int signatureLength)
			{
				var nameValue      = (long) nameIdentity;
				var signatureValue = (long) signatureIdentity;
				var hash           = (int) nameValue ^ (int) (nameValue >> 32);
				hash               = (hash * 16777619) ^ nameLength;
				hash               = (hash * 16777619) ^ (int) signatureValue ^ (int) (signatureValue >> 32);
				return (hash * 16777619) ^ signatureLength;
			}

			static int GetIdentityHashCode (nint identity)
			{
				var value = (long) identity;
				return (int) value ^ (int) (value >> 32);
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

	}
}
