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
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.GetFieldInfo (field, signature);
			}, this);
		}

		JniFieldInfo GetFieldInfo (string field, string signature)
		{
			var newField = JniPeerMembers.GetReplacementFieldInfo (Members.JniPeerTypeName, Members.ManagedPeerType, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName ?? field;
				var fieldSig     = newField.Value.TargetJniFieldSignature ?? signature;

				using var t = new JniType (typeName);
				if (t.TryGetInstanceField (fieldName, fieldSig, out var f)) {
					return f;
				}
			}
			return Members.JniPeerType.GetInstanceField (field, signature);
		}
	}}
}
