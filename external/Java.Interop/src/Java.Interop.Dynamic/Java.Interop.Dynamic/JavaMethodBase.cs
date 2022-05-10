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

using Java.Interop;

namespace Java.Interop.Dynamic {

	abstract class JavaMethodBase : JavaMemberInfo {

		public      abstract    bool        IsConstructor   {get;}

		public      abstract unsafe object? Invoke (IJavaPeerable? self, JniArgumentValue* arguments);

		protected   abstract    string      JniReturnType   {get;}

		public      JniObjectReference      PeerReference   {get; private set;}
		public      string?                 JniSignature    {get; private set;}

		protected   JniPeerMembers          members;

		List<JniObjectReference>?           arguments;
		public  List<JniObjectReference>    ArgumentTypes {
			get {
				LookupArguments ();
				return arguments!;
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
			JniObjectReference.Dispose (ref pr);
			PeerReference   = pr;

			members     = null!;

			if (arguments == null)
				return;

			for (int i = 0; i < arguments.Count; ++i) {
				var a = arguments [i];
				JniObjectReference.Dispose (ref a);
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
			var mgr = vm.TypeManager;

			if (!IsConstructor) {
				sb.Append (Name).Append (".");
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
						var typeName = JniEnvironment.Types.GetJniTypeNameFromClass (p) ??
							throw new NotSupportedException ($"Could not determine class for {p}.");
						var sig = JniTypeSignature.Parse (typeName);
						sb.Append (sig.QualifiedReference);
						arguments.Add (p.NewGlobalRef ());
					} finally {
						JniObjectReference.Dispose (ref p);
					}
				}
			} finally {
				JniObjectReference.Dispose (ref parameters);
			}
			sb.Append (")").Append (JniReturnType);
			JniSignature    = sb.ToString ();
		}

		public bool CompatibleWith (List<JniType?> argumentTypes, DynamicMetaObject[] argumentValues)
		{
			LookupArguments ();

			if (argumentTypes.Count != arguments?.Count)
				return false;

			var vm = JniEnvironment.Runtime;

			for (int i = 0; i < arguments.Count; ++i) {
				if (argumentTypes [i] == null) {
					// Builtin type -- JNIEnv.FindClass("I") throws!
					if (JniEnvironment.Types.GetJniTypeNameFromClass (arguments [i]) != vm.TypeManager.GetTypeSignature (argumentValues [i].LimitType).Name)
						return false;
				}
				else if (!JniEnvironment.Types.IsAssignableFrom (arguments [i], argumentTypes [i]?.PeerReference ?? default))
					return false;
			}
			return true;
		}

		public override string ToString ()
		{
			return string.Format ("{0}{1}({2}) -> {3}",
					IsStatic ? "static " : "",
					Name,
					string.Join (", ", ArgumentTypes.Select (a => JniEnvironment.Types.GetJniTypeNameFromClass (a))),
					JniReturnType);
		}
	}

}
