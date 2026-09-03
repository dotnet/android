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
		readonly Utf8ValueCache<JniFieldInfo>                Utf8StaticFields = new Utf8ValueCache<JniFieldInfo> ();

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		public JniFieldInfo GetFieldInfo (ReadOnlySpan<byte> encodedMember)
		{
			return GetFieldInfo (new JniUtf8EncodedMember (encodedMember));
		}

		public JniFieldInfo GetFieldInfo (JniUtf8EncodedMember encodedMember)
		{
			return Utf8StaticFields.GetOrAdd (encodedMember.Name, encodedMember.Signature, static (fieldName, fieldType, fields) => {
				Span<byte> field = fieldName.Length + 1 <= 256
					? stackalloc byte [fieldName.Length + 1]
					: new byte [fieldName.Length + 1];
				Span<byte> signature = fieldType.Length + 1 <= 512
					? stackalloc byte [fieldType.Length + 1]
					: new byte [fieldType.Length + 1];
				fieldName.CopyTo (field);
				fieldType.CopyTo (signature);
				field [fieldName.Length]     = 0;
				signature [fieldType.Length] = 0;
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		internal void Dispose ()
		{
			StaticFields.Clear ();
			Utf8StaticFields.Clear ();
		}
	}}
}
