#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniInstanceFields
	{
		internal JniInstanceFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		ConcurrentDictionary<string, JniFieldInfo>? instanceFields;

		ConcurrentDictionary<string, JniFieldInfo> InstanceFields => GetOrCreate (ref instanceFields, 3);

		internal void Dispose ()
		{
			Clear (ref instanceFields);
		}

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return InstanceFields.GetOrAdd (encodedMember, static (member, fields) => {
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
				if (t.TryGetInstanceField (fieldName, fieldSig, out var f)) {
					return f;
				}
			}
			if (Members.JniPeerType.TryGetInstanceField (field, signature, out var originalField)) {
				return originalField;
			}

			newField = JniPeerMembers.GetBaseReplacementFieldInfo (Members.ManagedPeerType, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName is string name ? name.AsSpan () : field;
				var fieldSig     = newField.Value.TargetJniFieldSignature is string sig ? sig.AsSpan () : signature;

				using var t = new JniType (typeName);
				if (t.TryGetInstanceField (fieldName, fieldSig, out var f)) {
					return f;
				}
			}
			return Members.JniPeerType.GetInstanceField (field, signature);
		}
	}}
}
