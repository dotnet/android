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

		public      abstract unsafe object  Invoke (IJavaPeerable self, JValue* arguments);

		protected   abstract    string      JniReturnType   {get;}

		public      JniObjectReference      PeerReference   {get; private set;}
		public      string                  JniSignature    {get; private set;}

		protected   JniPeerMembers          members;

		List<JniObjectReference>            arguments;
		public  List<JniObjectReference>    ArgumentTypes {
			get {
				LookupArguments ();
				return arguments;
			}
		}

		public  string  JniDeclaringClassName {
			get { return members == null ? "" : members.JniPeerTypeName; }
		}

		public JavaMethodBase (JniPeerMembers members, JniObjectReference method)
		{
			this.members    = members;
			PeerReference   = method.NewGlobalRef ();
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing || !PeerReference.IsValid)
				return;

			var pr          = PeerReference;
			JniEnvironment.References.Dispose (ref pr);
			PeerReference   = pr;

			members     = null;

			if (arguments == null)
				return;

			for (int i = 0; i < arguments.Count; ++i) {
				var a = arguments [i];
				JniEnvironment.References.Dispose (ref a);
				arguments [i] = a;
			}
			arguments   = null;
		}

		public void LookupArguments ()
		{
			if (arguments != null)
				return;

			var vm  = JniEnvironment.Runtime;
			var sb  = new StringBuilder ();

			if (!IsConstructor) {
				sb.Append (Name).Append ("\u0000");
			}

			sb.Append ("(");

			var parameters = IsConstructor
				? JavaClassInfo.GetConstructorParameters (PeerReference)
				: JavaClassInfo.GetMethodParameters (PeerReference);
			try {
				int len     = JniEnvironment.Arrays.GetArrayLength (parameters);
				arguments   = new List<JniObjectReference> (len);
				for (int i = 0; i < len; ++i) {
					var p = JniEnvironment.Arrays.GetObjectArrayElement (parameters, i);
					try {
						sb.Append (JniEnvironment.Types.GetJniTypeNameFromClass (p));
						arguments.Add (p.NewGlobalRef ());
					} finally {
						JniEnvironment.References.Dispose (ref p);
					}
				}
			} finally {
				JniEnvironment.References.Dispose (ref parameters);
			}
			sb.Append (")").Append (JniReturnType);
			JniSignature    = sb.ToString ();
		}

		public bool CompatibleWith (List<JniType> args, DynamicMetaObject[] dargs)
		{
			LookupArguments ();

			if (args.Count != arguments.Count)
				return false;

			var vm = JniEnvironment.Runtime;

			for (int i = 0; i < arguments.Count; ++i) {
				if (args [i] == null) {
					// Builtin type -- JNIEnv.FindClass("I") throws!
					if (JniEnvironment.Types.GetJniTypeNameFromClass (arguments [i]) != vm.GetJniTypeInfoForType (dargs [i].LimitType).QualifiedReference)
						return false;
				}
				else if (!JniEnvironment.Types.IsAssignableFrom (arguments [i], args [i].PeerReference))
					return false;
			}
			return true;
		}
	}

}
