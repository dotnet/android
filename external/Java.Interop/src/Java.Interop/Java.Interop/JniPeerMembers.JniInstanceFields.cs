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
				var info = newField.Value;
				using var t = CreateTargetType (info, Members.JniPeerTypeName);
				if (TryGetInstanceField (t, info, field, signature, out var f)) {
					return f;
				}
			}
			if (Members.JniPeerType.TryGetInstanceField (field, signature, out var originalField)) {
				return originalField;
			}

			newField = JniPeerMembers.GetBaseReplacementFieldInfo (Members.ManagedPeerType, field, signature);
			if (newField.HasValue) {
				var info = newField.Value;
				using var t = CreateTargetType (info, Members.JniPeerTypeName);
				if (TryGetInstanceField (t, info, field, signature, out var f)) {
					return f;
				}
			}
			return Members.JniPeerType.GetInstanceField (field, signature);
		}
	}}
}
