using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.Android.Binder;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class MethodCallback : MethodWriter
	{
		readonly GenBase type;
		readonly Method method;
		readonly string property_name;
		readonly bool is_formatted;
		readonly CodeGenerationOptions opt;

		readonly FieldWriter delegate_field;
		readonly MethodWriter delegate_getter;

		// static sbyte n_ByteValueExact (IntPtr jnienv, IntPtr native__this)
		// {
		// 	var __this = global::Java.Lang.Object.GetObject<Android.Icu.Math.BigDecimal> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);
		// 	return __this.ByteValueExact ();
		// }
		public MethodCallback (GenBase type, Method method, CodeGenerationOptions options, string propertyName, bool isFormatted)
		{
			this.type = type;
			this.method = method;

			property_name = propertyName;
			is_formatted = isFormatted;
			opt = options;

			delegate_field = new MethodCallbackDelegateField (method, options);
			delegate_getter = new GetDelegateHandlerMethod (method, options);

			Name = "n_" + method.Name + method.IDSignature;
			ReturnType = new TypeReferenceWriter (method.RetVal.NativeType);

			IsStatic = true;
			IsPrivate = method.IsInterfaceDefaultMethod;

			SourceWriterExtensions.AddObsolete (Attributes, null, opt, forceDeprecate: !string.IsNullOrWhiteSpace (method.Deprecated), deprecatedSince: method.DeprecatedSince);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Attributes.Add (new DebuggerDisableUserUnhandledExceptionsAttributeAttr ());

			Parameters.Add (new MethodParameterWriter ("jnienv", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("native__this", TypeReferenceWriter.IntPtr));

			foreach (var p in method.Parameters)
				Parameters.Add (new MethodParameterWriter (options.GetSafeIdentifier (p.UnsafeNativeName), new TypeReferenceWriter (p.NativeType)));
		}

		protected override void WriteBody (CodeWriter writer)
		{
			writer.WriteLine ("if (!global::Java.Interop.JniEnvironment.BeginMarshalMethod (jnienv, out var __envp, out var __r))");
			writer.Indent ();
			writer.WriteLine (method.IsVoid ? "return;" : "return default;");
			writer.Unindent ();

			writer.WriteLine ();
			writer.WriteLine ("try {");

			writer.Indent ();
			writer.WriteLine ($"var __this = global::Java.Lang.Object.GetObject<{opt.GetOutputName (type.FullName)}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer){opt.NullForgivingOperator};");

			foreach (var s in method.Parameters.GetCallbackPrep (opt))
				writer.WriteLine (s);

			if (string.IsNullOrEmpty (property_name)) {
				var call = "__this." + method.Name + (is_formatted ? "Formatted" : string.Empty) + " (" + method.Parameters.GetCall (opt) + ")";
				if (method.IsVoid)
					writer.WriteLine (call + ";");
				else
					writer.WriteLine ("{0} {1};", method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, call));
			} else {
				if (method.IsVoid)
					writer.WriteLine ("__this.{0} = {1};", property_name, method.Parameters.GetCall (opt));
				else
					writer.WriteLine ("{0} {1};", method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, "__this." + property_name));
			}

			foreach (var cleanup in method.Parameters.GetCallbackCleanup (opt))
				writer.WriteLine (cleanup);

			if (!method.IsVoid && method.Parameters.HasCleanup)
				writer.WriteLine ("return __ret;");

			writer.Unindent ();

			writer.WriteLine ("} catch (global::System.Exception __e) {");
			writer.Indent ();
			writer.WriteLine ("__r.OnUserUnhandledException (ref __envp, __e);");

			if (!method.IsVoid)
				writer.WriteLine ("return default;");

			writer.Unindent ();
			writer.WriteLine ("} finally {");
			writer.Indent ();
			writer.WriteLine ("global::Java.Interop.JniEnvironment.EndMarshalMethod (ref __envp);");
			writer.Unindent ();
			writer.WriteLine ("}");


		}

		public override void Write (CodeWriter writer)
		{
			delegate_field.Write (writer);

			writer.WriteLineNoIndent ("#pragma warning disable 0169");

			delegate_getter.Write (writer);
			writer.WriteLine ();

			base.Write (writer);

			writer.WriteLineNoIndent ("#pragma warning restore 0169");
			writer.WriteLine ();
		}
	}

	public class MethodCallbackDelegateField : FieldWriter
	{
		// static Delegate cb_byteValueExact;
		public MethodCallbackDelegateField (Method method, CodeGenerationOptions options)
		{
			Name = method.EscapedCallbackName;
			Type = TypeReferenceWriter.Delegate;

			IsStatic = true;
			IsPrivate = method.IsInterfaceDefaultMethod;

			if (!string.IsNullOrEmpty (options.NullableOperator))
				Type.Nullable = true;
		}
	}

	public class GetDelegateHandlerMethod : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;

		// static Delegate GetByteValueExactHandler ()
		// {
		// 	if (cb_byteValueExact == null)
		// 		cb_byteValueExact = JNINativeWrapper.CreateDelegate ((_JniMarshal_PP_B) n_ByteValueExact);
		// 	return cb_byteValueExact;
		// }
		public GetDelegateHandlerMethod (Method method, CodeGenerationOptions opt)
		{
			this.method = method;
			this.opt = opt;

			Name = method.ConnectorName;
			ReturnType = TypeReferenceWriter.Delegate;

			IsStatic = true;
			IsPrivate = method.IsInterfaceDefaultMethod;

			SourceWriterExtensions.AddObsolete (Attributes, null, opt, forceDeprecate: !string.IsNullOrWhiteSpace (method.Deprecated), deprecatedSince: method.DeprecatedSince);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			var callback_name = method.EscapedCallbackName;
			writer.WriteLine ($"return {callback_name} ??= new {method.GetDelegateType (opt)} (n_{method.Name + method.IDSignature});");
		}
	}
}
