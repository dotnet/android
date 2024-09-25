using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Xml;
using Android.Runtime;

using Mono.CodeGeneration;

using ICharSequence = Java.Lang.ICharSequence;

namespace Java.Interop
{
	abstract class CallbackCodeGenerator<TDelegateSpec>
	{
		public abstract TDelegateSpec GetDelegateType ();
		public abstract void GenerateNativeCallbackDelegate ();
	}
	
	[RequiresDynamicCode (MonoAndroidExport.DynamicFeatures)]
	[RequiresUnreferencedCode (MonoAndroidExport.DynamicFeatures)]
	class DynamicInvokeTypeInfo
	{
		// NewArray<T>(T[])
		static readonly MethodInfo jnienv_newarray                        = GetTArrayToIntPtr<int> (JNIEnv.NewArray<int>);
		static readonly MethodInfo jnienv_getarray                        = GetIntPtrToTArray<int> (JNIEnv.GetArray<int>);
		static readonly MethodInfo charsequence_tojnihandle               = GetEnumerableCharToIntPtrMethodInfo (CharSequence.ToLocalJniHandle);
		static readonly MethodInfo jnienv_tojnihandle                     = GetObjectToIntPtrMethodInfo (JNIEnv.ToLocalJniHandle);
		static readonly MethodInfo jnienv_newstring                       = GetStringToIntPtrMethodInfo (JNIEnv.NewString);
		static readonly MethodInfo jnienv_getstring                       = GetHandleToStringMethodInfo (JNIEnv.GetString);
		static readonly MethodInfo object_getobject = typeof (Java.Lang.Object).GetMethod ("GetObject", new[]{typeof (IntPtr), typeof (JniHandleOwnership)});
		static readonly BindingFlags sloppy_flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
		static readonly MethodInfo inputstreaminvoker_fromjnihandle       = GetHandleToStreamMethodInfo (InputStreamInvoker.FromJniHandle);
		static readonly MethodInfo inputstreamadapter_tojnihandle         = GetStreamToIntPtrMethodInfo (InputStreamAdapter.ToLocalJniHandle);
		static readonly MethodInfo outputstreaminvoker_fromjnihandle      = GetHandleToStreamMethodInfo (OutputStreamInvoker.FromJniHandle);
		static readonly MethodInfo outputstreamadapter_tojnihandle        = GetStreamToIntPtrMethodInfo (OutputStreamAdapter.ToLocalJniHandle);
		static readonly MethodInfo xmlpullparserreader_fromjnihandle      = GetHandleToXmlReaderMethodInfo (XmlPullParserReader.FromJniHandle);
		static readonly MethodInfo xmlreaderpullparser_tojnihandle        = GetXmlResourceParserReaderToIntPtrMethodInfo (XmlReaderPullParser.ToLocalJniHandle);
		static readonly MethodInfo xmlresourceparserreader_fromjnihandle  = GetHandleToXmlResourceParserReaderMethodInfo (XmlResourceParserReader.FromJniHandle);
		static readonly MethodInfo xmlreaderresourceparser_tojnihandle    = GetXmlReaderToIntPtrMethodInfo (XmlReaderResourceParser.ToLocalJniHandle);

		static readonly CodeLiteral do_not_transfer_literal = new CodeLiteral (JniHandleOwnership.DoNotTransfer);

		static DynamicInvokeTypeInfo ()
		{
			CheckReflection (jnienv_newarray, "JNIEnv.NewArray<T>");
			CheckReflection (jnienv_getarray, "JNIEnv.GetArray<T>");
			CheckReflection (charsequence_tojnihandle, "ICharSequence.ToLocalJniHandle");
			CheckReflection (jnienv_tojnihandle, "JNIEnv.ToLocalJniHandle");
			CheckReflection (jnienv_newstring, "JNIEnv.NewString");
			CheckReflection (jnienv_getstring, "JNIEnv.GetString");
			CheckReflection (object_getobject, "Java.Lang.Object.GetObject");
			CheckReflection (xmlpullparserreader_fromjnihandle, "XmlPullParserReader.FromJniHandle");
			CheckReflection (xmlresourceparserreader_fromjnihandle, "XmlResourceParserReader.FromJniHandle");
			CheckReflection (xmlreaderpullparser_tojnihandle, "XmlReaderPullParser.ToLocalJniHandle");
			CheckReflection (xmlreaderresourceparser_tojnihandle, "XmlReaderResourceParser.ToLocalJniHandle");
			CheckReflection (inputstreaminvoker_fromjnihandle, "InputStreamInvoker.FromJniHandle");
			CheckReflection (inputstreamadapter_tojnihandle, "InputStreamAdapter.ToLocalJniHandle");
			CheckReflection (outputstreaminvoker_fromjnihandle, "OutputStreamInvoker.FromJniHandle");
			CheckReflection (outputstreamadapter_tojnihandle, "OutputStreamAdapter.ToLocalJniHandle");
		}

		static MethodInfo GetEnumerableCharToIntPtrMethodInfo (Func<IEnumerable<char>, IntPtr> func)
		{
			return func.Method;
		}
		
		static MethodInfo GetObjectToIntPtrMethodInfo (Func<IJavaObject, IntPtr> func)
		{
			return func.Method;
		}

		static MethodInfo GetHandleToIntPtrMethodInfo (Func<IntPtr, JniHandleOwnership, IntPtr> func)
		{
			return func.Method;
		}

		static MethodInfo GetHandleToStreamMethodInfo (Func<IntPtr, JniHandleOwnership, Stream> func)
		{
			return func.Method;
		}

		static MethodInfo GetHandleToStringMethodInfo (Func<IntPtr, JniHandleOwnership, string> func)
		{
			return func.Method;
		}

		static MethodInfo GetHandleToXmlReaderMethodInfo (Func<IntPtr, JniHandleOwnership, XmlReader> func)
		{
			return func.Method;
		}

		static MethodInfo GetHandleToXmlResourceParserReaderMethodInfo (Func<IntPtr, JniHandleOwnership, XmlResourceParserReader> func)
		{
			return func.Method;
		}

		static MethodInfo GetIntPtrToTArray<T>(Func<IntPtr, T[]> func)
		{
			return func.Method.GetGenericMethodDefinition ();
		}

		static MethodInfo GetStreamToIntPtrMethodInfo (Func<Stream, IntPtr> func)
		{
			return func.Method;
		}

		static MethodInfo GetStringToIntPtrMethodInfo (Func<string, IntPtr> func)
		{
			return func.Method;
		}

		static MethodInfo GetTArrayToIntPtr<T>(Func<T[], IntPtr> func)
		{
			return func.Method.GetGenericMethodDefinition ();
		}

		static MethodInfo GetXmlReaderToIntPtrMethodInfo (Func<XmlReader, IntPtr> func)
		{
			return func.Method;
		}

		static MethodInfo GetXmlResourceParserReaderToIntPtrMethodInfo (Func<XmlResourceParserReader, IntPtr> func)
		{
			return func.Method;
		}

		static void CheckReflection (MethodInfo mi, string name)
		{
			if (mi == null)
				throw new InvalidOperationException ("Mono for Android bug: JNIEnv type contains incompatible method signatures : " + name);
		}

		public static DynamicInvokeTypeInfo Get (Type type, ExportParameterKind kind)
		{
			return new DynamicInvokeTypeInfo (type, kind);
		}
		
		private DynamicInvokeTypeInfo (Type type, ExportParameterKind parameterKind)
		{
			this.type = type;
			this.parameter_kind = parameterKind;
		}
		
		Type type;
		ExportParameterKind parameter_kind;
	
		public Type NativeType {
			get { return GetNativeType (type); }
		}
		
		#region IComplexMarhaller support
		
		// IComplexMarshallers are: ArraySymbol, CharSequenceSymbol, StreamSymbol, StringSymbol, XmlPullParserSymbol, XmlResourceParserSymbol
		
		public bool NeedsPrep {
			get { return NeedsPreparation (type, parameter_kind); }
		}
		
		public CodeExpression PrepareCallback (CodeExpression arg)
		{
			return GetCallbackPrep (type, parameter_kind, arg);
		}
	
		public CodeExpression CleanupCallback (CodeExpression arg, CodeExpression orgArg)
		{
			return GetCallbackCleanup (type, arg, orgArg);
		}
	
		static bool NeedsPreparation (Type type, ExportParameterKind pkind)
		{
			switch (GetKind (type)) {
			case SymbolKind.Array:
			case SymbolKind.CharSequence:
			case SymbolKind.String:
				return true;
			}
			// other than the above, no complex marshal is required.
			return false;
		}
		
		public static CodeExpression GetCallbackPrep (Type type, ExportParameterKind pkind, CodeExpression arg)
		{
			switch (GetKind (type)) {
			// ArraySymbol:
			//	return new string[] { String.Format ("{0}[] {1} = ({0}[]) JNIEnv.GetArray ({2}, JniHandleOwnership.DoNotTransfer, typeof ({3}));", ElementType, var_name, SymbolTable.GetNativeName (var_name), sym.FullName) };
			case SymbolKind.Array:
				return new CodeMethodCall (jnienv_getarray, arg, do_not_transfer_literal, new CodeLiteral (type)).CastTo (type);

			// CharSequenceSymbol:
			//	return new string[] { String.Format ("Java.Lang.ICharSequence {0} = Java.Lang.Object.GetObject<Java.Lang.ICharSequence> ({1}, JniHandleOwnership.DoNotTransfer);", var_name, SymbolTable.GetNativeName (var_name)) };
			case SymbolKind.CharSequence:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (typeof (ICharSequence)), arg, do_not_transfer_literal);
			
			// FIXME: this is extraneous. Non-callback generator had better be re-examined.
			// StreamSymbol:
			//	return new string[] { String.Format ("System.IO.Stream {0} = global::Android.Runtime.{1}Invoker.FromJniHandle ({2}, JniHandleOwnership.DoNotTransfer);", var_name, base_name, SymbolTable.GetNativeName (var_name)) };
			case SymbolKind.Stream:
				/*
				switch (pkind) {
				case ExportParameterKind.InputStream:
					return new CodeMethodCall (inputstreaminvoker_fromjnihandle, arg, do_not_transfer_literal);
				case ExportParameterKind.OutputStream:
					return new CodeMethodCall (outputstreaminvoker_fromjnihandle, arg, do_not_transfer_literal);
				default:
					throw new NotSupportedException ("To use Stream type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
				*/
				return arg;
			// StringSymbol:
			//	return new string[] { String.Format ("string {0} = JNIEnv.GetString ({1}, JniHandleOwnership.DoNotTransfer);", var_name, SymbolTable.GetNativeName (var_name)) };
			case SymbolKind.String:
				return new CodeMethodCall (jnienv_getstring, arg, do_not_transfer_literal);
				
			// FIXME: this is extraneous. Non-callback generator had better be re-examined.
			// XmlPullParserSymbol:
			//	return new string[] { String.Format ("System.Xml.XmlReader {0} = global::Android.Runtime.XmlPullParserReader.FromJniHandle ({1}, JniHandleOwnership.DoNotTransfer);", var_name, SymbolTable.GetNativeName (var_name)) };
			// XmlResourceParserSymbol:
			//	return new string[] { String.Format ("System.Xml.XmlReader {0} = global::Android.Runtime.XmlResourceParserReader.FromJniHandle ({1}, JniHandleOwnership.DoNotTransfer);", var_name, SymbolTable.GetNativeName (var_name)) };
			case SymbolKind.XmlReader:
				/*
				switch (pkind) {
				case ExportParameterKind.XmlPullParser:
					return new CodeMethodCall (xmlpullparserreader_fromjnihandle, arg, do_not_transfer_literal);
				case ExportParameterKind.XmlResourceParser:
					return new CodeMethodCall (xmlresourceparserreader_fromjnihandle, arg, do_not_transfer_literal);
				default:
					throw new NotSupportedException ("To use XmlReader type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
				*/
				return arg;
			}
			// other than the above, no complex marshal is required.
			return arg;
		}
		
		public static CodeExpression GetCallbackCleanup (Type type, CodeExpression arg, CodeExpression orgArg)
		{
			switch (GetKind (type)) {
			// ArraySymbol:
			//	string[] result = new string [2];
			//	result [0] = String.Format ("if ({0} != null)", var_name);
			//	result [1] = String.Format ("\tJNIEnv.CopyArray ({0}, {1});", var_name, SymbolTable.GetNativeName (var_name));
			//	return result;
			case SymbolKind.Array:
				MethodInfo copyArrayMethod;
				switch (Type.GetTypeCode (type)) {
				case TypeCode.Empty:
				case TypeCode.DBNull:
					throw new NotSupportedException ("Only primitive types and IJavaObject is supported in array type in callback method parameter or return value");
				case TypeCode.Object:
					if (typeof (IJavaObject).IsAssignableFrom (type))
						copyArrayMethod = typeof (JNIEnv).GetMethod ("CopyArray", new Type [] { typeof (IJavaObject), typeof (IntPtr) });
					else
						goto case TypeCode.Empty;
					break;
				default:
					copyArrayMethod = typeof (JNIEnv).GetMethod ("CopyArray", new Type [] { type, typeof (IntPtr) });
					break;
				}
				return new CodeWhen (arg.IsNull, arg, new CodeMethodCall (copyArrayMethod, arg, orgArg));
			
			// CharSequenceSymbol:
			// 	return new string[] { String.Format ("Java.Lang.ICharSequence {0} = Java.Lang.Object.GetObject<Java.Lang.ICharSequence> ({1}, JniHandleOwnership.DoNotTransfer);", var_name, SymbolTable.GetNativeName (var_name)) };
			case SymbolKind.CharSequence:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (typeof (ICharSequence)), arg, do_not_transfer_literal);
			}
			
			// StreamSymbol:
			// StringSymbol:
			// XmlPullParserSymbol:
			// XmlResourceParserSymbol:
			//	no callback cleanup

			// other than the above, no complex marshal is required.
			return arg;
		}
		
		#endregion
		
		enum SymbolKind {
			Array,
			CharSequence,
			Class,
			Collection,
			Enum,
			SimpleFormat,
			// Generic, - this does not exist in dynamic invocation
			GenericTypeParameter,
			Interface,
			Stream,
			String,
			XmlReader,
		}
		
		static SymbolKind GetKind (Type type)
		{
			if (type.IsArray)
				return SymbolKind.Array;
			if (type.IsEnum)
				return SymbolKind.Enum;
			if (type == typeof (Java.Lang.ICharSequence))
				return SymbolKind.CharSequence;
			if (type == typeof (System.IO.Stream))
				return SymbolKind.Stream;
			if (type == typeof (System.Xml.XmlReader))
				return SymbolKind.XmlReader;
			if (type == typeof (string))
				return SymbolKind.String;
			if (Type.GetTypeCode (type) != TypeCode.Object)
				return SymbolKind.SimpleFormat;
			if (type == typeof (IList) ||
			    type == typeof (IDictionary) ||
			    type == typeof (ICollection))
				return SymbolKind.Collection;
			if (type.IsGenericParameter)
				return SymbolKind.GenericTypeParameter;
			if (type.IsGenericTypeDefinition)
				throw new NotSupportedException ("Dynamic method generation is not supported for generic type definition");
			if (type.IsInterface)
				return SymbolKind.Interface;
			else
				return SymbolKind.Class;
		}
		
		public CodeExpression FromNative (CodeExpression arg)
		{
			switch (GetKind (type)) {
			// ArraySymbol:
			//	return String.Format ("({0}[]) JNIEnv.GetArray ({1}, {2}, typeof ({3}))", ElementType, var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", opt.GetOutputName (sym.FullName));
			case SymbolKind.Array:
				return new CodeMethodCall (jnienv_getarray.MakeGenericMethod (type.GetElementType ()), arg, do_not_transfer_literal, new CodeLiteral (type)).CastTo (type.GetElementType ().MakeArrayType ());

			// CharSequence:
			//	return String.Format ("Java.Lang.Object.GetObject<Java.Lang.ICharSequence> ({0}, {1})", var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.CharSequence:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (type), arg, do_not_transfer_literal);

			// ClassGen:
			//	return String.Format ("Java.Lang.Object.GetObject<{0}> ({1}, {2})", opt.GetOutputName (FullName), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.Class:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (type), arg, do_not_transfer_literal);
			
			// EnumSymbol:
			//	return String.Format ("({0}) {1}", type, varname);
			case SymbolKind.Enum:
				return arg.CastTo (type);

			// SimpleSymbol/FormatSymbol:
			//	return as is
			case SymbolKind.SimpleFormat:
				return arg;

			/*
			// GenericSymbol:
			//	return gen.FromNative (opt, varname, owned);
			case SymbolKind.Generic:
				throw new NotImplementedException ();
			*/

			// GenericTypeParameter:
			//	return String.Format ("({0}) Java.Lang.Object.GetObject<{3}> ({1}, {2})", type, varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer", FullName);
			case SymbolKind.GenericTypeParameter:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (type), arg, do_not_transfer_literal).CastTo (type);

			// InterfaceGen:
			//	return String.Format ("Java.Lang.Object.GetObject<{0}> ({1}, {2})", opt.GetOutputName (FullName), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.Interface:
				return new CodeMethodCall (object_getobject.MakeGenericMethod (type), arg, do_not_transfer_literal);

			// StreamSymbol:
			//	return String.Format (opt.GetOutputName ("Android.Runtime.{0}Invoker") + ".FromJniHandle ({1}, {2})", base_name, var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.Stream:
				switch (parameter_kind) {
				case ExportParameterKind.InputStream:
					return new CodeMethodCall (inputstreaminvoker_fromjnihandle, arg, do_not_transfer_literal);
				case ExportParameterKind.OutputStream:
					return new CodeMethodCall (outputstreaminvoker_fromjnihandle, arg, do_not_transfer_literal);
				default:
					throw new NotSupportedException ("To use Stream type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
				
			// StringSymbol:
			//	return String.Format ("JNIEnv.GetString ({0}, {1})", var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.String:
				return new CodeMethodCall (jnienv_getstring, arg, do_not_transfer_literal);

			// XmlPullParserSymbol:
			//	return String.Format (opt.GetOutputName ("Android.Runtime.XmlPullParserReader") + ".FromJniHandle ({0}, {1})", var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			// XmlResourceParserSymbol:
			//	return String.Format (opt.GetOutputName ("Android.Runtime.XmlResourceParserReader") + ".FromJniHandle ({0}, {1})", var_name, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			case SymbolKind.XmlReader:
				switch (parameter_kind) {
				case ExportParameterKind.XmlPullParser:
					return new CodeMethodCall (xmlpullparserreader_fromjnihandle, arg, do_not_transfer_literal);
				case ExportParameterKind.XmlResourceParser:
					return new CodeMethodCall (xmlresourceparserreader_fromjnihandle, arg, do_not_transfer_literal);
				default:
					throw new NotSupportedException ("To use XmlReader type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
			}

			throw new InvalidOperationException ();
		}
	
		// Ignore ToNative() overload that takes generic instancing mapping. The reflected method should have nothing to do with it.
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaArray`1", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaCollection", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaCollection`1", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaDictionary", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaDictionary`2", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaList", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaList`1", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaSet", "Mono.Android")]
		[DynamicDependency ("ToLocalJniHandle", "Android.Runtime.JavaSet`1", "Mono.Android")]
		public CodeExpression ToNative (CodeExpression arg)
		{
			switch (GetKind (type)) {
			// ArraySymbol:
			//	return String.Format ("JNIEnv.NewArray ({0})", var_name);
			case SymbolKind.Array:
				return new CodeMethodCall (jnienv_newarray.MakeGenericMethod (type.GetElementType ()), arg);
			
			// CharSequenceSymbol:
			//	return String.Format ("CharSequence.ToLocalJniHandle ({0})", var_name);
			case SymbolKind.CharSequence:
				return new CodeMethodCall (charsequence_tojnihandle, arg);
			
			// ClassGen:
			//	return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
			case SymbolKind.Class:
				return new CodeMethodCall (jnienv_tojnihandle, arg);

			// CollectionSymbol:
			//	return String.Format ("{0}.ToLocalJniHandle ({1})", opt.GetOutputName (marshaler + (parms != null && parms.IsConcrete ? parms.ToString () : String.Empty)), varname);
			case SymbolKind.Collection:
				return new CodeMethodCall (type.GetMethod ("ToLocalJniHandle"), arg);

			// EnumSymbol:
			//	return "(int) " + varname;
			case SymbolKind.Enum:
				return arg.CastTo (typeof (int));

			// SimpleSymbol/FormatSymbol:
			//	return as is
			case SymbolKind.SimpleFormat:
				return arg;

			/*
			// GenericSymbol:
			case SymbolKind.Generic:
				throw new NotImplementedException ();
			*/

			// GenericTypeParameter:
			//	return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
			case SymbolKind.GenericTypeParameter:
				return new CodeMethodCall (jnienv_tojnihandle, arg);

			// InterfaceGen:
			//	return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
			case SymbolKind.Interface:
				return new CodeMethodCall (jnienv_tojnihandle, arg);

			// StreamSymbol:
			case SymbolKind.Stream:
			//	return String.Format (opt.GetOutputName ("Android.Runtime.{0}Adapter") + ".ToLocalJniHandle ({1})", base_name, var_name);
				switch (parameter_kind) {
				case ExportParameterKind.InputStream:
					return new CodeMethodCall (inputstreamadapter_tojnihandle, arg);
				case ExportParameterKind.OutputStream:
					return new CodeMethodCall (outputstreamadapter_tojnihandle, arg);
				default:
					throw new NotSupportedException ("To use Stream type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
				
			// StringSymbol:
			//	return String.Format ("JNIEnv.NewString ({0})", var_name);
			case SymbolKind.String:
				return new CodeMethodCall (jnienv_newstring, arg);

			// XmlPullParserSymbol:
			//	return String.Format ("global::Android.Runtime.XmlReaderPullParser.ToLocalJniHandle ({0})", var_name);
			// XmlResourceParserSymbol:
			//	return String.Format ("global::Android.Runtime.XmlReaderResourceParser.ToLocalJniHandle ({0})", var_name);
			case SymbolKind.XmlReader:
				switch (parameter_kind) {
				case ExportParameterKind.XmlPullParser:
					return new CodeMethodCall (xmlreaderpullparser_tojnihandle, arg);
				case ExportParameterKind.XmlResourceParser:
					return new CodeMethodCall (xmlreaderresourceparser_tojnihandle, arg);
				default:
					throw new NotSupportedException ("To use XmlReader type in callback, ExportParameterAttribute is required on the parameter to indicate its Java appropriate parameter kind");
				}
			}

			throw new InvalidOperationException ();
		}

		public static Type GetNativeType (Type type)
		{
			if (type == typeof (void))
				return typeof (void);
			if (type.IsEnum)
				return typeof (int);
			switch (Type.GetTypeCode (type)) {
			case TypeCode.DBNull:
			case TypeCode.Object:
			case TypeCode.String:
				return typeof (IntPtr);
			default:
				return type;
			}
		}
	}
	
	[RequiresDynamicCode (MonoAndroidExport.DynamicFeatures)]
	[RequiresUnreferencedCode (MonoAndroidExport.DynamicFeatures)]
	static class DynamicCallbackFactory
	{
		static DynamicCallbackFactory ()
		{
			var assembly = AssemblyBuilder.DefineDynamicAssembly (
				new AssemblyName ("__callback_factory__"), AssemblyBuilderAccess.Run);
			Module = assembly.DefineDynamicModule ("__callback_factory__");
			CodeClass = new CodeClass (Module, "__callback_factory__class__");
		}
		public static ModuleBuilder Module { get; private set; }
		public static CodeClass CodeClass { get; private set; }
	}
	
	[RequiresDynamicCode (MonoAndroidExport.DynamicFeatures)]
	[RequiresUnreferencedCode (MonoAndroidExport.DynamicFeatures)]
	class DynamicCallbackCodeGenerator : CallbackCodeGenerator<Type>
	{
		public static Delegate Create (MethodInfo method)
		{
			return new DynamicCallbackCodeGenerator (method).GetCallback ();
		}
		
		static ExportParameterKind GetExportKind (ICustomAttributeProvider m)
		{
			foreach (ExportParameterAttribute a in m.GetCustomAttributes (typeof (ExportParameterAttribute), false))
				return a.Kind;
			return ExportParameterKind.Unspecified;
		}
		
		MethodInfo method;
		public DynamicCallbackCodeGenerator (MethodInfo method)
		{
			this.method = method;
			return_type_info = DynamicInvokeTypeInfo.Get (method.ReturnType, GetExportKind (method.ReturnParameter));
			var lpgen = new List<DynamicInvokeTypeInfo> ();
			foreach (var p in method.GetParameters ())
				lpgen.Add (DynamicInvokeTypeInfo.Get (p.ParameterType, GetExportKind (p)));
			parameter_type_infos = lpgen;
		}
		
		static Type GetActionFuncType (int count, bool func)
		{
			if (func) {
				switch (count) {
				case 1: return typeof (Func<>);
				case 2: return typeof (Func<,>);
				case 3: return typeof (Func<,,>);
				case 4: return typeof (Func<,,,>);
				case 5: return typeof (Func<,,,,>);
				case 6: return typeof (Func<,,,,,>);
				case 7: return typeof (Func<,,,,,,>);
				case 8: return typeof (Func<,,,,,,,>);
				case 9: return typeof (Func<,,,,,,,,>);
				case 10: return typeof (Func<,,,,,,,,,>);
				case 11: return typeof (Func<,,,,,,,,,,>);
				case 12: return typeof (Func<,,,,,,,,,,,>);
				case 13: return typeof (Func<,,,,,,,,,,,,>);
				default: throw new NotSupportedException ();
				}
			} else {
				switch (count) {
				case 1: return typeof (Action<>);
				case 2: return typeof (Action<,>);
				case 3: return typeof (Action<,,>);
				case 4: return typeof (Action<,,,>);
				case 5: return typeof (Action<,,,,>);
				case 6: return typeof (Action<,,,,,>);
				case 7: return typeof (Action<,,,,,,>);
				case 8: return typeof (Action<,,,,,,,>);
				case 9: return typeof (Action<,,,,,,,,>);
				case 10: return typeof (Action<,,,,,,,,,>);
				case 11: return typeof (Action<,,,,,,,,,,>);
				case 12: return typeof (Action<,,,,,,,,,,,>);
				case 13: return typeof (Action<,,,,,,,,,,,,>);
				default: throw new NotSupportedException ();
				}
			}
		}

		Type delegate_type;
		public override Type GetDelegateType ()
		{
			if (delegate_type == null) {
				var parms = new List<Type> ();
				parms.Add (typeof (IntPtr));
				parms.Add (typeof (IntPtr));
				parms.AddRange (parameter_type_infos.ConvertAll<Type> (p => p.NativeType));
				if (method.ReturnType == typeof (void))
					delegate_type = parms.Count == 0 ? typeof (Action) : GetActionFuncType (parms.Count, false).MakeGenericType (parms.ToArray ());
				else {
					parms.Add (return_type_info.NativeType);
					delegate_type = GetActionFuncType (parms.Count, true).MakeGenericType (parms.ToArray ());
				}
			}
			return delegate_type;
		}
		
		static int gen_count;
		static readonly MethodInfo get_object_method = typeof (Java.Lang.Object).GetMethod ("GetObject", new[]{typeof (IntPtr), typeof (IntPtr), typeof (JniHandleOwnership)});
		static readonly CodeLiteral do_not_transfer_literal = new CodeLiteral (JniHandleOwnership.DoNotTransfer);
		
		Delegate result;
		
		public Delegate GetCallback ()
		{
			if (result == null)
				GenerateNativeCallbackDelegate ();
			return result;
		}
		
		public override void GenerateNativeCallbackDelegate ()
		{
			int num;
			lock (DynamicCallbackFactory.Module)
				num = gen_count++;
			var name = "dynamic_callback_" + num;
			var paramTypes = new List<Type> ();
			paramTypes.Add (typeof (IntPtr));
			paramTypes.Add (typeof (IntPtr));
			paramTypes.AddRange (parameter_type_infos.ConvertAll<Type> (p => p.NativeType).ToArray ());
			var m = GenerateNativeCallbackDelegate (name);
			//Console.WriteLine (m.PrintCode ());
			var dm = new DynamicMethod (name, System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.Public, CallingConventions.Standard,
				return_type_info.NativeType, paramTypes.ToArray (), DynamicCallbackFactory.Module, true);
			m.Generate (dm.GetILGenerator ());
			result = dm.CreateDelegate (GetDelegateType ());
		}
		
		DynamicInvokeTypeInfo return_type_info;
		List<DynamicInvokeTypeInfo> parameter_type_infos;
		
		// I gave up making it common to generator and SRE... generator cannot provide
		// System.Type that is required for DeclareVariable.
		CodeMethod GenerateNativeCallbackDelegate (string generatedMethodName)
		{
			// sw.WriteLine ("{0}static {1} n_{2} (IntPtr jnienv, IntPtr native__this {3})", indent, RetVal.NativeType, Name + IDSignature, Parameters.CallbackSignature);
			var args = new List<Type> ();
			args.Add (typeof (IntPtr));
			args.Add (typeof (IntPtr));
			args.AddRange (parameter_type_infos.ConvertAll<Type> (p => p.NativeType).ToArray ());
			var mgen = DynamicCallbackFactory.CodeClass.CreateMethod (generatedMethodName, MethodAttributes.Static, return_type_info.NativeType, args.ToArray ());

			//sw.WriteLine ("{0}{{", indent);
			var builder = mgen.CodeBuilder;

			// non-static only
			CodeVariableReference varThis = null;
			if (!method.IsStatic) {
				//sw.WriteLine ("{0}\t{1} __this = Java.Lang.Object.GetObject<{1}> (native__this, JniHandleOwnership.DoNotTransfer);", indent, type.Name);
				varThis = builder.DeclareVariable (method.DeclaringType, new CodeMethodCall (
					get_object_method.MakeGenericMethod (method.DeclaringType),
					mgen.GetArg (0),
					mgen.GetArg (1),
					do_not_transfer_literal));
			}
			
			//foreach (string s in Parameters.GetCallbackPrep (opt))
			//	sw.WriteLine ("{0}\t{1}", indent, s);
			// ... is handled as PrepareCallback (CodeExpression)

			// unlike generator, we don't check if it is property method, so ... (cont.)
			
			//if (String.IsNullOrEmpty (property_name)) {
			//	string call = "__this." + Name + (as_formatted ? "Formatted" : String.Empty) + " (" + Parameters.Call + ")";
			//	if (IsVoid)
			//		sw.WriteLine ("{0}\t{1};", indent, call);
			//	else
			//		sw.WriteLine ("{0}\t{1} {2};", indent, Parameters.HasCleanup ? RetVal.NativeType + " __ret =" : "return", RetVal.ToNative (opt, call));
			var callArgs = new List<CodeExpression> ();
			for (int i = 0; i < parameter_type_infos.Count; i++) {
				if (parameter_type_infos [i].NeedsPrep)
					callArgs.Add (parameter_type_infos [i].PrepareCallback (mgen.GetArg (i + 2)));
				else
					callArgs.Add (parameter_type_infos [i].FromNative (mgen.GetArg (i + 2)));
			}
			CodeMethodCall call;
			if (method.IsStatic)
				call = new CodeMethodCall (method, callArgs.ToArray ());
			else
				call = new CodeMethodCall (varThis, method, callArgs.ToArray ());
			CodeExpression ret = null;
			if (method.ReturnType == typeof (void))
				builder.CurrentBlock.Add (call);
			else
				ret = builder.DeclareVariable (return_type_info.NativeType, return_type_info.ToNative (call));
			
			// ... (contd.) ignore the following part ...
			//} else {
			//	if (IsVoid)
			//		sw.WriteLine ("{0}\t__this.{1} = {2};", indent, property_name, Parameters.Call);
			//	else
			//		sw.WriteLine ("{0}\t{1} {2};", indent, Parameters.HasCleanup ? RetVal.NativeType + " __ret =" : "return", RetVal.ToNative (opt, "__this." + property_name));
			//}
			// ... until here.

			//foreach (string cleanup in Parameters.CallbackCleanup)
			//	sw.WriteLine ("{0}\t{1}", indent, cleanup);
			var callbackCleanup = new List<CodeStatement> ();
			for (int i = 0; i < parameter_type_infos.Count; i++)
				builder.CurrentBlock.Add (parameter_type_infos [i].CleanupCallback (callArgs [i], mgen.GetArg (i)));

			//if (!IsVoid && Parameters.HasCleanup)
			//	sw.WriteLine ("{0}\treturn __ret;", indent);
			if (method.ReturnType != typeof (void))
				builder.Return (ret);
			//sw.WriteLine ("{0}}}", indent);
			//sw.WriteLine ();
			return mgen;
		}
	}
}
