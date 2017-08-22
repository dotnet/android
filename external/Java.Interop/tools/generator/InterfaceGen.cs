using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Mono.Cecil;

using Xamarin.Android.Binder;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation {
#if HAVE_CECIL
	public class ManagedInterfaceGen : InterfaceGen {
		public ManagedInterfaceGen (TypeDefinition t)
			: base (new ManagedGenBaseSupport (t))
		{
			foreach (var ifaceImpl in t.Interfaces) {
				AddInterface (ifaceImpl.InterfaceType.FullNameCorrected ());
			}
			foreach (var m in t.Methods) {
				if (m.IsPrivate || m.IsAssembly || !m.CustomAttributes.Any (ca => ca.AttributeType.FullNameCorrected () == "Android.Runtime.RegisterAttribute"))
					continue;
				AddMethod (new ManagedMethod (this, m));
			}
		}

		public override string ArgsType {
			get { throw new NotImplementedException (); }
		}

		public override bool MayHaveManagedGenericArguments {
			get { return !this.IsAcw; }
		}
	}
#endif	// HAVE_CECIL
	
	public class XmlInterfaceGen : InterfaceGen {

		string args_type;

		public XmlInterfaceGen (XElement pkg, XElement elem) 
			: base (new InterfaceXmlGenBaseSupport (pkg, elem))
		{
			hasManagedName = elem.Attribute ("managedName") != null;
			args_type = elem.XGetAttribute ("argsType");
			foreach (var child in elem.Elements ()) {
				switch (child.Name.LocalName) {
				case "implements":
					string iname = child.XGetAttribute ("name-generic-aware");
					iname = iname.Length > 0 ? iname : child.XGetAttribute ("name");
					AddInterface (iname);
					break;
				case "method":
					if (child.XGetAttribute ("synthetic") != "true")
						AddMethod (new XmlMethod (this, child));
					break;
				case "field":
					AddField (new XmlField (child));
					break;
				case "typeParameters":
					break; // handled at GenBaseSupport
				default:
					Report.Warning (0, Report.WarningInterfaceGen + 0, "unexpected interface child {0}.", child);
					break;
				}
			}
		}
		
		public override string ArgsType {
			get { return args_type; }
		}
	}
	
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

		bool NeedsSender {
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

		void GenMethods (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			foreach (Method m in Methods.Where (m => !m.IsStatic)) {
				if (m.Name == Name || ContainsProperty (m.Name, true))
					m.Name = "Invoke" + m.Name;
				m.GenerateDeclaration (sw, indent, opt, this, AssemblyQualifiedName + "Invoker");
			}
		}

		void GenExtensionMethods (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			foreach (Method m in Methods.Where (m => !m.IsStatic)) {
				m.GenerateExtensionOverload (sw, indent, opt, FullName);
				m.GenerateExtensionAsyncWrapper (sw, indent, opt, FullName);
			}
		}

		void GenProperties (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			foreach (Property prop in Properties.Where (p => !p.Getter.IsStatic))
				prop.GenerateDeclaration (sw, indent, opt, this, AssemblyQualifiedName + "Invoker");
		}

		void GenerateInvoker (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\", DoNotGenerateAcw=true{2})]", indent, RawJniName, this.AdditionalAttributeString ());
			sw.WriteLine ("{0}internal class {1}Invoker : global::Java.Lang.Object, {1} {{", indent, Name);
			sw.WriteLine ();
			opt.CodeGenerator.WriteInterfaceInvokerHandle (this, sw, indent + "\t", opt, Name + "Invoker");
			sw.WriteLine ("{0}\tIntPtr class_ref;", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}\tpublic static {1} GetObject (IntPtr handle, JniHandleOwnership transfer)", indent, Name);
			sw.WriteLine ("{0}\t{{", indent);
			sw.WriteLine ("{0}\t\treturn global::Java.Lang.Object.GetObject<{1}> (handle, transfer);", indent, Name);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}\tstatic IntPtr Validate (IntPtr handle)", indent);
			sw.WriteLine ("{0}\t{{", indent);
			sw.WriteLine ("{0}\t\tif (!JNIEnv.IsInstanceOf (handle, java_class_ref))", indent);
			sw.WriteLine ("{0}\t\t\tthrow new InvalidCastException (string.Format (\"Unable to convert instance of type '{{0}}' to type '{{1}}'.\",", indent);
			sw.WriteLine ("{0}\t\t\t\t\t\tJNIEnv.GetClassNameFromInstance (handle), \"{1}\"));", indent, JavaName);
			sw.WriteLine ("{0}\t\treturn handle;", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}\tprotected override void Dispose (bool disposing)", indent);
			sw.WriteLine ("{0}\t{{", indent);
			sw.WriteLine ("{0}\t\tif (this.class_ref != IntPtr.Zero)", indent);
			sw.WriteLine ("{0}\t\t\tJNIEnv.DeleteGlobalRef (this.class_ref);", indent);
			sw.WriteLine ("{0}\t\tthis.class_ref = IntPtr.Zero;", indent);
			sw.WriteLine ("{0}\t\tbase.Dispose (disposing);", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}\tpublic {1}Invoker (IntPtr handle, JniHandleOwnership transfer) : base (Validate (handle), transfer)", indent, Name);
			sw.WriteLine ("{0}\t{{", indent);
			sw.WriteLine ("{0}\t\tIntPtr local_ref = JNIEnv.GetObjectClass ({1});", indent, opt.ContextType.GetObjectHandleProperty ("this"));
			sw.WriteLine ("{0}\t\tthis.class_ref = JNIEnv.NewGlobalRef (local_ref);", indent);
			sw.WriteLine ("{0}\t\tJNIEnv.DeleteLocalRef (local_ref);", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();

			HashSet<string> members = new HashSet<string> ();
			GenerateInvoker (sw, Properties.Where (p => !p.Getter.IsStatic), indent + "\t", opt, members);
			GenerateInvoker (sw, Methods.Where (m => !m.IsStatic), indent + "\t", opt, members);
			if (FullName == "Java.Lang.ICharSequence")
				GenCharSequenceEnumerator (sw, indent + "\t", opt);

			foreach (InterfaceGen iface in GetAllDerivedInterfaces ()) {
				GenerateInvoker (sw, iface.Properties.Where (p => !p.Getter.IsStatic), indent + "\t", opt, members);
				GenerateInvoker (sw, iface.Methods.Where (m => !m.IsStatic && !IsCovariantMethod (m) && !(iface.FullName.StartsWith ("Java.Lang.ICharSequence") && m.Name.EndsWith ("Formatted"))), indent + "\t", opt, members);
				if (iface.FullName == "Java.Lang.ICharSequence")
					GenCharSequenceEnumerator (sw, indent + "\t", opt);
			}
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		void GenerateInvoker (StreamWriter sw, IEnumerable<Property> properties, string indent, CodeGenerationOptions opt, HashSet<string> members)
		{
			foreach (Property prop in properties) {
				if (members.Contains (prop.Name))
					continue;
				members.Add (prop.Name);
				prop.GenerateInvoker (sw, indent, opt, this);
			}
		}

		void GenerateInvoker (StreamWriter sw, IEnumerable<Method> methods, string indent, CodeGenerationOptions opt, HashSet<string> members)
		{
			foreach (Method m in methods.Where (m => !m.IsStatic)) {
				string sig = m.GetSignature ();
				if (members.Contains (sig))
					continue;
				members.Add (sig);
				m.GenerateInvoker (sw, indent, opt, this);
			}
		}

		string GetEventDelegateName (Method m)
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

		string GetArgsName (Method m)
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

		void GenerateEventHandler (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			if (!IsListener)
				return;
			//Method m = Methods [0];
			foreach (var method in Methods.Where (m => m.EventName != string.Empty))
				GenerateEventArgs (method, sw, indent, opt);
			GenerateEventHandlerImpl (sw, indent, opt);
		}

		void GenerateEventArgs (Method m, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			string args_name = GetArgsName (m);
			if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty) {
				if (!m.IsSimpleEventHandler || m.IsEventHandlerWithHandledProperty) {
					sw.WriteLine ("{0}public partial class {1} : global::System.EventArgs {{", indent, args_name);
					sw.WriteLine ();
					var signature = m.Parameters.GetSignatureDropSender (opt);
					sw.WriteLine ("{0}\tpublic {1} ({2}{3}{4})", indent, args_name,
							m.IsEventHandlerWithHandledProperty ? "bool handled" : "",
							(m.IsEventHandlerWithHandledProperty && signature.Length != 0) ? ", " : "",
							signature);
					sw.WriteLine ("{0}\t{{", indent);
					if (m.IsEventHandlerWithHandledProperty)
						sw.WriteLine ("{0}\t\tthis.handled = handled;", indent);
					foreach (Parameter p in m.Parameters)
						if (!p.IsSender)
							sw.WriteLine ("{0}\t\tthis.{1} = {1};", indent, opt.GetSafeIdentifier (p.Name));
					sw.WriteLine ("{0}\t}}", indent);
					if (m.IsEventHandlerWithHandledProperty) {
						sw.WriteLine ();
						sw.WriteLine ("{0}\tbool handled;", indent);
						sw.WriteLine ("{0}\tpublic bool Handled {{", indent);
						sw.WriteLine ("{0}\t\tget {{ return handled; }}", indent);
						sw.WriteLine ("{0}\t\tset {{ handled = value; }}", indent);
						sw.WriteLine ("{0}\t}}", indent);
					}
					foreach (Parameter p in m.Parameters) {
						if (p.IsSender)
							continue;
						sw.WriteLine ();
						sw.WriteLine ("{0}\t{1} {2};", indent, opt.GetOutputName (p.Type), opt.GetSafeIdentifier (p.Name));
						// AbsListView.IMultiChoiceModeListener.onItemCheckedStateChanged() hit this strict name check, at parameter "@checked".
						sw.WriteLine ("{0}\tpublic {1} {2} {{", indent, opt.GetOutputName (p.Type), p.PropertyName);
						sw.WriteLine ("{0}\t\tget {{ return {1}; }}", indent, opt.GetSafeIdentifier (p.Name));
						sw.WriteLine ("{0}\t}}", indent);
					}
					sw.WriteLine ("{0}}}", indent);
					sw.WriteLine ();
				}
			} else {
				sw.WriteLine ("{0}public delegate {1} {2} ({3});", indent, opt.GetOutputName (m.RetVal.FullName), GetEventDelegateName (m), GenBase.GetSignature (m, opt));
				sw.WriteLine ();
			}
		}
		
		void GenerateEventHandlerImpl (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			string jniClass = "mono/" + RawJniName.Replace ('$', '_') + "Implementor";
			sw.WriteLine ("{0}[global::Android.Runtime.Register (\"{1}\"{2})]", indent, jniClass, this.AdditionalAttributeString ());
			sw.WriteLine ("{0}internal sealed partial class {1}Implementor : global::Java.Lang.Object, {1} {{", indent, Name);
			bool needs_sender = NeedsSender;
			if (needs_sender) {
				sw.WriteLine ();
				sw.WriteLine ("{0}\tobject sender;", indent);
			}
			sw.WriteLine ();
			sw.WriteLine ("{0}\tpublic {1}Implementor ({2})", indent, Name, needs_sender ? "object sender" : "");
			sw.WriteLine ("{0}\t\t: base (", indent);
			sw.WriteLine ("{0}\t\t\tglobal::Android.Runtime.JNIEnv.StartCreateInstance (\"{1}\", \"()V\"),", indent, jniClass);
			sw.WriteLine ("{0}\t\t\tJniHandleOwnership.TransferLocalRef)", indent);
			sw.WriteLine ("{0}\t{{", indent);
			sw.WriteLine ("{0}\t\tglobal::Android.Runtime.JNIEnv.FinishCreateInstance ({1}, \"()V\");", indent, GetObjectHandleProperty ("this"));
			if (needs_sender)
				sw.WriteLine ("{0}\t\tthis.sender = sender;", indent);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ();
			var handlers = new List<string> ();
			foreach (var m in Methods)
				GenerateEventHandlerImplContent (m, sw, indent, opt, needs_sender, jniClass, handlers);
			sw.WriteLine ();
			sw.WriteLine ("{0}\tinternal static bool __IsEmpty ({1}Implementor value)", indent, Name);
			sw.WriteLine ("{0}\t{{", indent);
			if (!Methods.Any (m => m.EventName != string.Empty) || handlers.Count == 0)
				sw.WriteLine ("{0}\t\treturn true;", indent);
			else
				sw.WriteLine ("{0}\t\treturn {1};", indent,
						string.Join (" && ", handlers.Select (e => string.Format ("value.{0}Handler == null", e))));
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}
		
		void GenerateEventHandlerImplContent (Method m, StreamWriter sw, string indent, CodeGenerationOptions opt, bool needs_sender, string jniClass, List<string> handlers)
		{
			string methodSpec = Methods.Count > 1 ? m.AdjustedName : String.Empty;
			handlers.Add (methodSpec);
			string args_name = GetArgsName (m);
			if (m.EventName != string.Empty) {
				sw.WriteLine ("#pragma warning disable 0649");
				sw.WriteLine ("{0}\tpublic {1} {2}Handler;", indent, GetEventDelegateName (m), methodSpec);
				sw.WriteLine ("#pragma warning restore 0649");
			}
			sw.WriteLine ();
			sw.WriteLine ("{0}\tpublic {1} {2} ({3})", indent, m.RetVal.FullName, m.Name, GenBase.GetSignature (m, opt));
			sw.WriteLine ("{0}\t{{", indent);
			if (m.EventName == string.Empty) {
				// generate nothing
			} else if (m.IsVoid) {
				sw.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				sw.WriteLine ("{0}\t\tif (__h != null)", indent);
				sw.WriteLine ("{0}\t\t\t__h ({1}, new {2} ({3}));", indent, needs_sender ? "sender" : m.Parameters.SenderName, args_name, m.Parameters.CallDropSender);
			} else if (m.IsEventHandlerWithHandledProperty) {
				sw.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				sw.WriteLine ("{0}\t\tif (__h == null)", indent);
				sw.WriteLine ("{0}\t\t\treturn {1};", indent, m.RetVal.DefaultValue);
				var call = m.Parameters.CallDropSender;
				sw.WriteLine ("{0}\t\tvar __e = new {1} (true{2}{3});", indent, args_name,
						call.Length != 0 ? ", " : "",
						call);
				sw.WriteLine ("{0}\t\t__h ({1}, __e);", indent, needs_sender ? "sender" : m.Parameters.SenderName);
				sw.WriteLine ("{0}\t\treturn __e.Handled;", indent);
			} else {
				sw.WriteLine ("{0}\t\tvar __h = {1}Handler;", indent, methodSpec);
				sw.WriteLine ("{0}\t\treturn __h != null ? __h ({1}) : default ({2});", indent, m.Parameters.GetCall (opt), opt.GetOutputName (m.RetVal.FullName));
			}
			sw.WriteLine ("{0}\t}}", indent);
		}
		
		public void GenerateEventsOrPropertiesForListener (StreamWriter sw, string indent, CodeGenerationOptions opt, ClassGen target)
		{
			var methods = target.Methods.Concat (target.Properties.Where (p => p.Setter != null).Select (p => p.Setter));
			var props = new HashSet<string> ();
			var refs = new HashSet<string> ();
			var eventMethods = methods.Where (m => m.IsListenerConnector && m.EventName != String.Empty && m.ListenerType == this).Distinct ();
			foreach (var method in eventMethods) {
				string name = method.CalculateEventName (target.ContainsName);
				if (String.IsNullOrEmpty (name)) {
					Report.Warning (0, Report.WarningInterfaceGen + 1, "empty event name in {0}.{1}.", FullName, method.Name);
					continue;
				}
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 4, "event name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", FullName, method.Name);
					continue;
				}
				var prop = target.Properties.FirstOrDefault (p => p.Setter == method);
				if (prop != null) {
					string setter = "__Set" + prop.Name;
					props.Add (prop.Name);
					refs.Add (setter);
					GenerateEventOrProperty (sw, indent, target, opt, name, setter,
						string.Format ("__v => {0} = __v", prop.Name),
						string.Format ("__v => {0} = null", prop.Name));
				} else {
					refs.Add (method.Name);
					string rm = null;
					string remove;
					if (method.Name.StartsWith ("Set"))
						remove = string.Format ("__v => {0} (null)", method.Name);
					else if (method.Name.StartsWith ("Add") &&
					         (rm = "Remove" + method.Name.Substring ("Add".Length)) != null &&
					         methods.Where (m => m.Name == rm).Any ())
						remove = string.Format ("__v => {0} (__v)", rm);
					else
						remove = string.Format ("__v => {{throw new NotSupportedException (\"Cannot unregister from {0}.{1}\");}}",
							FullName, method.Name);
					GenerateEventOrProperty (sw, indent, target, opt, name, method.Name,
						method.Name,
						remove);
				}
			}

			foreach (var r in refs) {
				sw.WriteLine ("{0}WeakReference weak_implementor_{1};", indent, r);
			}
			sw.WriteLine ();
			sw.WriteLine ("{0}{1}Implementor __Create{2}Implementor ()", indent, opt.GetOutputName (FullName), Name);
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\treturn new {1}Implementor ({2});", indent, opt.GetOutputName (FullName),
				NeedsSender ? "this" : "");
			sw.WriteLine ("{0}}}", indent);
		}
		
		public void GenerateEventOrProperty (StreamWriter sw, string indent, ClassGen target, CodeGenerationOptions opt, string name, string connector_fmt, string add, string remove)
		{
			if (!is_valid)
				return;
			foreach (var m in Methods) {
				string nameSpec = Methods.Count > 1 ? m.EventName ?? m.AdjustedName : String.Empty;
				string nameUnique = String.IsNullOrEmpty (nameSpec) ? name : nameSpec;
				if (nameUnique.StartsWith ("On"))
					nameUnique = nameUnique.Substring (2);
				if (target.ContainsName (nameUnique))
					nameUnique += "Event";
				GenerateEventOrProperty (m, sw, indent, target, opt, nameUnique, connector_fmt, add, remove);
			}
		}
		
		void GenerateEventOrProperty (Method m, StreamWriter sw, string indent, ClassGen target, CodeGenerationOptions opt, string name, string connector_fmt, string add, string remove)
		{
			if (m.EventName == string.Empty)
				return;
			string nameSpec = Methods.Count > 1 ? m.AdjustedName : String.Empty;
			int idx = FullName.LastIndexOf (".");
			int start = Name.StartsWith ("IOn") ? 3 : 1;
			string full_delegate_name = FullName.Substring (0, idx + 1) + Name.Substring (start, Name.Length - start - 8) + nameSpec;
			if (m.IsSimpleEventHandler)
				full_delegate_name = "EventHandler";
			else if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty)
				full_delegate_name = "EventHandler<" + FullName.Substring (0, idx + 1) + GetArgsName (m) + ">";
			else
				full_delegate_name += "Handler";
			if (m.RetVal.IsVoid || m.IsEventHandlerWithHandledProperty) {
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 5, "event name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", FullName, name);
					return;
				} else
					GenerateEvent (sw, indent, opt, name, nameSpec, m.AdjustedName, full_delegate_name, !m.Parameters.HasSender, connector_fmt, add, remove);
			} else {
				if (opt.GetSafeIdentifier (name) != name) {
					Report.Warning (0, Report.WarningInterfaceGen + 6, "event property name for {0}.{1} is invalid. `eventName' or `argsType` can be used to assign a valid member name.", FullName, name);
					return;
				}
				sw.WriteLine ("{0}WeakReference weak_implementor_{1};", indent, name);
				sw.WriteLine ("{0}{1}Implementor Impl{2} {{", indent, opt.GetOutputName (FullName), name);
				sw.WriteLine ("{0}\tget {{", indent);
				sw.WriteLine ("{0}\t\tif (weak_implementor_{1} == null || !weak_implementor_{1}.IsAlive)", indent, name);
				sw.WriteLine ("{0}\t\t\treturn null;", indent);
				sw.WriteLine ("{0}\t\treturn weak_implementor_{1}.Target as {2}Implementor;", indent, name, opt.GetOutputName (FullName));
				sw.WriteLine ("{0}\t}}", indent);
				sw.WriteLine ("{0}\tset {{ weak_implementor_{1} = new WeakReference (value, true); }}", indent, name);
				sw.WriteLine ("{0}}}", indent);
				sw.WriteLine ();
				GenerateProperty (sw, indent, opt, name, nameSpec, m.AdjustedName, connector_fmt, full_delegate_name);
			}
		}

		void GenerateEvent (StreamWriter sw, string indent, CodeGenerationOptions opt, string name, string nameSpec, string methodName, string full_delegate_name, bool needs_sender, string wrefSuffix, string add, string remove)
		{
			sw.WriteLine ("{0}public event {1} {2} {{", indent, opt.GetOutputName (full_delegate_name), name);
			sw.WriteLine ("{0}\tadd {{", indent);
			sw.WriteLine ("{0}\t\tglobal::Java.Interop.EventHelper.AddEventHandler<{1}, {1}Implementor>(",
					indent, opt.GetOutputName (FullName));
			sw.WriteLine ("{0}\t\t\t\tref weak_implementor_{1},", indent, wrefSuffix);
			sw.WriteLine ("{0}\t\t\t\t__Create{1}Implementor,", indent, Name);
			sw.WriteLine ("{0}\t\t\t\t{1},", indent, add);
			sw.WriteLine ("{0}\t\t\t\t__h => __h.{1}Handler += value);", indent, nameSpec);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ("{0}\tremove {{", indent);
			sw.WriteLine ("{0}\t\tglobal::Java.Interop.EventHelper.RemoveEventHandler<{1}, {1}Implementor>(",
					indent, opt.GetOutputName (FullName));
			sw.WriteLine ("{0}\t\t\t\tref weak_implementor_{1},", indent, wrefSuffix);
			sw.WriteLine ("{0}\t\t\t\t{1}Implementor.__IsEmpty,", indent, opt.GetOutputName (FullName));
			sw.WriteLine ("{0}\t\t\t\t{1},", indent, remove);
			sw.WriteLine ("{0}\t\t\t\t__h => __h.{1}Handler -= value);", indent, nameSpec);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		void GenerateProperty (StreamWriter sw, string indent, CodeGenerationOptions opt, string name, string nameSpec, string methodName, string connector_fmt, string full_delegate_name)
		{
			string handlerPrefix = Methods.Count > 1 ? methodName : string.Empty;
			sw.WriteLine ("{0}public {1} {2} {{", indent, opt.GetOutputName (full_delegate_name), name);
			sw.WriteLine ("{0}\tget {{", indent);
			sw.WriteLine ("{0}\t\t{1}Implementor impl = Impl{2};", indent, opt.GetOutputName (FullName), name);
			sw.WriteLine ("{0}\t\treturn impl == null ? null : impl.{1}Handler;", indent, handlerPrefix);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ("{0}\tset {{", indent);
			sw.WriteLine ("{0}\t\t{1}Implementor impl = Impl{2};", indent, opt.GetOutputName (FullName), name);
			sw.WriteLine ("{0}\t\tif (impl == null) {{", indent);
			sw.WriteLine ("{0}\t\t\timpl = new {1}Implementor ({2});", indent, opt.GetOutputName (FullName), NeedsSender ? "this" : string.Empty);
			sw.WriteLine ("{0}\t\t\tImpl{1} = impl;", indent, name);
			sw.WriteLine ("{0}\t\t}} else", indent);
			sw.WriteLine ("{0}\t\t\timpl.{1}Handler = value;", indent, nameSpec);
			sw.WriteLine ("{0}\t}}", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}
		
		// For each interface, generate either an abstract method or an explicit implementation method.
		public void GenerateAbstractMembers (ClassGen gen, StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			foreach (Method m in Methods.Where (m => !m.IsInterfaceDefaultMethod && !m.IsStatic)) {
				bool mapped = false;
				string sig = m.GetSignature ();
				if (opt.ContextGeneratedMethods.Any (_ => _.Name == m.Name && _.JniSignature == m.JniSignature))
					continue;
				for (var cls = gen; cls != null; cls = cls.BaseGen)
					if (cls.ContainsMethod (m, false) || cls != gen && gen.ExplicitlyImplementedInterfaceMethods.Contains (sig)) {
						mapped = true;
						break;
					}
				if (mapped)
					continue;
				if (gen.ExplicitlyImplementedInterfaceMethods.Contains (sig))
					m.GenerateExplicitInterfaceImplementation (sw, indent, opt, this);
				else
					m.GenerateAbstractDeclaration (sw, indent, opt, this, gen);
				opt.ContextGeneratedMethods.Add (m); 
			}
			foreach (Property prop in Properties.Where (p => !p.Getter.IsStatic)) {
				if (gen.ContainsProperty (prop.Name, false))
					continue;
				prop.GenerateAbstractDeclaration (sw, indent, opt, gen);
			}
		}

		void GenerateDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (ISymbol isym in Interfaces) {
				InterfaceGen igen = (isym is GenericSymbol ? (isym as GenericSymbol).Gen : isym) as InterfaceGen;
				if (igen.IsConstSugar)
					continue;
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (opt.GetOutputName (isym.FullName));
			}

			sw.WriteLine ("{0}// Metadata.xml XPath interface reference: path=\"{1}\"", indent, MetadataXPathReference);

			if (this.IsDeprecated)
				sw.WriteLine ("{0}[ObsoleteAttribute (@\"{1}\")]", indent, DeprecatedComment);
			sw.WriteLine ("{0}[Register (\"{1}\", \"\", \"{2}\"{3})]", indent, RawJniName, Namespace + "." + FullName.Substring (Namespace.Length + 1).Replace ('.', '/') + "Invoker", this.AdditionalAttributeString ());
			if (this.TypeParameters != null && this.TypeParameters.Any ())
				sw.WriteLine ("{0}{1}", indent, TypeParameters.ToGeneratedAttributeString ());
			sw.WriteLine ("{0}{1} partial interface {2} : {3} {{", indent, Visibility, Name,
				Interfaces.Count == 0 || sb.Length == 0 ? "IJavaObject" : sb.ToString ());
			sw.WriteLine ();
			GenProperties (sw, indent + "\t", opt);
			GenMethods (sw, indent + "\t", opt);
			sw.WriteLine (indent + "}");
			sw.WriteLine ();
		}

		public void GenerateExtensionsDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt, string declaringTypeName)
		{
			if (!Methods.Any (m => m.CanHaveStringOverload) && !Methods.Any (m => m.Asyncify))
				return;

			sw.WriteLine ("{0}public static partial class {1}{2}Extensions {{", indent, declaringTypeName, Name);
			GenExtensionMethods (sw, indent + "\t", opt);
			sw.WriteLine (indent + "}");
			sw.WriteLine ();
		}

		public override void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			opt.ContextTypes.Push (this);
			// interfaces don't nest, so generate as siblings
			foreach (GenBase nest in NestedTypes) {
				nest.Generate (sw, indent, opt, gen_info);
				sw.WriteLine ();
			}

			var staticMethods = Methods.Where (m => m.IsStatic);
			if (Fields.Any () || staticMethods.Any ()) {
				string name = hasManagedName
					? Name.Substring (1) + "Consts"
					: Name.Substring (1);
				sw.WriteLine ("{0}[Register (\"{1}\"{2}, DoNotGenerateAcw=true)]", indent, RawJniName, this.AdditionalAttributeString ());
				sw.WriteLine ("{0}public abstract class {1} : Java.Lang.Object {{", indent, name);
				sw.WriteLine ();
				sw.WriteLine ("{0}\tinternal {1} ()", indent, name);
				sw.WriteLine ("{0}\t{{", indent);
				sw.WriteLine ("{0}\t}}", indent);

				var seen = new HashSet<string> ();
				bool needsClassRef = GenFields (sw, indent + "\t", opt, seen) || staticMethods.Any ();
				foreach (var iface in GetAllImplementedInterfaces ().OfType<InterfaceGen>()) {
					sw.WriteLine ();
					sw.WriteLine ("{0}\t// The following are fields from: {1}", indent, iface.JavaName);
					bool v = iface.GenFields (sw, indent + "\t", opt, seen);
					needsClassRef = needsClassRef || v;
				}

				foreach (var m in Methods.Where (m => m.IsStatic))
					m.Generate (sw, indent + "\t", opt, this, true);

				if (needsClassRef) {
					sw.WriteLine ();
					opt.CodeGenerator.WriteClassHandle (this, sw, indent + "\t", opt, name);
				}

				sw.WriteLine ("{0}}}", indent, Name);
				sw.WriteLine ();

				if (!hasManagedName) {
					sw.WriteLine ("{0}[Register (\"{1}\"{2}, DoNotGenerateAcw=true)]", indent, RawJniName, this.AdditionalAttributeString ());
					sw.WriteLine ("{0}[global::System.Obsolete (\"Use the '{1}' type. This type will be removed in a future release.\")]", indent, name);
					sw.WriteLine ("{0}public abstract class {1}Consts : {1} {{", indent, name);
					sw.WriteLine ();
					sw.WriteLine ("{0}\tprivate {1}Consts ()", indent, name);
					sw.WriteLine ("{0}\t{{", indent);
					sw.WriteLine ("{0}\t}}", indent);
					sw.WriteLine ("{0}}}", indent);
					sw.WriteLine ();
				}
			}

			if (IsConstSugar)
				return;

			GenerateDeclaration (sw, indent, opt);
			if (!AssemblyQualifiedName.Contains ('/'))
				GenerateExtensionsDeclaration (sw, indent, opt, null);
			GenerateInvoker (sw, indent, opt);
			GenerateEventHandler (sw, indent, opt);
			opt.ContextTypes.Pop ();
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

