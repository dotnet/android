using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerStaticFields
	{
		internal JniPeerStaticFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		Dictionary<string, JniStaticFieldID>                StaticFields  = new Dictionary<string, JniStaticFieldID>();

		public JniStaticFieldID GetFieldID (string encodedMember)
		{
			lock (StaticFields) {
				JniStaticFieldID f;
				if (!StaticFields.TryGetValue (encodedMember, out f)) {
					string field, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out field, out signature);
					f = Members.JniPeerType.GetStaticField (field, signature);
					StaticFields.Add (encodedMember, f);
				}
				return f;
			}
		}

		internal void Dispose ()
		{
			if (StaticFields == null)
				return;

			foreach (var f in StaticFields.Values)
				f.Dispose ();
			StaticFields = null;
		}

		public object GetValue (string encodedMember)
		{
			var n   = JniPeerMembers.GetSignatureSeparatorIndex (encodedMember);
			switch (encodedMember [n + 1]) {
			case 'Z':   return GetBooleanValue (encodedMember);
			case 'B':   return GetByteValue (encodedMember);
			case 'C':   return GetCharValue (encodedMember);
			case 'S':   return GetInt16Value (encodedMember);
			case 'I':   return GetInt32Value (encodedMember);
			case 'J':   return GetInt64Value (encodedMember);
			case 'F':   return GetSingleValue (encodedMember);
			case 'D':   return GetDoubleValue (encodedMember);
			case 'L':
			case '[':
				var lref = GetObjectValue (encodedMember);
				return JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer);
			default:
				throw new NotSupportedException ("Unsupported argument type: " + encodedMember.Substring (n + 1));
			}
		}

		public void SetValue (string encodedMember, object value)
		{
			var n   = JniPeerMembers.GetSignatureSeparatorIndex (encodedMember);
			switch (encodedMember [n + 1]) {
			case 'Z':   SetValue (encodedMember, (bool)   value);   break;
			case 'B':   SetValue (encodedMember, (byte)   value);   break;
			case 'C':   SetValue (encodedMember, (char)   value);   break;
			case 'S':   SetValue (encodedMember, (short)  value);   break;
			case 'I':   SetValue (encodedMember, (int)    value);   break;
			case 'J':   SetValue (encodedMember, (long)   value);   break;
			case 'F':   SetValue (encodedMember, (float)  value);   break;
			case 'D':   SetValue (encodedMember, (double) value);   break;
			case 'L':
			case '[':
				using (var lref = JniMarshal.CreateLocalRef (value)) {
					SetValue (encodedMember, lref);
				}
				return;
			default:
				throw new NotSupportedException ("Unsupported argument type: " + encodedMember.Substring (n + 1));
			}
		}
	}
}

