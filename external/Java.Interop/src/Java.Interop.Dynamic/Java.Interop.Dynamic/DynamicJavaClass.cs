using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Java.Interop;

using Mono.Linq.Expressions;

namespace Java.Interop.Dynamic {

	public class DynamicJavaClass : IDynamicMetaObjectProvider, IDisposable
	{
		readonly    static  Func<string, JniPeerMembers>    CreatePeerMembers;

		readonly    static  JniInstanceMethodID                 Class_getConstructors;
		readonly    static  JniInstanceMethodID                 Class_getFields;
		readonly    static  JniInstanceMethodID                 Class_getMethods;

		readonly    static  JniInstanceMethodID                 Constructor_getParameterTypes;

		readonly    static  JniInstanceMethodID                 Field_getName;
		readonly    static  JniInstanceMethodID                 Field_getType;

		readonly    static  JniInstanceMethodID                 Method_getName;
		readonly    static  JniInstanceMethodID                 Method_getReturnType;

		readonly    static  internal    JniInstanceMethodID     Method_getParameterTypes;

		readonly    static  JniInstanceMethodID                 Member_getModifiers;

		static DynamicJavaClass ()
		{
			CreatePeerMembers = (Func<string, JniPeerMembers>)
				Delegate.CreateDelegate (
					typeof(Func<string, JniPeerMembers>),
					typeof(JniPeerMembers).GetMethod ("CreatePeerMembers", BindingFlags.NonPublic | BindingFlags.Static));
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

		public  string          JniClassName            {get; private set;}

		JniPeerMembers          members;
		bool                    disposed;

		Dictionary<string, HashSet<string>>                 StaticFields;
		Dictionary<string, List<JavaMethodInvokeInfo>>      StaticMethods;

		public DynamicJavaClass (string jniClassName)
		{
			if (jniClassName == null)
				throw new ArgumentNullException ("jniClassName");

			JniClassName    = jniClassName;
			members         = CreatePeerMembers (jniClassName);
		}

		public void Dispose ()
		{
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (!disposing)
				return;

			if (disposed)
				return;

			foreach (var name in StaticMethods.Keys.ToList ()) {
				foreach (var info in StaticMethods [name])
					info.Dispose ();
				StaticMethods [name]    = null;
			}

			JniPeerMembers.Dispose (members);
			members     = null;

			disposed    = true;
		}

		void LookupMethods ()
		{
			if (StaticMethods != null || disposed)
				return;

			StaticMethods   = new Dictionary<string, List<JavaMethodInvokeInfo>> ();

			using (var methods = new JavaObjectArray<JavaObject> (Class_getMethods.CallVirtualObjectMethod (members.JniPeerType.SafeHandle), JniHandleOwnership.Transfer)) {
				foreach (var method in methods) {
					var s = Member_getModifiers.CallVirtualInt32Method (method.SafeHandle);
					if ((s & JavaModifiers.Static) != JavaModifiers.Static) {
						method.Dispose ();
						continue;
					}

					var name = JniEnvironment.Strings.ToString (Method_getName.CallVirtualObjectMethod (method.SafeHandle), JniHandleOwnership.Transfer);

					List<JavaMethodInvokeInfo> overloads;
					if (!StaticMethods.TryGetValue (name, out overloads))
						StaticMethods.Add (name, overloads = new List<JavaMethodInvokeInfo> ());

					var rt = new JniType (Method_getReturnType.CallVirtualObjectMethod (method.SafeHandle), JniHandleOwnership.Transfer);
					var m = new JavaMethodInvokeInfo (name, true, rt, method);
					overloads.Add (m);
				}
			}
		}

		void LookupFields ()
		{
			if (StaticFields != null || disposed)
				return;

			StaticFields    = new Dictionary<string, HashSet<string>> ();

			using (var fields = new JavaObjectArray<JavaObject> (Class_getFields.CallVirtualObjectMethod (members.JniPeerType.SafeHandle), JniHandleOwnership.Transfer)) {
				foreach (var field in fields) {
					var s = Member_getModifiers.CallVirtualInt32Method (field.SafeHandle);
					if ((s & JavaModifiers.Static) != JavaModifiers.Static) {
						field.Dispose ();
						continue;
					}

					var name = JniEnvironment.Strings.ToString (Field_getName.CallVirtualObjectMethod (field.SafeHandle), JniHandleOwnership.Transfer);

					HashSet<string> overloads;
					if (!StaticFields.TryGetValue (name, out overloads))
						StaticFields.Add (name, overloads = new HashSet<string> ());

					using (var type = new JniType (Field_getType.CallVirtualObjectMethod (field.SafeHandle), JniHandleOwnership.Transfer)) {
						var info = JniEnvironment.Current.JavaVM.GetJniTypeInfoForJniTypeReference (type.Name);
						overloads.Add (name + "\u0000" + info.JniTypeReference);
					}

					field.Dispose ();
				}
			}
		}

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new MetaObject (parameter, this);
		}

		internal bool TryInvokeStaticMember (string name, DynamicMetaObject[] args, out object value)
		{
			value       = null;
			var margs   = (List<JniArgumentMarshalInfo>) null;
			List<JavaMethodInvokeInfo> overloads;
			if (!StaticMethods.TryGetValue (name, out overloads))
				throw new InvalidOperationException ("Should not have reached InvokeStaticMember when there is no overload found for method '" + name + "'!");

			var jtypes  = GetJniTypes (args);
			try {
				var matches = overloads.Where (o => o.CompatibleWith (jtypes, args));
				var invoke  = matches.FirstOrDefault ();
				if (invoke == null)
					return false;

				margs       = args.Select (arg => new JniArgumentMarshalInfo (arg.Value, arg.LimitType)).ToList ();
				var jvalues = margs.Select (a => a.JValue).ToArray ();
				value = members.StaticMethods.CallMethod (invoke.Signature, jvalues);
				return true;
			}
			finally {
				for (int i = 0; margs != null && i < margs.Count; ++i) {
					margs [i].Cleanup (args [i]);
				}
				for (int i = 0; i < jtypes.Count; ++i) {
					if (jtypes [i] != null)
						jtypes [i].Dispose ();
				}
			}
		}

		static List<JniType> GetJniTypes (DynamicMetaObject[] args)
		{
			var r   = new List<JniType> (args.Length);
			var vm  = JniEnvironment.Current.JavaVM;
			foreach (var a in args) {
				try {
					var at  = new JniType (vm.GetJniTypeInfoForType (a.LimitType).JniTypeReference);
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

#if false
		Type CreateManagedPeerType ()
		{
			var className   = JniClassName.Replace ('/', '-');
			var aname       = new AssemblyName ("Java.Interop.Dynamic-" + className);

			var assembly    = AppDomain.CurrentDomain.DefineDynamicAssembly (aname, AssemblyBuilderAccess.ReflectionOnly);
			var module      = assembly.DefineDynamicModule (className);
			var type        = module.DefineType (JniClassName, TypeAttributes.Sealed, typeof (JavaObject));
			type.SetCustomAttribute (
					new CustomAttributeBuilder (
						typeof (JniTypeInfoAttribute).GetConstructor (new[]{ typeof(string) }),
						new []{JniClassName}));

			return type.CreateType ();
		}
#endif

		class MetaObject : DynamicMetaObject<DynamicJavaClass>
		{
			delegate bool TryInvokeStaticMember (string name, DynamicMetaObject[] args, out object value);

			public MetaObject (Expression parameter, DynamicJavaClass value)
				: base (parameter, value)
			{
				//			Console.WriteLine ("# MyMetaObject..ctor: paramter={0} {1} {2}", parameter.ToCSharpCode (), parameter.GetType (), parameter.Type);
				Debug.WriteLine ("# MyMetaObject..ctor: value={0} {1}", value, value.GetType ());
			}

			public override IEnumerable<string> GetDynamicMemberNames ()
			{
				return Value.StaticFields.Keys.Concat (
					Value.StaticMethods.Keys
				);
			}

			public override DynamicMetaObject BindGetMember (GetMemberBinder binder)
			{
				HashSet<string> overloads = GetField (binder.Name);
				if (overloads == null)
					return binder.FallbackGetMember (this);

				if (Value.disposed) {
					return new DynamicMetaObject (ThrowObjectDisposedException (typeof (object)), BindingRestrictions.GetInstanceRestriction (Expression, Value));
				}

				Func<string, object>    getValue    = Value.members.StaticFields.GetValue;

				var e = Expression.Call (Expression.Constant (Value.members.StaticFields), getValue.Method, Expression.Constant (overloads.First ()));
				Debug.WriteLine ("# MetaObject.BindGetMember: e={0}", e.ToCSharpCode ());
				return new DynamicMetaObject (e, BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}

			static Expression ThrowObjectDisposedException (Type type = null)
			{
				return Expression.Throw (Expression.Constant (new ObjectDisposedException (nameof (DynamicJavaClass))), type);
			}

			HashSet<string> GetField (string name)
			{
				Value.LookupFields ();

				HashSet<string> overloads;
				if (Value.StaticFields != null && Value.StaticFields.TryGetValue (name, out overloads))
					return overloads;
				return null;
			}

			public override DynamicMetaObject BindInvokeMember (InvokeMemberBinder binder, DynamicMetaObject[] args)
			{
				Value.LookupMethods ();
				List<JavaMethodInvokeInfo> overloads    = null;
				if (Value.StaticMethods != null && !Value.StaticMethods.TryGetValue (binder.Name, out overloads))
					return binder.FallbackInvokeMember (this, args);

				if (Value.disposed) {
					return new DynamicMetaObject (ThrowObjectDisposedException (typeof (object)), BindingRestrictions.GetInstanceRestriction (Expression, Value));
				}

				foreach (var m in overloads)
					m.LookupArguments ();

				if (!overloads.Any (o => o.Arguments.Count == args.Length))
					return binder.FallbackInvokeMember (this, args);

				TryInvokeStaticMember   invoke  = Value.TryInvokeStaticMember;
				var value       = Expression.Parameter (typeof (object), "value");
				var fallback    = binder.FallbackInvokeMember (this, args);
				Debug.WriteLine ("DynamicJavaClass.MetaObject.BindConvert: Fallback={0}", fallback.Expression.ToCSharpCode ());
				var call        = Expression.Block (
						new[]{value},
						Expression.Condition (
							test:       Expression.Call (ExpressionAsT, invoke.Method, Expression.Constant (binder.Name), Expression.Constant (args), value),
							ifTrue:     value,
							ifFalse:    fallback.Expression)
				);
				return new DynamicMetaObject (call, BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}

			public override DynamicMetaObject BindSetMember (SetMemberBinder binder, DynamicMetaObject value)
			{
				HashSet<string> overloads = GetField (binder.Name);
				if (overloads == null)
					return binder.FallbackSetMember (this, value);

				if (Value.disposed) {
					return new DynamicMetaObject (ThrowObjectDisposedException (), BindingRestrictions.GetInstanceRestriction (Expression, Value));
				}

				Action<string, object>  setValue    = Value.members.StaticFields.SetValue;
				var e = Expression.Block (
						Expression.Call (Expression.Constant (Value.members.StaticFields), setValue.Method,
                            Expression.Constant (overloads.First ()), Expression.Convert (value.Expression, typeof(object))),
                        ExpressionAsT);
				Debug.WriteLine ("# MetaObject.BindSetMember: e={0}", e.ToCSharpCode ());
				return new DynamicMetaObject (e, BindingRestrictions.GetInstanceRestriction (Expression, Value));
			}
		}
	}

	static class JavaModifiers {
		public  static  readonly    int     Static;

		static JavaModifiers ()
		{
			using (var t = new JniType ("java/lang/reflect/Modifier")) {
				using (var s = t.GetStaticField ("STATIC", "I"))
					Static  = s.GetInt32Value (t.SafeHandle);
			}
		}
	}

	sealed class JavaMethodInvokeInfo : IDisposable {

		public  string          Name;
		public  JniType         ReturnType;
		public  List<JniType>   Arguments;
		public  bool            IsStatic;
		public  JavaObject      Method;

		public  string          Signature;

		public JavaMethodInvokeInfo (string name, bool isStatic, JniType returnType, JavaObject method)
		{
			Name            = name;
			IsStatic        = isStatic;
			ReturnType      = returnType;
			Method          = method;
		}

		public void Dispose ()
		{
			if (ReturnType == null)
				return;

			Method.Dispose ();
			ReturnType.Dispose ();
			ReturnType  = null;

			if (Arguments == null)
				return;

			for (int i = 0; i < Arguments.Count; ++i) {
				if (Arguments [i] == null)
					continue;
				Arguments [i].Dispose ();
				Arguments [i] = null;
			}
			Arguments   = null;
		}

		public void LookupArguments ()
		{
			if (Arguments != null)
				return;

			var vm  = JniEnvironment.Current.JavaVM;
			var sb  = new StringBuilder ();
			sb.Append (Name).Append ("\u0000").Append ("(");

			Arguments   = new List<JniType> ();
			using (var methodParams = new JavaObjectArray<JavaObject> (DynamicJavaClass.Method_getParameterTypes.CallVirtualObjectMethod (Method.SafeHandle), JniHandleOwnership.Transfer)) {
				foreach (var p in methodParams) {
					var pt  = new JniType (p.SafeHandle, JniHandleOwnership.DoNotTransfer);
					Arguments.Add (pt);
					sb.Append (vm.GetJniTypeInfoForJniTypeReference (pt.Name).JniTypeReference);
					p.Dispose ();
				}
			}
			sb.Append (")").Append (vm.GetJniTypeInfoForJniTypeReference (ReturnType.Name).JniTypeReference);
			Signature   = sb.ToString ();
		}

		public bool CompatibleWith (List<JniType> args, DynamicMetaObject[] dargs)
		{
			LookupArguments ();

			if (args.Count != Arguments.Count)
				return false;

			var vm = JniEnvironment.Current.JavaVM;

			for (int i = 0; i < Arguments.Count; ++i) {
				if (args [i] == null) {
					// Builtin type -- JNIEnv.FindClass("I") throws!
					if (Arguments [i].Name != vm.GetJniTypeInfoForType (dargs [i].LimitType).JniTypeReference)
						return false;
				}
				else if (!Arguments [i].IsAssignableFrom (args [i]))
					return false;
			}
			return true;
		}
	}

	class DynamicMetaObject<T> : DynamicMetaObject {

		public new T Value {
			get {return (T) base.Value;}
		}

		public Expression ExpressionAsT {
			get {return Expression.Convert (Expression, typeof (T));}
		}

		public DynamicMetaObject (Expression parameter, T value)
			: base (parameter, BindingRestrictions.GetInstanceRestriction (parameter, value), value)
		{
		}
	}

	struct JniArgumentMarshalInfo {
		JValue                          jvalue;
		JniLocalReference               lref;
		IJavaObject                     obj;
		Action<IJavaObject, object>     cleanup;

		internal JniArgumentMarshalInfo (object value, Type valueType)
		{
			this        = new JniArgumentMarshalInfo ();
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (valueType);
			if (info.CreateJValue != null)
				jvalue = info.CreateJValue (value);
			else if (info.CreateMarshalCollection != null) {
				obj     = info.CreateMarshalCollection (value);
				jvalue  = new JValue (obj);
			} else if (info.CreateLocalRef != null) {
				lref    = info.CreateLocalRef (value);
				jvalue  = new JValue (lref);
			} else
				throw new NotSupportedException ("Don't know how to get a JValue for: " + valueType.FullName);
			cleanup     = info.CleanupMarshalCollection;
		}

		public JValue JValue {
			get {return jvalue;}
		}

		public  void    Cleanup (object value)
		{
			if (cleanup != null && obj != null)
				cleanup (obj, value);
			if (lref != null)
				lref.Dispose ();
		}
	}
}

