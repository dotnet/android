using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;


namespace MonoDroid.Generation {

	public class Property {

		string name;

		public Property (string name)
		{
			this.name = name;
		}

		public Method Getter {get; set;}
		public Method Setter {get; set;}

		public bool IsGeneric {
			get { return Getter.IsGeneric; }
		}

		// This is a workaround for generaing compatibility for Android.Graphics.Drawables.ColorDrawable.SetColor (wrt bug #4288).
		public bool GenerateDispatchingSetter { get; set; }

		string AdjustedName {
			get { return Getter.ReturnType.StartsWith ("Java.Lang.ICharSequence") ? name + "Formatted" : name; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public string Type {
			get { return Setter != null ? Setter.Parameters [0].Type : Getter.ReturnType; }
		}

		public void GenerateCallbacks (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase gen)
		{
			GenerateCallbacks (sw, indent, opt, gen, AdjustedName);
		}

		public void GenerateCallbacks (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase gen, string name)
		{
			opt.CodeGenerator.WriteMethodCallback (Getter, sw, indent, opt, gen, name);
			if (Setter != null)
				opt.CodeGenerator.WriteMethodCallback (Setter, sw, indent, opt, gen, name);
		}

		public void GenerateAbstractDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase gen)
		{
			bool overrides = false;
			var baseProp = gen.BaseSymbol != null ? gen.BaseSymbol.GetPropertyByName (name, true) : null;
			if (baseProp != null) {
				if (baseProp.Type != Getter.Return) {
					// This may not be required if we can change generic parameter support to return constrained type (not just J.L.Object).
					sw.WriteLine ("{0}// skipped generating property {1} because its Java method declaration is variant that we cannot represent in C#", indent, name);
					return;
				}
				overrides = true;
			}

			bool requiresNew     = false;
			string abstract_name = AdjustedName;
			string visibility = Getter.RetVal.IsGeneric ? "protected" : Getter.Visibility;
			if (!overrides) {
				requiresNew      = gen.RequiresNew (abstract_name);
				GenerateCallbacks (sw, indent, opt, gen, abstract_name);
			}
			sw.WriteLine ("{0}{1}{2} abstract{3} {4} {5} {{",
					indent,
					visibility,
					requiresNew ? " new" : "",
					overrides ? " override" : "",
					opt.GetOutputName (Getter.ReturnType),
					abstract_name);
			if (gen.IsGeneratable)
				sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Getter.JavaName, Getter.Parameters.GetMethodXPathPredicate ());
			if (Getter.IsReturnEnumified)
				sw.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			opt.CodeGenerator.WriteMethodCustomAttributes (Getter, sw, indent);
			sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})] get;", indent, Getter.JavaName, Getter.JniSignature, Getter.ConnectorName, Getter.AdditionalAttributeString ());
			if (Setter != null) {
				if (gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Setter.JavaName, Setter.Parameters.GetMethodXPathPredicate ());
				opt.CodeGenerator.WriteMethodCustomAttributes (Setter, sw, indent);
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})] set;", indent, Setter.JavaName, Setter.JniSignature, Setter.ConnectorName, Setter.AdditionalAttributeString ());
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			if (Type.StartsWith ("Java.Lang.ICharSequence"))
				GenerateStringVariant (sw, indent);
		}

		public void GenerateDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase gen, string adapter)
		{
			sw.WriteLine ("{0}{1} {2} {{", indent, opt.GetOutputName (Type), AdjustedName);
			if (Getter != null) {
				if (gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Getter.JavaName, Getter.Parameters.GetMethodXPathPredicate ());
				if (Getter.GenericArguments != null && Getter.GenericArguments.Any ())
					sw.WriteLine ("{0}{1}", indent, Getter.GenericArguments.ToGeneratedAttributeString ());
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] get;", indent, Getter.JavaName, Getter.JniSignature, Getter.ConnectorName, Getter.GetAdapterName (opt, adapter), Getter.AdditionalAttributeString ());
			}
			if (Setter != null) {
				if (gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Setter.JavaName, Setter.Parameters.GetMethodXPathPredicate ());
				if (Setter.GenericArguments != null && Setter.GenericArguments.Any ())
					sw.WriteLine ("{0}{1}", indent, Setter.GenericArguments.ToGeneratedAttributeString ());
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] set;", indent, Setter.JavaName, Setter.JniSignature, Setter.ConnectorName, Setter.GetAdapterName (opt, adapter), Setter.AdditionalAttributeString ());
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateInvoker (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase container)
		{
			GenerateCallbacks (sw, indent, opt, container);
			opt.CodeGenerator.WriteMethodIdField (Getter, sw, indent, opt, invoker: true);
			if (Setter != null)
				opt.CodeGenerator.WriteMethodIdField (Setter, sw, indent, opt, invoker: true);
			sw.WriteLine ("{0}public unsafe {1} {2} {{", indent, opt.GetOutputName (Getter.ReturnType), AdjustedName);
			sw.WriteLine ("{0}\tget {{", indent);
			opt.CodeGenerator.WriteMethodInvokerBody (Getter, sw, indent + "\t\t", opt);
			sw.WriteLine ("{0}\t}}", indent);
			if (Setter != null) {
				string pname = Setter.Parameters [0].Name;
				Setter.Parameters [0].Name = "value";
				sw.WriteLine ("{0}\tset {{", indent);
				opt.CodeGenerator.WriteMethodInvokerBody (Setter, sw, indent + "\t\t", opt);
				sw.WriteLine ("{0}\t}}", indent);
				Setter.Parameters [0].Name = pname;
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateExplicitIface (StreamWriter sw, string indent, CodeGenerationOptions opt, GenericSymbol gen, string adapter)
		{
			Dictionary<string, string> mappings = new Dictionary<string, string> ();
			for (int i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters [i].Name] = gen.TypeParams [i].FullName;

			//If the property type is Java.Lang.Object, we don't need to generate an explicit implementation
			if (Getter?.RetVal.GetGenericType (mappings) == "Java.Lang.Object")
				return;
			if (Setter?.Parameters[0].GetGenericType (mappings) == "Java.Lang.Object")
				return;

			sw.WriteLine ("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
			sw.WriteLine ("{0}{1} {2}.{3} {{", indent, opt.GetOutputName (Type), opt.GetOutputName (gen.Gen.FullName), AdjustedName);
			if (Getter != null) {
				if (gen.Gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.Gen.MetadataXPathReference, Getter.JavaName, Getter.Parameters.GetMethodXPathPredicate ());
				if (Getter.GenericArguments != null && Getter.GenericArguments.Any ())
					sw.WriteLine ("{0}{1}", indent, Getter.GenericArguments.ToGeneratedAttributeString ());
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] get {{", indent, Getter.JavaName, Getter.JniSignature, Getter.ConnectorName, Getter.GetAdapterName (opt, adapter), Getter.AdditionalAttributeString ());
				sw.WriteLine ("{0}\t\treturn {1};", indent, Name);
				sw.WriteLine ("{0}\t}}", indent);
			}
			if (Setter != null) {
				if (gen.Gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.Gen.MetadataXPathReference, Setter.JavaName, Setter.Parameters.GetMethodXPathPredicate ());
				if (Setter.GenericArguments != null && Setter.GenericArguments.Any ())
					sw.WriteLine ("{0}{1}", indent, Setter.GenericArguments.ToGeneratedAttributeString ());
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})] set {{", indent, Setter.JavaName, Setter.JniSignature, Setter.ConnectorName, Setter.GetAdapterName (opt, adapter), Setter.AdditionalAttributeString ());
				sw.WriteLine ("{0}\t\t{1} = {2};", indent, Name, Setter.Parameters.GetGenericCall (opt, mappings));
				sw.WriteLine ("{0}\t}}", indent);
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		void GenerateStringVariant (StreamWriter sw, string indent)
		{
			bool is_array = Getter.RetVal.IsArray;
			sw.WriteLine ("{0}{1} string{2} {3} {{", indent, (Setter ?? Getter).Visibility, is_array ? "[]" : String.Empty, Name);
			if (is_array)
				sw.WriteLine ("{0}\tget {{ return CharSequence.ArrayToStringArray ({1}); }}", indent, AdjustedName);
			else
				sw.WriteLine ("{0}\tget {{ return {1} == null ? null : {1}.ToString (); }}", indent, AdjustedName);
			if (Setter != null) {
				if (is_array) {
					sw.WriteLine ("{0}\tset {{", indent);
					sw.WriteLine ("{0}\t\tglobal::Java.Lang.ICharSequence[] jlsa = CharSequence.ArrayFromStringArray (value);", indent);
					sw.WriteLine ("{0}\t\t{1} = jlsa;", indent, AdjustedName);
					sw.WriteLine ("{0}\t\tforeach (global::Java.Lang.String jls in jlsa) if (jls != null) jls.Dispose ();", indent);
					sw.WriteLine ("{0}\t}}", indent);
				} else {
					sw.WriteLine ("{0}\tset {{", indent);
					sw.WriteLine ("{0}\t\tglobal::Java.Lang.String jls = value == null ? null : new global::Java.Lang.String (value);", indent);
					sw.WriteLine ("{0}\t\t{1} = jls;", indent, AdjustedName);
					sw.WriteLine ("{0}\t\tif (jls != null) jls.Dispose ();", indent);
					sw.WriteLine ("{0}\t}}", indent);
				}
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void Generate (GenBase gen, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			Generate (gen, sw, indent, opt, true, false);
		}

		public void Generate (GenBase gen, StreamWriter sw, string indent, CodeGenerationOptions opt, bool with_callbacks, bool force_override)
		{
			// <TechnicalDebt>
			// This is a special workaround for AdapterView inheritance.
			// (How it is special? They have hand-written bindings AND brings generic
			// version of AdapterView<T> in the inheritance, also added by metadata!)
			//
			// They are on top of fragile hand-bound code, and when we are making changes
			// in generator, they bite. Since we are not going to bring API breakage
			// right now, we need special workarounds to get things working.
			//
			// So far, what we need here is to have AbsSpinner.Adapter compile.
			//
			// > platforms/*/src/generated/Android.Widget.AbsSpinner.cs(156,56): error CS0533:
			// > `Android.Widget.AbsSpinner.Adapter' hides inherited abstract member
			// > `Android.Widget.AdapterView<Android.Widget.ISpinnerAdapter>.Adapter
			//
			// It is because the AdapterView<T>.Adapter is hand-bound and cannot be
			// detected by generator!
			//
			// So, we explicitly treat it as a special-case.
			//
			// Then, Spinner, ListView and GridView instantiate them, so they are also special cases.
			// </TechnicalDebt>
			if (Name == "Adapter" &&
			    (Getter.DeclaringType.BaseGen.FullName == "Android.Widget.AdapterView" ||
			     Getter.DeclaringType.BaseGen.BaseGen != null && Getter.DeclaringType.BaseGen.BaseGen.FullName == "Android.Widget.AdapterView"))
				force_override = true;
			// ... and the above breaks generator tests...
			if (Name == "Adapter" &&
			    (Getter.DeclaringType.BaseGen.FullName == "Xamarin.Test.AdapterView" ||
			     Getter.DeclaringType.BaseGen.BaseGen != null && Getter.DeclaringType.BaseGen.BaseGen.FullName == "Xamarin.Test.AdapterView"))
				force_override = true;
		
			string decl_name = AdjustedName;
			string needNew          = gen.RequiresNew (decl_name) ? " new" : "";
			string virtual_override = String.Empty;
			bool is_virtual = Getter.IsVirtual && (Setter == null || Setter.IsVirtual);
			if (with_callbacks && is_virtual) {
				virtual_override = needNew + " virtual";
				opt.CodeGenerator.WriteMethodCallback (Getter, sw, indent, opt, gen, AdjustedName);
			}
			if (with_callbacks && is_virtual && Setter != null) {
				virtual_override = needNew + " virtual";
				opt.CodeGenerator.WriteMethodCallback (Setter, sw, indent, opt, gen, AdjustedName);
			}
			virtual_override = force_override ? " override" : virtual_override;
			if ((Getter ?? Setter).IsStatic)
				virtual_override = " static";
			// It should be using AdjustedName instead of Name, but ICharSequence ("Formatted") properties are not caught by this...
			else if (gen.BaseSymbol != null && gen.BaseSymbol.GetPropertyByName (Name, true) != null)
				virtual_override = " override";

			opt.CodeGenerator.WriteMethodIdField (Getter, sw, indent, opt);
			if (Setter != null)
				opt.CodeGenerator.WriteMethodIdField (Setter, sw, indent, opt);
			string visibility = Getter.IsAbstract && Getter.RetVal.IsGeneric ? "protected" : (Setter ?? Getter).Visibility;
			// Unlike [Register], mcs does not allow applying [Obsolete] on property accessors, so we can apply them only under limited condition...
			if (Getter.Deprecated != null && (Setter == null || Setter.Deprecated != null))
				sw.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, Getter.Deprecated.Replace ("\"", "\"\"").Trim () + (Setter != null && Setter.Deprecated != Getter.Deprecated ? " " + Setter.Deprecated.Replace ("\"", "\"\"").Trim () : null));
			opt.CodeGenerator.WriteMethodCustomAttributes (Getter, sw, indent);
			sw.WriteLine ("{0}{1}{2} unsafe {3} {4} {{", indent, visibility, virtual_override, opt.GetOutputName (Getter.ReturnType), decl_name);
			if (gen.IsGeneratable)
				sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Getter.JavaName, Getter.Parameters.GetMethodXPathPredicate ());
			sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, Getter.JavaName, Getter.JniSignature, Getter.ConnectorName, Getter.AdditionalAttributeString ());
			sw.WriteLine ("{0}\tget {{", indent);
			opt.CodeGenerator.WriteMethodBody (Getter, sw, indent + "\t\t", opt);
			sw.WriteLine ("{0}\t}}", indent);
			if (Setter != null) {
				if (gen.IsGeneratable)
					sw.WriteLine ("{0}\t// Metadata.xml XPath method reference: path=\"{1}/method[@name='{2}'{3}]\"", indent, gen.MetadataXPathReference, Setter.JavaName, Setter.Parameters.GetMethodXPathPredicate ());
				opt.CodeGenerator.WriteMethodCustomAttributes (Setter, sw, indent);
				sw.WriteLine ("{0}\t[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, Setter.JavaName, Setter.JniSignature, Setter.ConnectorName, Setter.AdditionalAttributeString ());
				sw.WriteLine ("{0}\tset {{", indent);
				string pname = Setter.Parameters [0].Name;
				Setter.Parameters [0].Name = "value";
				opt.CodeGenerator.WriteMethodBody (Setter, sw, indent + "\t\t", opt);
				Setter.Parameters [0].Name = pname;
				sw.WriteLine ("{0}\t}}", indent);
			} else if (GenerateDispatchingSetter) {
				sw.WriteLine ("{0}// This is a dispatching setter", indent + "\t");
				sw.WriteLine ("{0}set {{ Set{1} (value); }}", indent + "\t", Name);
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();

			if (Type.StartsWith ("Java.Lang.ICharSequence") && virtual_override != " override")
				GenerateStringVariant (sw, indent);
		}
	}
}

