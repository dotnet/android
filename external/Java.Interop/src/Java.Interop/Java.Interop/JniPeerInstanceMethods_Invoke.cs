using System;

namespace Java.Interop {

	partial class JniPeerMembers {

		partial class JniInstanceMethods {

			public unsafe void InvokeVirtualVoidMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					m.InvokeVirtualVoidMethod (self.PeerReference, parameters);
					return;
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				n.InvokeNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				return;
			}

			public unsafe void InvokeNonvirtualVoidMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				m.InvokeNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				return;
		}

			public unsafe bool InvokeVirtualBooleanMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualBooleanMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe bool InvokeNonvirtualBooleanMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe sbyte InvokeVirtualSByteMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualSByteMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualSByteMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe sbyte InvokeNonvirtualSByteMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualSByteMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe char InvokeVirtualCharMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualCharMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe char InvokeNonvirtualCharMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe short InvokeVirtualInt16Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualInt16Method (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualInt16Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe short InvokeNonvirtualInt16Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualInt16Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe int InvokeVirtualInt32Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualInt32Method (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualInt32Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe int InvokeNonvirtualInt32Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualInt32Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe long InvokeVirtualInt64Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualInt64Method (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualInt64Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe long InvokeNonvirtualInt64Method (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualInt64Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe float InvokeVirtualSingleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualSingleMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualSingleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe float InvokeNonvirtualSingleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualSingleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe double InvokeVirtualDoubleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualDoubleMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe double InvokeNonvirtualDoubleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}

			public unsafe JniObjectReference InvokeVirtualObjectMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (self.GetType () == declaringType || declaringType == null) {
					var m   = GetMethodInfo (encodedMember);
					return m.InvokeVirtualObjectMethod (self.PeerReference, parameters);
					
				}
				var j = self.JniPeerMembers;
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return n.InvokeNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
			}

			public unsafe JniObjectReference InvokeNonvirtualObjectMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var j   = self.JniPeerMembers;
				var m   = GetMethodInfo (encodedMember);

				return m.InvokeNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
				
		}
		}
	}
}
