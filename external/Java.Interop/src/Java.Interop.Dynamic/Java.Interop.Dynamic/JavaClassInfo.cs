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

	class JavaClassInfo : IDisposable {

		static  readonly    Func<string, Type, JniPeerMembers>  CreatePeerMembers;

		static  readonly    JniMethodInfo                       Class_getConstructors;
		static  readonly    JniMethodInfo                       Class_getFields;
		static  readonly    JniMethodInfo                       Class_getMethods;

		static  readonly    JniMethodInfo                       Constructor_getParameterTypes;

		static  readonly    JniMethodInfo                       Field_getName;
		static  readonly    JniMethodInfo                       Field_getType;

		static  readonly    JniMethodInfo                       Method_getName;
		static  readonly    JniMethodInfo                       Method_getReturnType;
		static  readonly    JniMethodInfo                       Method_getParameterTypes;

		static  readonly    JniMethodInfo                       Member_getModifiers;

		static  readonly    Dictionary<string, WeakReference>   Classes = new Dictionary<string, WeakReference> ();

		static JavaClassInfo ()
		{
			CreatePeerMembers = (Func<string, Type, JniPeerMembers>) typeof (JniPeerMembers)
				.GetTypeInfo ()
				.GetDeclaredMethod ("CreatePeerMembers")
				.CreateDelegate (typeof (Func<string, Type, JniPeerMembers>));
			if (CreatePeerMembers == null)
				throw new NotSupportedException ("Could not find JniPeerMembers.CreatePeerMembers!");

			using (var t = new JniType ("java/lang/Class")) {
				Class_getConstructors   = t.GetInstanceMethod ("getConstructors", "()[Ljava/lang/reflect/Constructor;");
				Class_getFields         = t.GetInstanceMethod ("getFields", "()[Ljava/lang/reflect/Field;");
				Class_getMethods        = t.GetInstanceMethod ("getMethods", "()[Ljava/lang/reflect/Method;");
			}
			using (var t = new JniType ("java/lang/reflect/Constructor")) {
				Constructor_getParameterTypes   = t.GetInstanceMethod ("getParameterTypes", "()[Ljava/lang/Class;");
			}
			using (var t = new JniType ("java/lang/reflect/Field")) {
				Field_getName   = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				Field_getType   = t.GetInstanceMethod ("getType", "()Ljava/lang/Class;");
			}
			using (var t = new JniType ("java/lang/reflect/Method")) {
				Method_getName              = t.GetInstanceMethod ("getName", "()Ljava/lang/String;");
				Method_getParameterTypes    = t.GetInstanceMethod ("getParameterTypes", "()[Ljava/lang/Class;");
				Method_getReturnType        = t.GetInstanceMethod ("getReturnType", "()Ljava/lang/Class;");
			}
			using (var t = new JniType ("java/lang/reflect/Member")) {
				Member_getModifiers     = t.GetInstanceMethod ("getModifiers", "()I");
			}
		}

		public  static  JavaClassInfo   GetClassInfo (string jniClassName)
		{
			lock (Classes) {
				JavaClassInfo? info = _GetClassInfo (jniClassName);
				if (info != null) {
					Interlocked.Increment (ref info.RefCount);
					return info;
				}

				info    = new JavaClassInfo (jniClassName);
				Classes.Add (jniClassName, new WeakReference (info));
				return info;
			}
		}

		static JavaClassInfo? _GetClassInfo (string jniClassName)
		{
			lock (Classes) {
				WeakReference value;
				if (Classes.TryGetValue (jniClassName, out value)) {
					var info    = (JavaClassInfo) value.Target;
					if (info != null) {
						return info;
					}
					Classes.Remove (jniClassName);
				}

				return null;
			}
		}

		// For testing
		public  static  int GetClassInfoCount (string jniClassName)
		{
			lock (Classes) {
				var info    = _GetClassInfo (jniClassName);
				if (info != null)
					return info.RefCount;
				return -1;
			}
		}

		public      string              JniClassName        {get; private set;}

		internal    bool                Disposed;
		internal    JniPeerMembers      Members;

		int         RefCount    = 1;

		List<JavaConstructorInfo>?                      constructors;
		Dictionary<string, List<JavaFieldInfo>>?        fields;
		Dictionary<string, List<JavaMethodInfo>>?       methods;

		public  List<JavaConstructorInfo>?              Constructors {
			get {return LookupConstructors ();}
		}

		public  Dictionary<string, List<JavaFieldInfo>>?    Fields {
			get {return LookupFields ();}
		}

		public Dictionary<string, List<JavaMethodInfo>>?    Methods {
			get {return LookupMethods ();}
		}


		JavaClassInfo (string jniClassName)
		{
			if (jniClassName == null)
				throw new ArgumentNullException ("jniClassName");

			JniClassName    = jniClassName;
			Members         = CreatePeerMembers (jniClassName, typeof (JavaInstanceProxy));
		}

		public void Dispose ()
		{
			lock (Classes) {
				if (Interlocked.Decrement (ref RefCount) != 0)
					return;

				Disposed = true;

				if (methods != null) {
					foreach (var name in methods.Keys.ToList ()) {
						foreach (var info in methods [name])
							info.Dispose ();
						methods.Remove (name);
					}
				}

				JniPeerMembers.Dispose (Members);
				Classes.Remove (JniClassName);
			}
		}

		internal static JniObjectReference GetMethodParameters (JniObjectReference method)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (method, Method_getParameterTypes);
		}

		internal static JniObjectReference GetConstructorParameters (JniObjectReference method)
		{
			return JniEnvironment.InstanceMethods.CallObjectMethod (method, Constructor_getParameterTypes);
		}

		List<JavaConstructorInfo>? LookupConstructors ()
		{
			if (Members == null)
				return null;

			lock (Members) {
				if (constructors != null || Disposed)
					return constructors;

				constructors    = new List<JavaConstructorInfo> ();

				var ctors = JniEnvironment.InstanceMethods.CallObjectMethod (Members.JniPeerType.PeerReference, Class_getConstructors);
				try {
					int len     = JniEnvironment.Arrays.GetArrayLength (ctors);
					for (int i  = 0; i < len; ++i) {
						var ctor    = JniEnvironment.Arrays.GetObjectArrayElement (ctors, i);
						var m       = new JavaConstructorInfo (Members, ctor);
						constructors.Add (m);
						JniObjectReference.Dispose (ref ctor);
					}
				} finally {
					JniObjectReference.Dispose (ref ctors);
				}

				return constructors;
			}
		}

		Dictionary<string, List<JavaFieldInfo>>? LookupFields ()
		{
			if (Members == null)
				return null;

			lock (Members) {
				if (this.fields != null || Disposed)
					return this.fields;

				this.fields = new Dictionary<string, List<JavaFieldInfo>> ();

				var fields = JniEnvironment.InstanceMethods.CallObjectMethod (Members.JniPeerType.PeerReference, Class_getFields);
				try {
					int len     = JniEnvironment.Arrays.GetArrayLength (fields);
					for (int i  = 0; i < len; ++i) {
						var field       = JniEnvironment.Arrays.GetObjectArrayElement (fields, i);
						var n_name      = JniEnvironment.InstanceMethods.CallObjectMethod (field, Field_getName);
						var isStatic    = IsStatic (field);
						var name        = JniEnvironment.Strings.ToString (ref n_name, JniObjectReferenceOptions.CopyAndDispose) ??
							throw new InvalidOperationException ($"Could not determine field name at index {i}!");

						List<JavaFieldInfo>? overloads = null;
						if (!Fields?.TryGetValue (name, out overloads) ?? false)
							Fields!.Add (name, overloads = new List<JavaFieldInfo> ());

						var n_type      = JniEnvironment.InstanceMethods.CallObjectMethod (field, Field_getType);
						using (var type = new JniType (ref n_type, JniObjectReferenceOptions.CopyAndDispose)) {
							var sig = JniTypeSignature.Parse (type.Name);
							overloads?.Add (new JavaFieldInfo (Members, name + "." + sig.QualifiedReference, isStatic));
						}

						JniObjectReference.Dispose (ref field);
					}
				} finally {
					JniObjectReference.Dispose (ref fields);
				}

				return this.fields;
			}
		}

		Dictionary<string, List<JavaMethodInfo>>? LookupMethods ()
		{
			if (Members == null)
				return null;

			lock (Members) {
				if (this.methods != null || Disposed)
					return this.methods;

				this.methods = new Dictionary<string, List<JavaMethodInfo>> ();

				var methods  = JniEnvironment.InstanceMethods.CallObjectMethod (Members.JniPeerType.PeerReference, Class_getMethods);
				try {
					int len     = JniEnvironment.Arrays.GetArrayLength (methods);
					for (int i  = 0; i < len; ++i) {
						var method      = JniEnvironment.Arrays.GetObjectArrayElement (methods, i);
						var n_name      = JniEnvironment.InstanceMethods.CallObjectMethod (method, Method_getName);
						var isStatic    = IsStatic (method);
						var name        = JniEnvironment.Strings.ToString (ref n_name, JniObjectReferenceOptions.CopyAndDispose) ??
							throw new InvalidOperationException ($"Could not determine method name at index {i}!");

						List<JavaMethodInfo>? overloads = null;
						if (!Methods?.TryGetValue (name, out overloads) ?? false)
							Methods!.Add (name, overloads = new List<JavaMethodInfo> ());

						var nrt = JniEnvironment.InstanceMethods.CallObjectMethod (method, Method_getReturnType);
						var rt  = new JniType (ref nrt, JniObjectReferenceOptions.CopyAndDispose);
						var m   = new JavaMethodInfo (Members, method, name, isStatic) {
							ReturnType  = rt,
						};
						overloads?.Add (m);
						JniObjectReference.Dispose (ref method);
					}
				} finally {
					JniObjectReference.Dispose (ref methods);
				}

				return this.methods;
			}
		}

		static bool IsStatic (JniObjectReference member)
		{
			var s   = JniEnvironment.InstanceMethods.CallIntMethod (member, Member_getModifiers);

			return (s & JavaModifiers.Static) == JavaModifiers.Static;
		}

		internal unsafe bool TryInvokeMember (IJavaPeerable self, JavaMethodBase[] overloads, DynamicMetaObject[] args, out object? value)
		{
			value       = null;
			var vms     = (List<JniValueMarshaler>?) null;
			var states  = (JniValueMarshalerState[]?) null;

			var jtypes  = GetJniTypes (args);
			try {
				var matches = overloads.Where (o => (o.IsConstructor || o.IsStatic == (self == null)) && o.CompatibleWith (jtypes, args));
				var invoke  = matches.FirstOrDefault ();
				if (invoke == null)
					return false;

				var jvm     = JniEnvironment.Runtime;
				vms         = args.Select (arg => jvm.ValueManager.GetValueMarshaler (arg.LimitType)).ToList ();
				states      = new JniValueMarshalerState [vms.Count];
				for (int i = 0; i < vms.Count; ++i) {
					states [i]  = vms [i].CreateArgumentState (args [i].Value);
				}
				var jargs   = stackalloc JniArgumentValue [vms.Count];
				for (int i = 0; i < states.Length; ++i)
					jargs [i] = states [i].JniArgumentValue;
				value       = invoke.Invoke (self, jargs);
				return true;
			}
			finally {
				for (int i = 0; vms != null && i < vms.Count; ++i) {
					if (states == null) {
						continue;
					}
					vms [i].DestroyArgumentState (args [i].Value, ref states [i]);
				}
				for (int i = 0; i < jtypes.Count; ++i) {
					jtypes [i]?.Dispose ();
				}
			}
		}

		static List<JniType?> GetJniTypes (DynamicMetaObject[] args)
		{
			var r   = new List<JniType?> (args.Length);
			var vm  = JniEnvironment.Runtime;
			foreach (var a in args) {
				try {
					var at  = new JniType (vm.TypeManager.GetTypeSignature (a.LimitType).Name);
					r.Add (at);
				} catch (JavaException e) {
					e.Dispose ();
					r.Add (null);
				} catch {
					r.Add (null);
				}
			}
			return r;
		}
	}
}

