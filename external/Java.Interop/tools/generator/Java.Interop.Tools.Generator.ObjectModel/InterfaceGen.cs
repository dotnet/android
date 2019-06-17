using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using Xamarin.Android.Binder;

namespace MonoDroid.Generation
{

	public abstract class InterfaceGen : GenBase, IRequireGenericMarshal {

		protected bool hasManagedName;

		protected InterfaceGen (GenBaseSupport support)
			: base (support)
		{
		}
		
		public abstract string ArgsType { get; }

		public override string DefaultValue {
			get { return "IntPtr.Zero"; }
		}

		public bool HasManagedName => hasManagedName;

		public bool IsConstSugar {
			get { 
				if (Methods.Count > 0 || Properties.Count > 0)
					return false;

				foreach (InterfaceGen impl in GetAllDerivedInterfaces ())
					if (!impl.IsConstSugar)
						return false;

				// Need to keep Java.IO.ISerializable as a "marker interface"; want to
				// hide android.provider.ContactsContract.DataColumnsWithJoins
				if (Fields.Count == 0 && Interfaces.Count == 0)
					return false;

				return true;
			}
		}

		public bool IsListener {
			// If there is a property it cannot generate valid implementor, so reject this at least so far.
			get { return Name.EndsWith ("Listener") && Properties.Count == 0 && Interfaces.Count == 0; }
		}

		public virtual bool MayHaveManagedGenericArguments {
			get { return false; }
		}

		public override string NativeType {
			get { return "IntPtr"; }
		}

		internal bool NeedsSender {
			get {
				return Methods.Any (m => (m.RetVal.IsVoid && !m.Parameters.HasSender) ||
						(m.IsEventHandlerWithHandledProperty && !m.Parameters.HasSender));
			}
		}

		public override string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null) 
		{
			return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
			/*
			if (String.IsNullOrEmpty (Marshaler))
				return String.Format ("JNIEnv.ToLocalJniHandle ({0})", varname);
			else
				return GetObjectHandleProperty (varname);
			*/
		}

		public override string FromNative (CodeGenerationOptions opt, string varname, bool owned) 
		{
			return String.Format ("global::Java.Lang.Object.GetObject<{0}> ({1}, {2})", opt.GetOutputName (FullName), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			/*
			if (String.IsNullOrEmpty (Marshaler))
				return String.Format ("global::Java.Lang.Object.GetObject<{0}> ({1}, {2})", opt.GetOutputName (FullName), varname, owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer");
			else
				return String.Format ("new {1} ({0})", varname, Marshaler);
			*/
		}

		public override void AddNestedType (GenBase gen)
		{
			base.AddNestedType (gen);
			string nest_name = gen.JavaName.Substring (JavaName.Length + 1);
			if (nest_name.IndexOf (".") < 0) {
				if (gen is InterfaceGen) {
					gen.FullName = FullName + gen.Name.Substring (1);
					gen.Name = Name + gen.Name.Substring (1);
				} else {
					gen.FullName = FullName.Substring (0, FullName.Length - Name.Length) + Name.Substring (1) + gen.Name;
					gen.Name = Name.Substring (1) + gen.Name;
				}
			}
		}
		
		public override void ResetValidation ()
		{
			validated = false;
			base.ResetValidation ();
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			if (validated)
				return is_valid;

			validated = true;
			
			// Due to demand to validate in prior to validate ClassGen's BaseType, it is *not* done at
			// GenBase.
			if (TypeParameters != null && !TypeParameters.Validate (opt, type_params))
				return false;

			if (!base.OnValidate (opt, type_params) || iface_validation_failed || MethodValidationFailed) {
				if (iface_validation_failed)
					Report.Warning (0, Report.WarningInterfaceGen + 2, "Invalidating {0} and all nested types because some of its interfaces were invalid.", FullName);
				else if (MethodValidationFailed)
					Report.Warning (0, Report.WarningInterfaceGen + 3, "Invalidating {0} and all nested types because some of its methods were invalid.", FullName);
				foreach (GenBase nest in NestedTypes)
					nest.Invalidate ();
				is_valid = false;
				return false;
			}

			return true;
		}

		internal string GetEventDelegateName (Method m)
		{
			int start = Name.StartsWith ("IOn") ? 3 : 1;
			if (m.RetVal.IsVoid) {
				if (m.IsSimpleEventHandler)
					return "EventHandler";
				else {
					return "EventHandler<" + GetArgsName (m) + ">";
				}
			} else if (m.IsEventHandlerWithHandledProperty) {
				return "EventHandler<" + GetArgsName (m) + ">";
			} else {
				string methodSpec = Methods.Count > 1 ? m.AdjustedName : String.Empty;
				return Name.Substring (start, Name.Length - start - 8) + methodSpec + "Handler";
			}
		}

		internal string GetArgsName (Method m)
		{

			string nameBase;
			int start;
			int trim = 0;
			if (Methods.Count > 1) {
				if (!String.IsNullOrEmpty (m.ArgsType))
					return m.ArgsType;
				if (m.IsSimpleEventHandler)
					return "EventArgs";
				nameBase = m.AdjustedName;
				start = nameBase.StartsWith ("On") ? 2 : 0;
			} else {
				if (!String.IsNullOrEmpty (ArgsType))
					return ArgsType;
				if (m.IsSimpleEventHandler)
					return "EventArgs";
				nameBase = Name;
				start = Name.StartsWith ("IOn") ? 3 : 1;
				trim = 8; // "Listener"
			}
			return nameBase.Substring (start, nameBase.Length - start - trim) + "EventArgs";
		}
		
		public override void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			opt.CodeGenerator.WriteInterface (this, sw, indent, opt, gen_info);
		}

		public override void Generate (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			gen_info.CurrentType = FullName;

			StreamWriter sw = gen_info.Writer = gen_info.OpenStream(opt.GetFileName (FullName));

			sw.WriteLine ("using System;");
			sw.WriteLine ("using System.Collections.Generic;");
			sw.WriteLine ("using Android.Runtime;");
			if (opt.CodeGenerationTarget != CodeGenerationTarget.XamarinAndroid) {
				sw.WriteLine ("using Java.Interop;");
			}
			sw.WriteLine ();
			sw.WriteLine ("namespace {0} {{", Namespace);
			sw.WriteLine ();

			Generate (sw, "\t", opt, gen_info);

			sw.WriteLine ("}");
			sw.Close ();
			gen_info.Writer = null;
			
			GenerateAnnotationAttribute (opt, gen_info);
		}

		#region IRequireGenericMarshal implementation.
		// SymbolTable.Lookup() for IList/IDictioanry/etc. results in this InterfaceGen,
		// so we also have to override this property here.
		public string GetGenericJavaObjectTypeOverride ()
		{
			int idx = FullName.IndexOf ('<');
			return SymbolTable.GetGenericJavaObjectTypeOverride (
				idx < 0 ? FullName : FullName.Substring (0, idx),
				idx < 0 ? null : FullName.Substring (idx + 1).TrimEnd ('>'));
		}

		public string ToInteroperableJavaObject (string var_name)
		{
			return GetGenericJavaObjectTypeOverride () != null ? SymbolTable.GetNativeName (var_name) : var_name;
		}
		#endregion
	}
}

