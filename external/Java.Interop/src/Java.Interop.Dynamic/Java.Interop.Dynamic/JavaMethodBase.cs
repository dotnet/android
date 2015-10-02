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

	abstract class JavaMethodBase : JavaMemberInfo {

		public      abstract    bool        IsConstructor   {get;}

		public      abstract    object      Invoke (IJavaObject self, JValue[] arguments);

		protected   abstract    string      JniReturnType   {get;}

		public      JniGlobalReference      SafeHandle      {get; private set;}
		public      string                  JniSignature    {get; private set;}

		protected   JniPeerMembers          members;

		List<JniGlobalReference>    arguments;
		public  List<JniGlobalReference>    ArgumentTypes {
			get {
				LookupArguments ();
				return arguments;
			}
		}

		public  string  JniDeclaringClassName {
			get { return members == null ? "" : members.JniPeerTypeName; }
		}

		public JavaMethodBase (JniPeerMembers members, JniReferenceSafeHandle method)
		{
			this.members    = members;
			SafeHandle      = method.NewGlobalRef ();
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing || SafeHandle == null)
				return;

			SafeHandle.Dispose ();
			SafeHandle  = null;

			members     = null;

			if (arguments == null)
				return;

			for (int i = 0; i < arguments.Count; ++i) {
				arguments [i].Dispose ();
				arguments [i] = null;
			}
			arguments   = null;
		}

		public void LookupArguments ()
		{
			if (arguments != null)
				return;

			var vm  = JniEnvironment.Current.JavaVM;
			var sb  = new StringBuilder ();

			if (!IsConstructor) {
				sb.Append (Name).Append ("\u0000");
			}

			sb.Append ("(");

			var parameters = IsConstructor
				? JavaClassInfo.GetConstructorParameters (SafeHandle)
				: JavaClassInfo.GetMethodParameters (SafeHandle);
			using (parameters) {
				int len     = JniEnvironment.Arrays.GetArrayLength (parameters);
				arguments   = new List<JniGlobalReference> (len);
				for (int i = 0; i < len; ++i) {
					using (var p = JniEnvironment.Arrays.GetObjectArrayElement (parameters, i)) {
						sb.Append (JniEnvironment.Types.GetJniTypeNameFromClass (p));
						arguments.Add (p.NewGlobalRef ());
					}
				}
			}
			sb.Append (")").Append (JniReturnType);
			JniSignature    = sb.ToString ();
		}

		public bool CompatibleWith (List<JniType> args, DynamicMetaObject[] dargs)
		{
			LookupArguments ();

			if (args.Count != arguments.Count)
				return false;

			var vm = JniEnvironment.Current.JavaVM;

			for (int i = 0; i < arguments.Count; ++i) {
				Debug.WriteLine ("# jonp: JavaMethodBase.CompatibleWith: arguments[{0}]={1} == {2} {3}",
					i, JniEnvironment.Types.GetJniTypeNameFromClass (arguments [i]), args [i], dargs [i].LimitType);
				if (args [i] == null) {
					// Builtin type -- JNIEnv.FindClass("I") throws!
					if (JniEnvironment.Types.GetJniTypeNameFromClass (arguments [i]) != vm.GetJniTypeInfoForType (dargs [i].LimitType).JniTypeReference)
						return false;
				}
				else if (!JniEnvironment.Types.IsAssignableFrom (arguments [i], args [i].SafeHandle))
					return false;
			}
			return true;
		}
	}

}
