using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Text;

using Mono.Linq.Expressions;

using Java.Interop;

namespace Java.Interop.Dynamic {

	class JavaMethodInfo : JavaMethodBase {

		public  JniType         ReturnType;

		string  name;
		bool    isStatic;

		public JavaMethodInfo (JniPeerMembers members, JniReferenceSafeHandle method, string name, bool isStatic)
			: base (members, method)
		{
			this.name       = name;
			this.isStatic   = isStatic;
		}

		public  override    string  Name {
			get {return name;}
		}

		public  override    bool    IsStatic {
			get {return isStatic;}
		}

		public override     bool    IsConstructor {
			get {return false;}
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing || ReturnType == null)
				return;

			ReturnType.Dispose ();
			ReturnType  = null;
		}

		protected override string JniReturnType {
			get {
				if (ReturnType == null)
					return "V";
				return JniEnvironment.Current.JavaVM.GetJniTypeInfoForJniTypeReference (ReturnType.Name).JniTypeReference;
			}
		}

		public override unsafe object Invoke (IJavaObject self, JValue* arguments)
		{
			AssertSelf (self);

			if (IsStatic)
				return InvokeStaticMethod (arguments);
			return InvokeInstanceMethod (self, arguments);
		}

		void AssertSelf (IJavaObject self)
		{
			if (IsStatic && self != null)
				throw new ArgumentException (
						string.Format ("Field '{0}' is static but an instance was provided.", JniSignature),
						"self");
			if (!IsStatic && self == null)
				throw new ArgumentException (
						string.Format ("Field '{0}' is an instance field but no instance was provided.", JniSignature),
						"self");
		}

		unsafe object InvokeInstanceMethod (IJavaObject self, JValue* arguments)
		{
			var e   = GetSignatureReturnTypeStartIndex ();
			switch (JniSignature [e + 1]) {
			case 'Z':	return members.InstanceMethods.CallBooleanMethod (JniSignature, self, arguments);
			case 'B':   return members.InstanceMethods.CallSByteMethod (JniSignature, self, arguments);
			case 'C':   return members.InstanceMethods.CallCharMethod (JniSignature, self, arguments);
			case 'S':   return members.InstanceMethods.CallInt16Method (JniSignature, self, arguments);
			case 'I':   return members.InstanceMethods.CallInt32Method (JniSignature, self, arguments);
			case 'J':   return members.InstanceMethods.CallInt64Method (JniSignature, self, arguments);
			case 'F':   return members.InstanceMethods.CallSingleMethod (JniSignature, self, arguments);
			case 'D':   return members.InstanceMethods.CallDoubleMethod (JniSignature, self, arguments);
			case 'L':
			case '[':
				var lref = members.InstanceMethods.CallObjectMethod (JniSignature, self, arguments);
				return ToReturnValue (lref, JniSignature, e + 1);
			case 'V':
				members.InstanceMethods.CallVoidMethod (JniSignature, self, arguments);
				return null;
			default:
				throw new NotSupportedException ("Unsupported argument type: " + JniSignature.Substring (e + 1));
			}
		}

		unsafe object InvokeStaticMethod (JValue* arguments)
		{
			var e   = GetSignatureReturnTypeStartIndex ();
			switch (JniSignature [e + 1]) {
			case 'Z':   return members.StaticMethods.CallBooleanMethod (JniSignature, arguments);
			case 'B':   return members.StaticMethods.CallSByteMethod (JniSignature, arguments);
			case 'C':   return members.StaticMethods.CallCharMethod (JniSignature, arguments);
			case 'S':   return members.StaticMethods.CallInt16Method (JniSignature, arguments);
			case 'I':   return members.StaticMethods.CallInt32Method (JniSignature, arguments);
			case 'J':   return members.StaticMethods.CallInt64Method (JniSignature, arguments);
			case 'F':   return members.StaticMethods.CallSingleMethod (JniSignature, arguments);
			case 'D':   return members.StaticMethods.CallDoubleMethod (JniSignature, arguments);
			case 'L':
			case '[':
				var lref = members.StaticMethods.CallObjectMethod (JniSignature, arguments);
				return ToReturnValue (lref, JniSignature, e + 1);
			case 'V':
				members.StaticMethods.CallVoidMethod (JniSignature, arguments);
				return null;
			default:
				throw new NotSupportedException ("Unsupported argument type: " + JniSignature.Substring (e + 1));
			}
		}

		protected int GetSignatureReturnTypeStartIndex ()
		{
			int n = JniSignature.IndexOf (')');
			if (n == JniSignature.Length - 1)
				throw new NotSupportedException (
					string.Format ("Could not determine method return type from signature '{0}'.", JniSignature));
			return n;
		}
	}

}
