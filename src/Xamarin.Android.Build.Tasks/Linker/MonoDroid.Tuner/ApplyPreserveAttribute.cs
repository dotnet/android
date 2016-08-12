// Copyright 2011, 2015 Xamarin Inc. All rights reserved.

using System;
using System.Collections.Generic;

using Mono.Linker;
using Mono.Tuner;

using Mono.Cecil;

namespace MonoDroid.Tuner {

	public class ApplyPreserveAttribute : ApplyPreserveAttributeBase {
		
		bool is_sdk;

		// System.ServiceModeldll is an SDK assembly but it does contain types with [DataMember] attributes
		public override bool IsActiveFor (AssemblyDefinition assembly)
		{
			is_sdk = Profile.IsSdkAssembly (assembly);
			if (is_sdk && assembly.Name.Name != "System.ServiceModel")
				return false;
			return Annotations.GetAction (assembly) == AssemblyAction.Link;
		}
		
		// SDK candidates - they will be preserved only if the application (not the SDK) uses it
		static List<ICustomAttributeProvider> srs_data_contract = new List<ICustomAttributeProvider> ();
		static List<ICustomAttributeProvider> xml_serialization = new List<ICustomAttributeProvider> ();

		public static IList<ICustomAttributeProvider> DataContract {
			get {
				return srs_data_contract;
			}
		}

		public static IList<ICustomAttributeProvider> XmlSerialization {
			get {
				return xml_serialization;
			}
		}

		protected override bool IsPreservedAttribute (ICustomAttributeProvider provider, CustomAttribute attribute, out bool removeAttribute)
		{
			removeAttribute = false;
			TypeReference type = attribute.Constructor.DeclaringType;
			
			switch (type.Namespace) {
			case "Android.Runtime":
				// there's no need to keep the [Preserve] attribute in the assembly once it was processed
				if (type.Name == "PreserveAttribute") {
					removeAttribute = true;
					return true;
				}
				break;
			case "System.Runtime.Serialization":
				bool srs = false;
				// http://bugzilla.xamarin.com/show_bug.cgi?id=1415
				// http://msdn.microsoft.com/en-us/library/system.runtime.serialization.datamemberattribute.aspx
				if (provider is PropertyDefinition || provider is FieldDefinition || provider is EventDefinition)
					srs = (type.Name == "DataMemberAttribute");
				else if (provider is TypeDefinition)
					srs = (type.Name == "DataContractAttribute");
				
				if (srs) {
					MarkDefautConstructor (provider, is_sdk ? srs_data_contract : null);
					return !is_sdk;
				}
				break;
			case "System.Xml.Serialization":
				// http://msdn.microsoft.com/en-us/library/83y7df3e.aspx
				string name = type.Name;
				if ((name.StartsWith ("Xml", StringComparison.Ordinal) && name.EndsWith ("Attribute", StringComparison.Ordinal))) {
					// but we do not have to keep things that XML serialization will ignore anyway!
					if (name != "XmlIgnoreAttribute") {
						// the default constructor of the type *being used* is needed
						MarkDefautConstructor (provider, is_sdk ? xml_serialization : null);
						return !is_sdk;
					}
				}
				break;
			default:
				if (type.Name == "PreserveAttribute") {
					// there's no need to keep the [Preserve] attribute in the assembly once it was processed
					removeAttribute = true;
					return true;
				}
				break;
			}
			// keep them (provider and attribute)
			return false;
		}
		
		// xml serialization requires the default .ctor to be present
		void MarkDefautConstructor (ICustomAttributeProvider provider, IList<ICustomAttributeProvider> list)
		{
			if (list != null) {
				list.Add (provider);
				return;
			}

			TypeDefinition td = (provider as TypeDefinition);
			if (td == null) {
				PropertyDefinition pd = (provider as PropertyDefinition);
				if (pd != null) {
					MarkDefautConstructor (pd.DeclaringType);					
					MarkGenericType (pd.PropertyType as GenericInstanceType);
					td = pd.PropertyType.Resolve ();
				} else {
					FieldDefinition fd = (provider as FieldDefinition);
					if (fd != null) {
						MarkDefautConstructor (fd.DeclaringType);
						MarkGenericType (fd.FieldType as GenericInstanceType);
						td = (fd.FieldType as TypeReference).Resolve ();
					}
				}
			}
			
			// e.g. <T> property (see bug #5543) or field (see linkall unit tests)
			if (td != null)
				MarkDefautConstructor (td);			
		}
		
		void MarkGenericType (GenericInstanceType git)
		{
			if (git == null || !git.HasGenericArguments)
				return;
			
			foreach (TypeReference tr in git.GenericArguments)
				MarkDefautConstructor (tr.Resolve ());
		}
			
		void MarkDefautConstructor (TypeDefinition type)
		{
			if ((type == null) || !type.HasMethods)
				return;
			
			foreach (MethodDefinition ctor in type.Methods) {
				if (!ctor.IsConstructor || ctor.IsStatic || ctor.HasParameters)
					continue;

				Annotations.AddPreservedMethod (type, ctor);
			}
		}
	}
}
