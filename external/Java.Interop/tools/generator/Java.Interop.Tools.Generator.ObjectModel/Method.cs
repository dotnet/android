using System;

namespace MonoDroid.Generation
{

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

