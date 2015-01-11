using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using Mono.Linq.Expressions;

using Java.Interop;

namespace Java.Interop.Dynamic {

	public class DynamicJavaClass : IDynamicMetaObjectProvider
	{
		public  string          JniClassName            {get; private set;}

		JniPeerMembers          members;

		public DynamicJavaClass (string jniClassName)
		{
			if (jniClassName == null)
				throw new ArgumentNullException ("jniClassName");

			JniClassName    = jniClassName;
			members         = new JniPeerMembers (jniClassName, CreateManagedPeerType ());
		}

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new MetaStaticMemberAccessObject (parameter, this);
		}

		public object GetStaticFieldValue (string fieldName, Type fieldType)
		{
			Console.WriteLine ("# DynamicJavaClass({0}).field({1}) as {2}", JniClassName, fieldName, fieldType);
			var typeInfo    = JniEnvironment.Current.JavaVM.GetJniTypeInfoForJniTypeReference (JniClassName);
			switch (typeInfo.ToString ()) {
			case "I":
			case "java/lang/Integer":   // WTF?
				return members.StaticFields.GetInt32Value (fieldName + "\u0000I");
			}
			return null;
		}

		internal StaticFieldAccess GetStaticFieldAccess (string fieldName)
		{
			return new StaticFieldAccess (this, fieldName);
		}

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
	}

	class MetaStaticMemberAccessObject : DynamicMetaObject
	{
		public MetaStaticMemberAccessObject (Expression parameter, object value)
			: base(parameter, BindingRestrictions.Empty, value)
		{
			Console.WriteLine ("# MyMetaObject..ctor: paramter={0} {1} {2}", parameter.ToCSharpCode (), parameter.GetType (), parameter.Type);
			Console.WriteLine ("# MyMetaObject..ctor: value={0} {1}", value, value.GetType ());
		}

		public override DynamicMetaObject BindConvert(ConvertBinder binder)
		{
			Console.WriteLine ("Convert: Expression={0} [{1}]", Expression.ToCSharpCode (), Expression.Type);
			return this.PrintAndReturnIdentity("Convert: Explicit={0}; ReturnType={1}; Type={2}", binder.Explicit, binder.ReturnType, binder.Type);
		}

		public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
		{
			return this.PrintAndReturnIdentity("InvokeMember of method={0}; ReturnType={1}; args={{{2}}}; CallInfo={3}", binder.Name, binder.ReturnType,
				string.Join (", ", args.Select (a => string.Format ("{0} [{1}]", a.Expression.ToCSharpCode (), a.LimitType))), binder.CallInfo);
		}

		public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
		{
			return this.PrintAndReturnIdentity("SetMember of property {0}", binder.Name);
		}

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
		{
			Console.WriteLine ("GetMember: Expression={0} [{1}]; property={2}", Expression.ToCSharpCode (), Expression.Type, binder.Name);
			var expr =
				Expression.Call (
					Expression.Convert (Expression, typeof(DynamicJavaClass)),
					typeof (DynamicJavaClass).GetMethod ("GetStaticFieldAccess", BindingFlags.Instance | BindingFlags.NonPublic),
					Expression.Constant (binder.Name));
			return new DynamicMetaObject (
				expr,
				BindingRestrictions.GetTypeRestriction (
					expr,
					typeof (StaticFieldAccess)));
		}

		private DynamicMetaObject PrintAndReturnIdentity(string message, params object[] args)
		{
			Console.WriteLine(message, args);
			return new DynamicMetaObject(
				Expression,
				BindingRestrictions.GetTypeRestriction(
					Expression,
					typeof (DynamicJavaClass)));
		}
	}
	class StaticFieldAccess : IDynamicMetaObjectProvider {

		public  string              FieldName   {get; private set;}
		public  DynamicJavaClass    JavaClass   {get; private set;}

		public StaticFieldAccess (DynamicJavaClass klass, string fieldName)
		{
			JavaClass = klass;
			FieldName = fieldName;
		}

		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			Console.WriteLine ("# FieldAccessInfo.GetMetaObject: parameter={0}", parameter);
			return new MetaStaticFieldAccessObject(parameter, this);
		}
	}

	class MetaStaticFieldAccessObject : DynamicMetaObject {

		public MetaStaticFieldAccessObject (Expression e, object value)
			: base (e, BindingRestrictions.Empty, value)
		{
			Console.WriteLine ("MyHelperObject: e={0}", e.ToCSharpCode ());
		}

		public override DynamicMetaObject BindConvert (ConvertBinder binder)
		{
			Console.WriteLine ("MetaStaticFieldAccessObject.Convert: Expression={0} [{1}]", Expression.ToCSharpCode (), Expression.Type);

			var field       = Expression.Convert(Expression, typeof(StaticFieldAccess));
			var instance    = Expression.Property (field, "JavaClass");
			var fieldName   = Expression.Property (field, "FieldName");
			var gsfv        = typeof(DynamicJavaClass).GetMethod ("GetStaticFieldValue");
			var expr        = Expression.Convert (
					Expression.Call (instance, gsfv, fieldName, Expression.Constant (binder.Type)),
                    binder.Type);
			return new DynamicMetaObject (
					expr,
					BindingRestrictions.GetTypeRestriction (
						expr,
						binder.Type));
		}
	}

#if false
	public class DynamicJavaClass : DynamicObject {

		public  string  __JniClassName  {get; private set;}
		JniPeerMembers members;

		public DynamicJavaClass (string jniClassName)
		{
			if (jniClassName == null)
				throw new ArgumentNullException ("jniClassName");
			__JniClassName = jniClassName;
			members = new JniPeerMembers (jniClassName, null);
		}

		public override bool TryInvokeMember (InvokeMemberBinder binder, object[] args, out object result)
		{
			result = null;

			var signature = GetJniSignature (binder, args);
			var arguments = new List<JniArgumentMarshalInfo> (args.Length);
			foreach (var arg in args)
				arguments.Add (new JniArgumentMarshalInfo (arg));
			JValue[] margs = arguments.Select (a => a.JValue).ToArray ();

			try {
				if (binder.ReturnType == typeof (void)) {
					members.StaticMethods.CallVoidMethod (signature, margs);
				} else if (binder.ReturnType == typeof (bool)) {
					result  = members.StaticMethods.CallBooleanMethod (signature, margs);
				} else if (binder.ReturnType == typeof (char)) {
					result  = members.StaticMethods.CallCharMethod (signature, margs);
				} else if (binder.ReturnType == typeof (double)) {
					result  = members.StaticMethods.CallDoubleMethod (signature, margs);
				} else if (binder.ReturnType == typeof (short)) {
					result  = members.StaticMethods.CallInt16Method (signature, margs);
				} else if (binder.ReturnType == typeof (int)) {
					result  = members.StaticMethods.CallInt32Method (signature, margs);
				} else if (binder.ReturnType == typeof (long)) {
					result  = members.StaticMethods.CallInt64Method (signature, margs);
				} else if (binder.ReturnType == typeof (sbyte) || binder.ReturnType == typeof (byte)) {
					result  = members.StaticMethods.CallSByteMethod (signature, margs);
				} else if (binder.ReturnType == typeof (float)) {
					result  = members.StaticMethods.CallSingleMethod (signature, margs);
				} else {
					throw new NotSupportedException ("TODO: DynamicJavaInstance");
					// result  = members.StaticMethods.CallObjectMethod (signature, margs);
				}
				return true;
			} catch (Exception e) {
				Debug.WriteLine ("# jonp: TryInvokeMember: {0}", e);
				result = null;
				for (int i = 0; i < args.Length; ++i) {
					arguments [i].Cleanup (args [i]);
				}
				return false;
			}
		}

		static string GetJniSignature (InvokeMemberBinder binder, object[] args)
		{
			var sb = new StringBuilder ();
			sb.Append (binder.Name);
			sb.Append ("\u0000");
			sb.Append ("(");
			foreach (var arg in args) {
				var argType = arg != null
					? arg.GetType ()
					: typeof (object);
				sb.Append (JavaVM.Current.GetJniTypeInfoForType (argType).ToString ());
			}
			sb.Append (")");
			sb.Append (JavaVM.Current.GetJniTypeInfoForType (binder.ReturnType).ToString ());

			return sb.ToString ();
		}
	}

	struct JniArgumentMarshalInfo {
		JValue                          jvalue;
		JniLocalReference               lref;
		IJavaObject                     obj;
		Action<IJavaObject, object>     cleanup;

		internal JniArgumentMarshalInfo (object value)
		{
			this        = new JniArgumentMarshalInfo ();
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniMarshalInfoForType (value == null ? typeof(void) : value.GetType ());
			if (info.CreateJValue != null)
				jvalue  = info.CreateJValue (value);
			else if (info.CreateMarshalCollection != null) {
				obj     = info.CreateMarshalCollection (value);
				jvalue  = new JValue (obj);
			} else if (info.CreateLocalRef != null) {
				lref    = info.CreateLocalRef (value);
				jvalue  = new JValue (lref);
			} else
				throw new NotSupportedException ("Don't know how to get a JValue for");
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
#endif
}

