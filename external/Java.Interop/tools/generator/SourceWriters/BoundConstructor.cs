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
	public class BoundConstructor : ConstructorWriter
	{
		protected Ctor constructor;
		protected CodeGenerationOptions opt;
		protected CodeGeneratorContext context;
		readonly string context_this;

		public BoundConstructor (ClassGen klass, Ctor constructor, bool useBase, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			this.constructor = constructor;
			this.opt = opt;
			this.context = context;

			Name = klass.Name;

			constructor.JavadocInfo?.AddJavadocs (Comments);
			Comments.Add (string.Format ("// Metadata.xml XPath constructor reference: path=\"{0}/constructor[@name='{1}'{2}]\"", klass.MetadataXPathReference, klass.JavaSimpleName, constructor.Parameters.GetMethodXPathPredicate ()));

			Attributes.Add (new RegisterAttr (".ctor", constructor.JniSignature, string.Empty, additionalProperties: constructor.AdditionalAttributeString ()));

			if (constructor.Deprecated != null)
				Attributes.Add (new ObsoleteAttr (constructor.Deprecated.Replace ("\"", "\"\"")));

			if (constructor.CustomAttributes != null)
				Attributes.Add (new CustomAttr (constructor.CustomAttributes));

			if (constructor.Annotation != null)
				Attributes.Add (new CustomAttr (constructor.Annotation));

			SetVisibility (constructor.Visibility);
			IsUnsafe = true;

			BaseCall = $"{(useBase ? "base" : "this")} (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)";
			context_this = context.ContextType.GetObjectHandleProperty ("this");

			this.AddMethodParameters (constructor.Parameters, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			writer.WriteLine ("{0}string __id = \"{1}\";",
						constructor.IsNonStaticNestedType ? "" : "const ",
						constructor.IsNonStaticNestedType
						? "(" + constructor.Parameters.GetJniNestedDerivedSignature (opt) + ")V"
						: constructor.JniSignature);
			writer.WriteLine ();
			writer.WriteLine ($"if ({context_this} != IntPtr.Zero)");
			writer.WriteLine ("\treturn;");
			writer.WriteLine ();

			foreach (var prep in constructor.Parameters.GetCallPrep (opt))
				writer.WriteLine (prep);

			writer.WriteLine ("try {");

			writer.Indent ();
			WriteParamterListCallArgs (writer, constructor.Parameters, false, opt);
			writer.WriteLine ("var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (){0});", constructor.Parameters.GetCallArgs (opt, invoker: false));
			writer.WriteLine ("SetHandle (__r.Handle, JniHandleOwnership.TransferLocalRef);");
			writer.WriteLine ("_members.InstanceMethods.FinishCreateInstance (__id, this{0});", constructor.Parameters.GetCallArgs (opt, invoker: false));
			writer.Unindent ();

			writer.WriteLine ("} finally {");

			writer.Indent ();
			var call_cleanup = constructor.Parameters.GetCallCleanup (opt);
			foreach (string cleanup in call_cleanup)
				writer.WriteLine (cleanup);

			foreach (var p in constructor.Parameters.Where (para => para.ShouldGenerateKeepAlive ()))
				writer.WriteLine ($"global::System.GC.KeepAlive ({opt.GetSafeIdentifier (p.Name)});");

			writer.Unindent ();

			writer.WriteLine ("}");
		}

		void WriteParamterListCallArgs (CodeWriter writer, ParameterList parameters, bool invoker, CodeGenerationOptions opt)
		{
			if (parameters.Count == 0)
				return;

			string JValue = "JValue";

			switch (opt.CodeGenerationTarget) {
				case CodeGenerationTarget.XAJavaInterop1:
				case CodeGenerationTarget.JavaInterop1:
					JValue = invoker ? JValue : "JniArgumentValue";
					break;
			}

			writer.WriteLine ("{0}* __args = stackalloc {0} [{1}];", JValue, parameters.Count);

			for (var i = 0; i < parameters.Count; ++i) {
				var p = parameters [i];
				writer.WriteLine ("__args [{0}] = new {1} ({2});", i, JValue, p.GetCall (opt));
			}
		}
	}

	public class StringOverloadConstructor : BoundConstructor
	{
		public StringOverloadConstructor (ClassGen klass, Ctor constructor, bool useBase, CodeGenerationOptions opt, CodeGeneratorContext context) :
			base (klass, constructor, useBase, opt, context)
		{
			Comments.Clear ();
			Parameters.Clear ();

			// TODO: This is likely incorrect but done for compat with old generator.
			// A string overload for an obsolete ctor will not be marked obsolete.
			var obsolete_attr = Attributes.OfType<ObsoleteAttr> ().FirstOrDefault ();

			if (obsolete_attr != null)
				Attributes.Remove (obsolete_attr);

			this.AddMethodParametersStringOverloads (constructor.Parameters, opt);
		}
	}
}
