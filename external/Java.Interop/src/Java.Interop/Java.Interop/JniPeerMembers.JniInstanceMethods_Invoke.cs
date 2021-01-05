#nullable enable

using System;

namespace Java.Interop {

	partial class JniPeerMembers {

		partial class JniInstanceMethods {

			public unsafe void InvokeAbstractVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return;
			}

			public unsafe void InvokeVirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return;
			}

			public unsafe void InvokeNonvirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return;
			}

			public unsafe bool InvokeAbstractBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe bool InvokeVirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe bool InvokeNonvirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe sbyte InvokeAbstractSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe sbyte InvokeVirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe sbyte InvokeNonvirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe char InvokeAbstractCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe char InvokeVirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe char InvokeNonvirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe short InvokeAbstractInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe short InvokeVirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe short InvokeNonvirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe int InvokeAbstractInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe int InvokeVirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe int InvokeNonvirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe long InvokeAbstractInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe long InvokeVirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe long InvokeNonvirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe float InvokeAbstractSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe float InvokeVirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe float InvokeNonvirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe double InvokeAbstractDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe double InvokeVirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe double InvokeNonvirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe JniObjectReference InvokeAbstractObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe JniObjectReference InvokeVirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					var _nr = JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
					GC.KeepAlive (self);
					return _nr;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				var r = JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				GC.KeepAlive (self);
				return r;
			}

			public unsafe JniObjectReference InvokeNonvirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				var r   = JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				GC.KeepAlive (self);
				return r;
			}
		}
	}
}
