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

		readonly ConcurrentDictionary<string, JniFieldInfo> StaticFields = new ConcurrentDictionary<string, JniFieldInfo> (1, 3, StringComparer.Ordinal);

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.GetFieldInfo (field, signature);
			}, this);
		}

		JniFieldInfo GetFieldInfo (string field, string signature)
		{
			var newField = JniPeerMembers.GetReplacementFieldInfo (Members.JniPeerOriginalTypeName, Members.JniPeerTypeName, Members.ManagedPeerType, field, signature);
			if (newField.HasValue) {
				var typeName     = newField.Value.TargetJniType ?? Members.JniPeerTypeName;
				var fieldName    = newField.Value.TargetJniFieldName ?? field;
				var fieldSig     = newField.Value.TargetJniFieldSignature ?? signature;

				using var t = new JniType (typeName);
				if (t.TryGetStaticField (fieldName, fieldSig, out var f)) {
					return f;
				}
			}
			return Members.JniPeerType.GetStaticField (field, signature);
		}

		internal void Dispose ()
		{
			StaticFields.Clear ();
		}
	}}
}
