using Java.Interop.Tools.TypeNameMappings;
using Mono.Cecil;
using MonoDroid.Utils;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
#if HAVE_CECIL
	public class ManagedMethod : Method {
		MethodDefinition m;
		string java_name;
		string java_return;
		bool is_acw;
		bool is_interface_default_method;

		public ManagedMethod (GenBase declaringType, MethodDefinition m)
			: base (declaringType)
		{
			this.m = m;
			GenericArguments = m.GenericArguments ();
			var regatt = m.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			is_acw = regatt != null;
			is_interface_default_method = m.CustomAttributes
				.Any (ca => ca.AttributeType.FullName == "Java.Interop.JavaInterfaceDefaultMethodAttribute");
			java_name = regatt != null ? ((string) regatt.ConstructorArguments [0].Value) : m.Name;
			
			foreach (var p in m.GetParameters (regatt))
				Parameters.Add (p);
			
			if (regatt != null) {
				var jnisig = (string)(regatt.ConstructorArguments.Count > 1 ? regatt.ConstructorArguments [1].Value : regatt.Properties.First (p => p.Name == "JniSignature").Argument.Value);
				var rt = JavaNativeTypeManager.ReturnTypeFromSignature (jnisig);
				if (rt != null)
					java_return = rt.Type;
			}
			FillReturnType ();
		}

		public override string AssemblyName => m.DeclaringType.Module.Assembly.FullName;

		public override string Deprecated => m.Deprecated ();

		public override string Visibility => m.Visibility ();

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
#endif	// HAVE_CECIL

	public class XmlMethod : Method {

		XElement elem;

		public XmlMethod (GenBase declaringType, XElement elem)
			: base (declaringType)
		{
			this.elem = elem;
			GenericArguments = elem.GenericArguments ();
			is_static = elem.XGetAttribute ("static") == "true";
			is_virtual = !is_static && elem.XGetAttribute ("final") == "false";
			if (elem.Attribute ("managedName") != null)
				name = elem.XGetAttribute ("managedName");
			else
				name = StringRocks.MemberToPascalCase (JavaName);

			is_abstract = elem.XGetAttribute ("abstract") == "true";
			if (declaringType is InterfaceGen)
				is_interface_default_method = !is_abstract && !is_static;

			GenerateDispatchingSetter = elem.Attribute ("generateDispatchingSetter") != null;

			foreach (var child in elem.Elements ()) {
				if (child.Name == "parameter")
					Parameters.Add (Parameter.FromElement (child));
			}
			FillReturnType ();
		}

		// core XML-based properties
		public override string Deprecated => elem.Deprecated ();

		public override string Visibility => elem.Visibility ();

		public override string ArgsType {
			get {
				var a = elem.Attribute ("argsType");
				if (a == null)
					return null;
				return a.Value;
			}
		}

		public override string EventName {
			get {
				var a = elem.Attribute ("eventName");
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
			get { return IsReturnEnumified ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("return"); }
		}
		
		public override string ManagedReturn {
			get { return IsReturnEnumified ? elem.XGetAttribute ("enumReturn") : elem.XGetAttribute ("managedReturn"); }
		}
		
		public override bool IsReturnEnumified {
			get { return elem.Attribute ("enumReturn") != null; }
		}

		protected override string PropertyNameOverride {
			get { return elem.XGetAttribute ("propertyName"); }
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

				return elem.Attribute ("generateAsyncWrapper") != null;
			}
		}

		public override string CustomAttributes {
			get { return elem.XGetAttribute ("customAttributes"); }
		}
	}

	public abstract class Method : MethodBase {

		protected Method (GenBase declaringType)
			: base (declaringType)
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

		public override bool Matches (MethodBase other)
		{
			bool ret = base.Matches (other);
			if (!ret)
				return ret;

			var otherMethod = other as Method;
			if (otherMethod == null)
				return false;

			if (RetVal.RawJavaType != otherMethod.RetVal.RawJavaType)
				return false;

			return true;
		}

		public string GetMetadataXPathReference (GenBase declaringType)
		{
			return string.Format ("{0}/method[@name='{1}'{2}]", declaringType.MetadataXPathReference, JavaName, Parameters.GetMethodXPathPredicate ());
		}
		
		internal string CalculateEventName (Func<string, bool> checkNameDuplicate)
		{
			string event_name = EventName;
			if (event_name == null) {
				var trimSize = Name.EndsWith ("Listener", StringComparison.Ordinal) ? 8 : 0;
				event_name = Name.Substring (0, Name.Length - trimSize).Substring (3);
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
		internal string EscapedCallbackName {
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
		internal string GetDelegateType ()
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

		public string GetSignature ()
		{
			return String.Format ("n_{0}:{1}:{2}", JavaName, JniSignature, ConnectorName);
		}

		public bool CanHaveStringOverload {
			get { return IsReturnCharSequence || Parameters.HasCharSequence; }
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

