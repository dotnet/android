#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticFields
	{
		internal JniStaticFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		ConcurrentDictionary<string, JniFieldInfo>? staticFields;

		ConcurrentDictionary<string, JniFieldInfo> StaticFields => GetOrCreate (ref staticFields, 3);

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				ReadOnlySpan<char> field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.GetFieldInfo (field, signature);
			}, this);
		}

		JniFieldInfo GetFieldInfo (ReadOnlySpan<char> field, ReadOnlySpan<char> signature)
		{
			var newField = Members.GetReplacementFieldInfo (field, signature);
			if (newField.HasValue && TryGetReplacementField (newField.Value, field, signature, out var replacement))
				return replacement;

			if (Members.JniPeerType.TryGetStaticField (field, signature, out var originalField)) {
				return originalField;
			}

			newField = Members.GetBaseReplacementFieldInfo (field, signature);
			if (newField.HasValue && TryGetReplacementField (newField.Value, field, signature, out replacement))
				return replacement;

			return Members.JniPeerType.GetStaticField (field, signature);
		}

		bool TryGetReplacementField (
			JniRuntime.ReplacementFieldInfo info,
			ReadOnlySpan<char> fallbackName,
			ReadOnlySpan<char> fallbackSignature,
			[System.Diagnostics.CodeAnalysis.NotNullWhen (true)] out JniFieldInfo? field)
		{
			var typeName  = info.TargetJniType ?? Members.JniPeerTypeName;
			var fieldName = info.TargetJniFieldName is string name ? name.AsSpan () : fallbackName;
			var fieldSig  = info.TargetJniFieldSignature is string sig ? sig.AsSpan () : fallbackSignature;
			JniType? type = new JniType (typeName);
			try {
				if (!type.TryGetStaticField (fieldName, fieldSig, out field))
					return false;

				field.StaticRedirect = type;
				type = null;
				return true;
			} finally {
				type?.Dispose ();
			}
		}

		JniType GetFieldDeclaringType (JniFieldInfo field)
		{
			if (field.StaticRedirect != null)
				return field.StaticRedirect;
			return Members.JniPeerType;
		}

		internal void Dispose ()
		{
			Clear (ref staticFields, static field => field.StaticRedirect?.Dispose ());
		}
	}}
}
