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
			return GetOrAdd (
				StaticFields,
				encodedMember,
				static (member, fields) => {
					ReadOnlySpan<char> field, signature;
					JniPeerMembers.GetNameAndSignature (member, out field, out signature);
					return fields.GetFieldInfo (field, signature);
				},
				this,
				static field => field.StaticRedirect?.Dispose ());
		}

		JniFieldInfo GetFieldInfo (ReadOnlySpan<char> field, ReadOnlySpan<char> signature)
		{
			var newField = JniPeerMembers.GetReplacementFieldInfo (Members.JniPeerTypeName, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName is string name ? name.AsSpan () : field;
				var fieldSig     = newField.Value.TargetJniFieldSignature is string sig ? sig.AsSpan () : signature;

				JniType? t = new JniType (typeName);
				try {
					if (t.TryGetStaticField (fieldName, fieldSig, out var f)) {
						f.StaticRedirect = t;
						t = null;
						return f;
					}
				} finally {
					t?.Dispose ();
				}
			}
			if (Members.JniPeerType.TryGetStaticField (field, signature, out var originalField)) {
				return originalField;
			}

			newField = JniPeerMembers.GetBaseReplacementFieldInfo (Members.ManagedPeerType, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName is string name ? name.AsSpan () : field;
				var fieldSig     = newField.Value.TargetJniFieldSignature is string sig ? sig.AsSpan () : signature;

				JniType? t = new JniType (typeName);
				try {
					if (t.TryGetStaticField (fieldName, fieldSig, out var f)) {
						f.StaticRedirect = t;
						t = null;
						return f;
					}
				} finally {
					t?.Dispose ();
				}
			}
			return Members.JniPeerType.GetStaticField (field, signature);
		}

		JniType GetFieldDeclaringType (JniFieldInfo field)
		{
			return field.StaticRedirect ?? Members.JniPeerType;
		}

		internal void Dispose ()
		{
			Clear (ref staticFields, static field => field.StaticRedirect?.Dispose ());
		}
	}}
}
