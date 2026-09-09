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
			var newField = JniPeerMembers.GetReplacementFieldInfo (Members.JniPeerTypeName, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName is string name ? name.AsSpan () : field;
				var fieldSig     = newField.Value.TargetJniFieldSignature is string sig ? sig.AsSpan () : signature;

				using var t = new JniType (typeName);
				if (t.TryGetStaticField (fieldName, fieldSig, out var f)) {
					return f;
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

				using var t = new JniType (typeName);
				if (t.TryGetStaticField (fieldName, fieldSig, out var f)) {
					return f;
				}
			}
			return Members.JniPeerType.GetStaticField (field, signature);
		}

		internal void Dispose ()
		{
			Clear (ref staticFields);
		}
	}}
}
