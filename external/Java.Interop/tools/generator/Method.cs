using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using Mono.Cecil;

using Xamarin.Android.Tools;

using MonoDroid.Utils;

namespace MonoDroid.Generation {
#if USE_CECIL
	public class ManagedMethod : Method {
		MethodDefinition m;
		string java_name;
		string java_return;
		bool is_acw;
		bool is_interface_default_method;

		public ManagedMethod (GenBase declaringType, MethodDefinition m)
			: this (declaringType, m, new ManagedMethodBaseSupport (m))
		{
		}
		
		ManagedMethod (GenBase declaringType, MethodDefinition m, ManagedMethodBaseSupport support)
			: base (declaringType, support)
		{
			this.m = m;
			var regatt = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			is_interface_default_method = m.CustomAttributes
				.Any (ca => ca.AttributeType.FullName == "Java.Interop.JavaInterfaceDefaultMethodAttribute");
			java_name = regatt != null ? ((string) regatt.ConstructorArguments [0].Value) : m.Name;
			
			foreach (var p in support.GetParameters (regatt))
				Parameters.Add (p);
			
			if (regatt != null) {
				var rt = support.GetJniReturnType (regatt);
				if (rt != null)
					java_return = rt.Type;
			}
			FillReturnType ();
		}

		public override bool IsAcw {
			get { return is_acw; }
		}

		public override bool IsInterfaceDefaultMethod {
			get { return is_interface_default_method; }
		}

		// Strip "Formatted" from ICharSequence-based method. Use this wherever m.Name was used.
		string NameBase {
			get { return IsReturnCharSequence ? m.Name.Substring (0, m.Name.Length - "Formatted".Length) : m.Name; }
		}

		public override string Name {
			get { return m.IsGetter ? (m.Name.StartsWith ("get_Is") && m.Name.Length > 6 && char.IsUpper (m.Name [6]) ? string.Empty : "Get") + NameBase.Substring (4) : m.IsSetter ? (m.Name.StartsWith ("set_Is") && m.Name.Length > 6 && char.IsUpper (m.Name [6])  ? string.Empty : "Set") + NameBase.Substring (4) : NameBase; }
			set { throw new NotImplementedException (); }
		}

		public override string ArgsType {
			get { throw new NotImplementedException (); }
		}

		public override string EventName {
			get { throw new NotImplementedException (); }
		}

		public override string JavaName {
			get { return java_name; }
		}

		public override bool IsAbstract {
			get { return m.IsAbstract; }
		}

		public override bool IsFinal {
			get { return m.IsFinal; }
		}

		public override bool IsStatic {
			get { return m.IsStatic; }
		}

		public override bool IsVirtual {
			get { return m.IsVirtual; }
			set { throw new NotImplementedException (); }
		}

		public override string ManagedReturn {
			get { return m.ReturnType.FullNameCorrected (); }
		}
		
		public override sealed bool IsReturnEnumified {
			get { return m.MethodReturnType.CustomAttributes.Any (c => c.AttributeType.FullName == "Android.Runtime.GeneratedEnumAttribute"); }
		}

		public override string Return {
			get { return java_return ?? m.ReturnType.FullNameCorrected (); }
		}

		protected override string PropertyNameOverride {
			get { return null; }
		}

		public override int SourceApiLevel {
			get { return 0; }
		}

		public override bool Asyncify {
			get { return false; }
		}

		public override string CustomAttributes {
			get { return null; }
		}
	}
#endif

	public class XmlMethod : Method {

		XmlElement elem;

		public XmlMethod (GenBase declaringType, XmlElement elem)
			: base (declaringType, new XmlMethodBaseSupport (elem))
		{
			this.elem = elem;
			is_static = elem.XGetAttribute ("static") == "true";
			is_virtual = !is_static && elem.XGetAttribute ("final") == "false";
			if (elem.HasAttribute ("managedName"))
				name = elem.XGetAttribute ("managedName");
			else
				name = StringRocks.MemberToPascalCase (JavaName);

			is_abstract = elem.XGetAttribute ("abstract") == "true";
			if (declaringType is InterfaceGen)
				is_interface_default_method = !is_abstract && !is_static;

			GenerateDispatchingSetter = elem.HasAttribute ("generateDispatchingSetter");

			foreach (XmlNode child in elem.ChildNodes) {
				if (child.Name == "parameter")
					Parameters.Add (Parameter.FromElement (child as XmlElement));
			}
			FillReturnType ();
		}

		// core XML-based properties

		public override string ArgsType {
			get {
				var a = elem.Attributes ["argsType"];
				if (a == null)
					return null;
				return a.Value;
			}
		}

		public override string EventName {
			get {
				var a = elem.Attributes ["eventName"];
				if (a == null)
					return null;
				return a.Value;
			}
		}

		bool is_abstract;
		public override bool IsAbstract {
			get { return is_abstract; }
		}

		public override bool IsFinal {
			get { return elem.XGetAttribute ("final") == "true"; }
		}

		bool is_interface_default_method;
		public override bool IsInterfaceDefaultMethod {
			get { return is_interface_default_method; }
		}

		public override string JavaName {
			get { return elem.XGetAttribute ("name"); }
		}

		bool is_static;
		public override bool IsStatic {
			get { return is_static; }
		}

		bool is_virtual;
		public override bool IsVirtual {
			get { return is_virtual; }
			set { is_virtual = value; }
		}

		string name;
		public override string Name {
			get { return name; }
			set { name = value; }
		}
		
		// FIXME: this should not require enumReturn. Somewhere in generator uses this property improperly.
		public override string Return {
			get { return elem.HasAttribute ("enumReturn") ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("return"); }
		}
		
		public override string ManagedReturn {
			get { return elem.HasAttribute ("enumReturn") ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("managedReturn"); }
		}
		
		public override bool IsReturnEnumified {
			get { return elem.HasAttribute ("enumReturn"); }
		}

		protected override string PropertyNameOverride {
			get {
				var pn = elem.Attributes ["propertyName"];
				if (pn == null)
					return null;
				return pn.Value;
			}
		}

		static readonly Regex ApiLevel = new Regex (@"api-(\d+).xml");
		public override int SourceApiLevel {
			get {
				string source = elem.XGetAttribute ("merge.SourceFile");
				if (source == null)
					return 0;
				Match m = ApiLevel.Match (source);
				if (!m.Success)
					return 0;
				int api;
				if (int.TryParse (m.Groups [1].Value, out api))
					return api;
				return 0;
			}
		}

		public override bool Asyncify {
			get {
				if (IsOverride)
					return false;

				return elem.HasAttribute ("generateAsyncWrapper");
			}
		}

		public override string CustomAttributes {
			get {
				if (!elem.HasAttribute ("customAttributes"))
					return null;

				return elem.GetAttribute ("customAttributes");
			}
		}
	}

	public abstract class Method : MethodBase {

		protected Method (GenBase declaringType, IMethodBaseSupport support)
			: base (declaringType, support)
		{
		}
		
		internal void FillReturnType ()
		{
			retval = new ReturnValue (this, Return, ManagedReturn, IsReturnEnumified);
		}

		public bool GenerateDispatchingSetter { get; protected set; }

		public abstract string ArgsType { get; }
		public abstract string EventName { get; }
		public abstract bool IsAbstract { get; }
		public abstract bool IsFinal { get; }
		public abstract bool IsInterfaceDefaultMethod { get; }
		public abstract string JavaName { get; }
		public abstract bool IsStatic { get; }
		public abstract bool IsVirtual { get; set; }
		public abstract string Return { get; }
		public abstract bool IsReturnEnumified { get; }
		public abstract string ManagedReturn { get; }
		protected abstract string PropertyNameOverride { get; }
		public abstract int SourceApiLevel { get; }
		public abstract bool Asyncify { get; }
		public abstract string CustomAttributes { get; }

		// convenience properties
		public bool CanAdd {
			get {
				return Name.Length > 3 && Name.StartsWith ("Add") && Name.EndsWith ("Listener") && Parameters.Count == 1 && IsVoid &&
					!(Parameters [0].IsArray);
			}
		}

		public bool CanSet {
			get {
				return Name.Length > 3 && Name.StartsWith ("Set") && Parameters.Count == 1 && IsVoid &&
					!(Parameters [0].IsArray);
			}
		}

		public string DefaultReturn {
			get { return RetVal.DefaultValue; }
		}

		public bool IsPropertyAccessor {
			get { return CanGet || CanSet; }
		}

		public string PropertyName {
			get {
				if (!IsPropertyAccessor)
					throw new InvalidOperationException ("Not a property: " + Name);
				var pn = PropertyNameOverride;
				if (pn != null)
					return pn;
				var nameBase = Name;
				if (CanAdd || CanSet || Name.StartsWith ("Get"))
					nameBase = Name.Substring (3);
				if (IsAbstract && (CanGet && RetVal.IsGeneric || CanSet && Parameters [0].IsGeneric) &&
				    DeclaringType is ClassGen) // Interface methods cannot be RawXxx (because they are not generic so far...)
					return "Raw" + nameBase;
				return nameBase;
			}
		}

		public bool CanGet {
			get {
				return Parameters.Count == 0 &&
					!IsVoid && !RetVal.IsArray &&
					((Name.Length > 4 && Name.StartsWith ("Get") && char.IsUpper (Name [3])) ||
					((Name.Length > 4 && Name.StartsWith ("Has") && char.IsUpper (Name [3]) && retval.JavaName == "boolean") ||
					 (Name.Length > 3 && Name.StartsWith ("Is")  && char.IsUpper (Name [2]) && retval.JavaName == "boolean")));
			}
		}

		public override bool IsGeneric {
			get { return base.IsGeneric || RetVal.IsGeneric; }
		}

		public bool IsListenerConnector {
			get { return (CanAdd || CanSet) && Parameters [0].IsListener; }
		}

		internal bool IsReturnCharSequence {
			get { return RetVal.FullName.StartsWith ("Java.Lang.ICharSequence"); }
		}

		internal bool IsSimpleEventHandler {
			get { return RetVal.IsVoid && (Parameters.Count == 0 || (Parameters.HasSender && Parameters.Count == 1)); }
		}
		
		public bool IsEventHandlerWithHandledProperty {
			get { return RetVal.JavaName == "boolean" && EventName != ""; }
		}

		bool is_override;
		public bool IsOverride {
			get { return !IsStatic && is_override; }
			set { is_override = value; }
		}

		public bool IsInterfaceDefaultMethodOverride { get; set; }

		public bool IsVoid {
			get { return RetVal.JavaName == "void"; }
		}

		string jni_sig;
		public string JniSignature {
			get {
				if (jni_sig == null)
					jni_sig = "(" + Parameters.JniSignature + ")" + RetVal.JniName;
				return jni_sig;
			}
		}

		public InterfaceGen ListenerType {
			get { return Parameters [0].ListenerType; }
		}

		ReturnValue retval;
		public ReturnValue RetVal {
			get { return retval; }
		}

		public string ReturnType {
			get { return RetVal.FullName; }
		}

		public string GetMetadataXPathReference (GenBase declaringType)
		{
			return string.Format ("{0}/method[@name='{1}'{2}]", declaringType.MetadataXPathReference, JavaName, Parameters.GetMethodXPathPredicate ());
		}
		
		internal string CalculateEventName (Func<string, bool> checkNameDuplicate)
		{
			string event_name = EventName;
			if (event_name == null) {
				event_name = Name.Substring (0, Name.Length - 8).Substring (3);
				if (event_name.StartsWith ("On"))
					event_name = event_name.Substring (2);
				if (checkNameDuplicate (event_name))
					event_name += "Event";
			}
			return event_name;
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			if (GenericArguments != null)
				GenericArguments.Validate (opt, type_params);
			var tpl = GenericParameterDefinitionList.Merge (type_params, GenericArguments);
			if (!retval.Validate (opt, tpl))
				return false;

			return base.OnValidate (opt, tpl);
		}

		string connector_name;
		public string ConnectorName {
			get {
				if (connector_name == null)
					connector_name = "Get" + Name + IDSignature + "Handler";
				return connector_name;
			}
		}

		string escaped_cb_name;
		string EscapedCallbackName {
			get {
				if (escaped_cb_name == null)
					escaped_cb_name = "cb_" + JavaName + IDSignature;
				return escaped_cb_name;
			}
		}

		string escaped_id_name;
		internal string EscapedIdName {
			get {
				if (escaped_id_name == null)
					escaped_id_name = "id_" + JavaName.Replace ("<", "_x60_").Replace (">", "_x62_") + IDSignature;
				return escaped_id_name;
			}
		}

		string delegate_type;
		string GetDelegateType ()
		{
			if (delegate_type == null) {
				string parms = Parameters.DelegateTypeParams;
				if (IsVoid)
					delegate_type = String.Format ("Action<IntPtr, IntPtr{0}>", parms);
				else
					delegate_type = String.Format ("Func<IntPtr, IntPtr{0}, {1}>", parms, RetVal.NativeType);
			}
			return delegate_type;
		}
		
		// it used to be private though...
		internal string AdjustedName {
			get { return IsReturnCharSequence ? Name + "Formatted" : Name; }
		}

		public void GenerateCallback (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type, string property_name)
		{
			GenerateCallback (sw, indent, opt, type, property_name, false);
		}

		#region "if you're changing this part, also change method in CallbackCode.cs"
		void GenerateCallback (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type, string property_name, bool as_formatted)
		{
			string delegate_type = GetDelegateType ();
			sw.WriteLine ("{0}static Delegate {1};", indent, EscapedCallbackName);
			sw.WriteLine ("#pragma warning disable 0169");
			sw.WriteLine ("{0}static Delegate {1} ()", indent, ConnectorName);
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\tif ({1} == null)", indent, EscapedCallbackName);
			sw.WriteLine ("{0}\t\t{1} = JNINativeWrapper.CreateDelegate (({2}) n_{3});", indent, EscapedCallbackName, delegate_type, Name + IDSignature);
			sw.WriteLine ("{0}\treturn {1};", indent, EscapedCallbackName);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
			sw.WriteLine ("{0}static {1} n_{2} (IntPtr jnienv, IntPtr native__this{3})", indent, RetVal.NativeType, Name + IDSignature, Parameters.GetCallbackSignature (opt));
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\t{1} __this = global::Java.Lang.Object.GetObject<{1}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);", indent, opt.GetOutputName (type.FullName));
			foreach (string s in Parameters.GetCallbackPrep (opt))
				sw.WriteLine ("{0}\t{1}", indent, s);
			if (String.IsNullOrEmpty (property_name)) {
				string call = "__this." + Name + (as_formatted ? "Formatted" : String.Empty) + " (" + Parameters.GetCall (opt) + ")";
				if (IsVoid)
					sw.WriteLine ("{0}\t{1};", indent, call);
				else
					sw.WriteLine ("{0}\t{1} {2};", indent, Parameters.HasCleanup ? RetVal.NativeType + " __ret =" : "return", RetVal.ToNative (opt, call));
			} else {
				if (IsVoid)
					sw.WriteLine ("{0}\t__this.{1} = {2};", indent, property_name, Parameters.GetCall (opt));
				else
					sw.WriteLine ("{0}\t{1} {2};", indent, Parameters.HasCleanup ? RetVal.NativeType + " __ret =" : "return", RetVal.ToNative (opt, "__this." + property_name));
			}
			foreach (string cleanup in Parameters.GetCallbackCleanup (opt))
				sw.WriteLine ("{0}\t{1}", indent, cleanup);
			if (!IsVoid && Parameters.HasCleanup)
				sw.WriteLine ("{0}\treturn __ret;", indent);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ("#pragma warning restore 0169");
			sw.WriteLine ();
		}
		#endregion

		public void GenerateCustomAttributes (StreamWriter sw, string indent)
		{
			if (this.GenericArguments != null && this.GenericArguments.Any ())
				sw.WriteLine ("{0}{1}", indent, GenericArguments.ToGeneratedAttributeString ());
			if (CustomAttributes != null)
				sw.WriteLine ("{0}{1}", indent, CustomAttributes);
			if (Annotation != null)
				sw.WriteLine ("{0}{1}", indent, Annotation);
		}

		public void GenerateBody (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			opt.CodeGenerator.WriteMethodBody (this, sw, indent, opt);
		}

		public void GenerateExplicitInterfaceImplementation (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase iface)
		{
//			sw.WriteLine ("// explicitly implemented method from " + iface.FullName);
			GenerateCustomAttributes (sw, indent);
			sw.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (RetVal.FullName), opt.GetOutputName (iface.FullName), Name, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\treturn {1} ({2});", indent, Name, Parameters.GetCall (opt));
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateExplicitInterfaceInvoker (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase iface)
		{
			//sw.WriteLine ("\t\t// explicitly implemented invoker method from " + iface.FullName);
			GenerateIdField (sw, indent, opt);
			sw.WriteLine ("{0}unsafe {1} {2}.{3} ({4})",
					indent, opt.GetOutputName (RetVal.FullName), opt.GetOutputName (iface.FullName), Name, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			GenerateBody (sw, indent + "\t", opt);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateAbstractDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt, InterfaceGen gen, GenBase impl)
		{
			if (RetVal.IsGeneric && gen != null) {
				GenerateCustomAttributes (sw, indent);
				sw.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (RetVal.FullName), opt.GetOutputName (gen.FullName), Name, GenBase.GetSignature (this, opt));
				sw.WriteLine ("{0}{{", indent);
				sw.WriteLine ("{0}\tthrow new NotImplementedException ();", indent);
				sw.WriteLine ("{0}}}", indent);
				sw.WriteLine ();
			} else {
				bool gen_as_formatted = IsReturnCharSequence;
				string name = AdjustedName;
				GenerateCallback (sw, indent, opt, impl, null, gen_as_formatted);
				sw.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, GetMetadataXPathReference (this.DeclaringType));
				sw.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, JavaName, JniSignature, ConnectorName, this.AdditionalAttributeString ());
				GenerateCustomAttributes (sw, indent);
				sw.WriteLine ("{0}{1} abstract {2} {3} ({4});", indent, Visibility, opt.GetOutputName (RetVal.FullName), name, GenBase.GetSignature (this, opt));
				sw.WriteLine ();

				if (gen_as_formatted || Parameters.HasCharSequence)
					GenerateStringOverload (sw, indent, opt);
			}

			GenerateAsyncWrapper (sw, indent, opt);
		}

		public void GenerateDeclaration (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type, string adapter)
		{
			sw.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, GetMetadataXPathReference (this.DeclaringType));
			if (Deprecated != null)
				sw.WriteLine ("[Obsolete (@\"{0}\")]", Deprecated.Replace ("\"", "\"\""));
			if (IsReturnEnumified)
				sw.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			if (IsInterfaceDefaultMethod)
				sw.WriteLine ("{0}[global::Java.Interop.JavaInterfaceDefaultMethod]", indent);
			sw.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})]", indent, JavaName, JniSignature, ConnectorName, GetAdapterName (opt, adapter), this.AdditionalAttributeString ());
			GenerateCustomAttributes (sw, indent);
			sw.WriteLine ("{0}{1} {2} ({3});", indent, opt.GetOutputName (RetVal.FullName), AdjustedName, GenBase.GetSignature (this, opt));
			sw.WriteLine ();
		}

		public void GenerateEventDelegate (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}public delegate {1} {2}EventHandler ({3});", indent, opt.GetOutputName (RetVal.FullName), Name, GenBase.GetSignature (this, opt));
			sw.WriteLine ();
		}

		// This is supposed to generate instantiated generic method output, but I don't think it is done yet.
		public void GenerateExplicitIface (StreamWriter sw, string indent, CodeGenerationOptions opt, GenericSymbol gen)
		{
			sw.WriteLine ("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
			GenerateCustomAttributes (sw, indent);
			sw.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (RetVal.FullName), opt.GetOutputName (gen.Gen.FullName), Name, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			Dictionary<string, string> mappings = new Dictionary<string, string> ();
			for (int i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters[i].Name] = gen.TypeParams [i].FullName;
			GenerateGenericBody (sw, indent + "\t", opt, null, String.Empty, mappings);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		void GenerateGenericBody (StreamWriter sw, string indent, CodeGenerationOptions opt, string property_name, string container_prefix, Dictionary<string, string> mappings)
		{
			if (String.IsNullOrEmpty (property_name)) {
				string call = container_prefix + Name + " (" + Parameters.GetGenericCall (opt, mappings) + ")";
				sw.WriteLine ("{0}{1}{2};", indent, IsVoid ? String.Empty : "return ", RetVal.GetGenericReturn (opt, call, mappings));
			} else {
				if (IsVoid) // setter
					sw.WriteLine ("{0}{1} = {2};", indent, container_prefix + property_name, Parameters.GetGenericCall (opt, mappings));
				else // getter
					sw.WriteLine ("{0}return {1};", indent, RetVal.GetGenericReturn (opt, container_prefix + property_name, mappings));
			}
		}

		public void GenerateIdField (StreamWriter sw, string indent, CodeGenerationOptions opt, bool invoker = false)
		{
			if (invoker) {
				sw.WriteLine ("{0}IntPtr {1};", indent, EscapedIdName);
				return;
			}
			opt.CodeGenerator.WriteMethodIdField (this, sw, indent, opt);
		}

		public void GenerateInvoker (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type)
		{
			GenerateCallback (sw, indent, opt, type, null, IsReturnCharSequence);
			GenerateIdField (sw, indent, opt, invoker:true);
			sw.WriteLine ("{0}public unsafe {1}{2} {3} ({4})",
			              indent, IsStatic ? "static " : string.Empty, opt.GetOutputName (RetVal.FullName), AdjustedName, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			GenerateInvokerBody (sw, indent + "\t", opt);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateInvokerBody (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			sw.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, EscapedIdName);
			sw.WriteLine ("{0}\t{1} = JNIEnv.GetMethodID (class_ref, \"{2}\", \"{3}\");", indent, EscapedIdName, JavaName, JniSignature);
			foreach (string prep in Parameters.GetCallPrep (opt))
				sw.WriteLine ("{0}{1}", indent, prep);
			Parameters.WriteCallArgs (sw, indent, opt, invoker:true);
			string env_method = "Call" + RetVal.CallMethodPrefix + "Method";
			string call = "JNIEnv." + env_method + " (" +
				opt.ContextType.GetObjectHandleProperty ("this") + ", " + EscapedIdName + Parameters.GetCallArgs (opt, invoker:true) + ")";
			if (IsVoid)
				sw.WriteLine ("{0}{1};", indent, call);
			else
				sw.WriteLine ("{0}{1}{2};", indent, Parameters.HasCleanup ? opt.GetOutputName (RetVal.FullName) + " __ret = " : "return ", RetVal.FromNative (opt, call, true));

			foreach (string cleanup in Parameters.GetCallCleanup (opt))
				sw.WriteLine ("{0}{1}", indent, cleanup);

			if (!IsVoid && Parameters.HasCleanup)
				sw.WriteLine ("{0}return __ret;", indent);
		}

		public string GetSignature ()
		{
			return String.Format ("n_{0}:{1}:{2}", JavaName, JniSignature, ConnectorName);
		}

		void GenerateStringOverloadBody (StreamWriter sw, string indent, CodeGenerationOptions opt, bool haveSelf)
		{
			var call = new System.Text.StringBuilder ();
			foreach (Parameter p in Parameters) {
				string pname = p.Name;
				if (p.Type == "Java.Lang.ICharSequence") {
					pname = p.GetName ("jls_");
					sw.WriteLine ("{0}global::Java.Lang.String {1} = {2} == null ? null : new global::Java.Lang.String ({2});", indent, pname, p.Name);
				} else if (p.Type == "Java.Lang.ICharSequence[]" || p.Type == "params Java.Lang.ICharSequence[]") {
					pname = p.GetName ("jlca_");
					sw.WriteLine ("{0}global::Java.Lang.ICharSequence[] {1} = CharSequence.ArrayFromStringArray({2});", indent, pname, p.Name);
				}
				if (call.Length > 0)
					call.Append (", ");
				call.Append (pname);
			}
			sw.WriteLine ("{0}{1}{2}{3} ({4});", indent, RetVal.IsVoid ? String.Empty : opt.GetOutputName (RetVal.FullName) + " __result = ", haveSelf ? "self." : "", AdjustedName, call.ToString ());
			foreach (Parameter p in Parameters) {
				if (p.Type == "Java.Lang.ICharSequence")
					sw.WriteLine ("{0}if ({1} != null) {1}.Dispose ();", indent, p.GetName ("jls_"));
				else if (p.Type == "Java.Lang.ICharSequence[]")
					sw.WriteLine ("{0}if ({1} != null) foreach (global::Java.Lang.String s in {1}) if (s != null) s.Dispose ();", indent, p.GetName ("jlca_"));
			}
			switch (RetVal.FullName) {
			case "void":
				break;
			case "Java.Lang.ICharSequence[]":
				sw.WriteLine ("{0}return CharSequence.ArrayToStringArray (__result);", indent);
				break;
			case "Java.Lang.ICharSequence":
				sw.WriteLine ("{0}return __result == null ? null : __result.ToString ();", indent);
				break;
			default:
				sw.WriteLine ("{0}return __result;", indent);
				break;
			}
		}

		void GenerateStringOverload (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			string static_arg = IsStatic ? " static" : String.Empty;
			string ret = opt.GetOutputName (RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			if (Deprecated != null)
				sw.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, Deprecated.Replace ("\"", "\"\"").Trim ());
			sw.WriteLine ("{0}{1}{2} {3} {4} ({5})", indent, Visibility, static_arg, ret, Name, GenBase.GetSignature (this, opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
			sw.WriteLine ("{0}{{", indent);
			GenerateStringOverloadBody (sw, indent + "\t", opt, false);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public bool CanHaveStringOverload {
			get { return IsReturnCharSequence || Parameters.HasCharSequence; }
		}

		public void GenerateExtensionOverload (StreamWriter sw, string indent, CodeGenerationOptions opt, string selfType)
		{
			if (!CanHaveStringOverload)
				return;

			string ret = opt.GetOutputName (RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			sw.WriteLine ();
			sw.WriteLine ("{0}public static {1} {2} (this {3} self, {4})",
					indent, ret, Name, selfType,
				GenBase.GetSignature (this, opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
			sw.WriteLine ("{0}{{", indent);
			GenerateStringOverloadBody (sw, indent + "\t", opt, true);
			sw.WriteLine ("{0}}}", indent);
		}

		public void GenerateAsyncWrapper (StreamWriter sw, string indent, CodeGenerationOptions opt)
		{
			if (!Asyncify)
				return;

			string static_arg = IsStatic ? " static" : String.Empty;
			string ret;

			if (IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (RetVal.FullName) + ">";

			sw.WriteLine ("{0}{1}{2} {3} {4}Async ({5})", indent, Visibility, static_arg, ret, AdjustedName, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => {1} ({2}));", indent, AdjustedName, Parameters.GetCall (opt));
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void GenerateExtensionAsyncWrapper (StreamWriter sw, string indent, CodeGenerationOptions opt, string selfType)
		{
			if (!Asyncify)
				return;

			string ret;

			if (IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (RetVal.FullName) + ">";

			sw.WriteLine ("{0}public static {1} {2}Async (this {3} self{4}{5})", indent, ret, AdjustedName, selfType, Parameters.Count > 0 ? ", " : string.Empty, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			sw.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => self.{1} ({2}));", indent, AdjustedName, Parameters.GetCall (opt));
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();
		}

		public void Generate (StreamWriter sw, string indent, CodeGenerationOptions opt, GenBase type, bool generate_callbacks)
		{
			if (!IsValid)
				return;

			bool gen_as_formatted =  IsReturnCharSequence;
			if (generate_callbacks && IsVirtual)
				GenerateCallback (sw, indent, opt, type, null, gen_as_formatted);

			string name_and_jnisig = JavaName + JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
			bool gen_string_overload =  !IsOverride && Parameters.HasCharSequence && !type.ContainsMethod (name_and_jnisig);

			string static_arg = IsStatic ? " static" : String.Empty;
			string virt_ov = IsOverride ? " override" : IsVirtual ? " virtual" : String.Empty;
			if ((string.IsNullOrEmpty (virt_ov) || virt_ov == " virtual") && type.RequiresNew (AdjustedName)) {
				virt_ov = " new" + virt_ov;
			}
			string seal = IsOverride && IsFinal ? " sealed" : null;
			string ret = opt.GetOutputName (RetVal.FullName);
			GenerateIdField (sw, indent, opt);
			sw.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, GetMetadataXPathReference (this.DeclaringType));
			if (Deprecated != null)
				sw.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, Deprecated.Replace ("\"", "\"\""));
			if (IsReturnEnumified)
				sw.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			sw.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]",
				indent, JavaName, JniSignature, IsVirtual ? ConnectorName : String.Empty, this.AdditionalAttributeString ());
			GenerateCustomAttributes (sw, indent);
			sw.WriteLine ("{0}{1}{2}{3}{4} unsafe {5} {6} ({7})", indent, Visibility, static_arg, virt_ov, seal, ret, AdjustedName, GenBase.GetSignature (this, opt));
			sw.WriteLine ("{0}{{", indent);
			GenerateBody (sw, indent + "\t", opt);
			sw.WriteLine ("{0}}}", indent);
			sw.WriteLine ();

			if (gen_string_overload || gen_as_formatted)
				GenerateStringOverload (sw, indent, opt);

			GenerateAsyncWrapper (sw, indent, opt);
		}
		
		internal string GetAdapterName (CodeGenerationOptions opt, string adapter)
		{
			if (String.IsNullOrEmpty (adapter))
				return adapter;
			if (AssemblyName == null)
				return adapter + ", " + opt.AssemblyName;
			return adapter + AssemblyName;
		}
	}
}

