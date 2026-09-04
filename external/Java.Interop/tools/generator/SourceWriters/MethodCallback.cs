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
		readonly string target_name;

		readonly FieldWriter delegate_field;
		readonly MethodWriter delegate_getter;
		readonly UnmanagedCallbackKind kind;
		readonly TypedCallbackShape typed_shape;

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

			kind = UnmanagedCallbackSupport.GetCallbackKind (type, method, options, propertyName, isFormatted);
			if (kind == UnmanagedCallbackKind.UnmanagedTyped)
				UnmanagedCallbackSupport.TryGetTypedShape (type, method, options, propertyName, isFormatted, out typed_shape);

			if (kind == UnmanagedCallbackKind.Legacy) {
				delegate_field = new MethodCallbackDelegateField (method, options);
				delegate_getter = new GetDelegateHandlerMethod (method, options);
			} else {
				// The cb_* field and Get*Handler () connector are gone, but the _JniMarshal_*
				// delegate *type* is still registered. Mono.Android's hand-written legacy
				// JNINativeWrapper wraps a fixed set of these signatures, and dropping the type
				// declarations breaks its compilation. Removing them is a further, separate
				// saving that requires retiring the reflection-based registration path.
				method.GetDelegateType (options);
			}

			Name = UnmanagedCallbackSupport.GetCallbackName (type, method, options);
			target_name = UnmanagedCallbackSupport.GetCallbackTargetName (type, method, options);
			ReturnType = new TypeReferenceWriter (method.RetVal.NativeType);

			IsStatic = true;
			IsPrivate = method.IsInterfaceDefaultMethod;

			if (kind != UnmanagedCallbackKind.Legacy)
				Attributes.Add (new CustomAttr ("[global::System.Runtime.InteropServices.UnmanagedCallersOnly]"));

			SourceWriterExtensions.AddObsolete (Attributes, null, opt, forceDeprecate: !string.IsNullOrWhiteSpace (method.Deprecated), deprecatedSince: method.DeprecatedSince);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Parameters.Add (new MethodParameterWriter ("jnienv", TypeReferenceWriter.IntPtr));
			Parameters.Add (new MethodParameterWriter ("native__this", TypeReferenceWriter.IntPtr));

			foreach (var p in method.Parameters)
				Parameters.Add (new MethodParameterWriter (options.GetSafeIdentifier (p.UnsafeNativeName), new TypeReferenceWriter (p.NativeType)));
		}

		protected override void WriteBody (CodeWriter writer)
		{
			string call;
			if (typed_shape != null) {
				call = typed_shape.GetHelperInvocation (opt.GetOutputName (type.FullName), target_name, opt.NullableOperator);
			} else {
				var paramArgs = string.Join ("", method.Parameters.Select (p => $", {opt.GetSafeIdentifier (p.UnsafeNativeName)}"));
				call = $"global::Java.Interop.JniMarshal.{(method.IsVoid ? "SafeInvokeAction" : "SafeInvokeFunc")} (jnienv, native__this{paramArgs}, &{target_name})";
			}

			writer.WriteLine ("unsafe {");
			writer.Indent ();
			writer.WriteLine (method.IsVoid ? call + ";" : "return " + call + ";");
			writer.Unindent ();
			writer.WriteLine ("}");
		}

		void WriteMarshalBody (CodeWriter writer)
		{
			var attributes = new List<AttributeWriter> ();
			SourceWriterExtensions.AddObsolete (attributes, null, opt, forceDeprecate: !string.IsNullOrWhiteSpace (method.Deprecated), deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddSupportedOSPlatform (attributes, method, opt);
			foreach (var attribute in attributes)
				attribute.WriteAttribute (writer);

			if (typed_shape != null)
				writer.WriteLine (typed_shape.GetTargetSignature (opt.GetOutputName (type.FullName), target_name, opt.NullableOperator));
			else
				writer.WriteLine ($"private static {method.RetVal.NativeType} {target_name} (IntPtr jnienv, IntPtr native__this{method.Parameters.GetCallbackSignature (opt)})");
			writer.WriteLine ("{");

			writer.Indent ();
			if (typed_shape == null)
				writer.WriteLine ($"var __this = global::Java.Lang.Object.GetObject<{opt.GetOutputName (type.FullName)}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer){opt.NullForgivingOperator};");

			foreach (var s in typed_shape != null ? GetTypedCallbackPrep () : method.Parameters.GetCallbackPrep (opt).Cast<string> ())
				writer.WriteLine (s);

			// The typed helper owns the JNI conversion of a peer return value, and no typed shape
			// has a parameter requiring post-call cleanup, so the `__ret` dance is unnecessary.
			var useRetTemporary = typed_shape == null && method.Parameters.HasCleanup;
			string ToReturn (string call) => typed_shape != null && typed_shape.ReturnsPeer ? call : method.RetVal.ToNative (opt, call);

			if (string.IsNullOrEmpty (property_name)) {
				var call = "__this." + method.Name + (is_formatted ? "Formatted" : string.Empty) + " (" + method.Parameters.GetCall (opt) + ")";
				if (method.IsVoid)
					writer.WriteLine (call + ";");
				else
					writer.WriteLine ("{0} {1};", useRetTemporary ? method.RetVal.NativeType + " __ret =" : "return", ToReturn (call));
			} else {
				if (method.IsVoid)
					writer.WriteLine ("__this.{0} = {1};", property_name, method.Parameters.GetCall (opt));
				else
					writer.WriteLine ("{0} {1};", useRetTemporary ? method.RetVal.NativeType + " __ret =" : "return", ToReturn ("__this." + property_name));
			}

			if (typed_shape == null) {
				foreach (var cleanup in method.Parameters.GetCallbackCleanup (opt))
					writer.WriteLine (cleanup);
			}

			if (!method.IsVoid && useRetTemporary)
				writer.WriteLine ("return __ret;");

			writer.Unindent ();
			writer.WriteLine ("}");
		}

		// Object parameters are marshaled by the shared typed helper and arrive already converted;
		// only the pure scalar projections (bool, char, enums) remain for the target method.
		IEnumerable<string> GetTypedCallbackPrep ()
		{
			foreach (var p in method.Parameters) {
				if (p.NeedsPrep)
					continue;
				foreach (var line in p.GetPreCallback (opt))
					yield return line;
			}
		}

		public override void Write (CodeWriter writer)
		{
			delegate_field?.Write (writer);

			writer.WriteLineNoIndent ("#pragma warning disable 0169");

			if (delegate_getter != null) {
				delegate_getter.Write (writer);
				writer.WriteLine ();
			}

			base.Write (writer);
			WriteMarshalBody (writer);

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
