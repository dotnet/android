using System;
using Mono.Tuner;
using Mono.Cecil;

namespace MonoDroid.Tuner
{
	public class PreserveRuntimeSerialization : BaseSubStep
	{
		public override SubStepTargets Targets {
			get { return SubStepTargets.Type; }
		}

		public override bool IsActiveFor (Mono.Cecil.AssemblyDefinition assembly)
		{
			return assembly.Name.Name == "System.Runtime.Serialization" || assembly.Name.Name == "System.Xml";
		}

		bool system_runtime_serialization = false;
		bool system_xml_serialization = false;

		public override void ProcessType (TypeDefinition type)
		{
			switch (type.Namespace) {
			case "System.Runtime.Serialization.Json":
				switch (type.Name) {
				case "JsonFormatWriterInterpreter":
					TypeDefinition jwd = GetType ("System.Runtime.Serialization", "System.Runtime.Serialization.Json.JsonWriterDelegator");
					PreserveMethods (jwd);
					break;
				}
				break;
			case "System.Runtime.Serialization":
				// MS referencesource use reflection to call the required methods to serialize each PrimitiveDataContract subclasses
				// this goes thru XmlFormatGeneratorStatics and it's a better candidate (than PrimitiveDataContract) as there are other callers
				switch (type.Name) {
				case "XmlFormatGeneratorStatics":
					TypeDefinition xwd = GetType ("System.Runtime.Serialization", "System.Runtime.Serialization.XmlWriterDelegator");
					PreserveMethods (xwd);
					TypeDefinition xoswc = GetType ("System.Runtime.Serialization", "System.Runtime.Serialization.XmlObjectSerializerWriteContext");
					PreserveMethods (xoswc);
					TypeDefinition xosrc = GetType ("System.Runtime.Serialization", "System.Runtime.Serialization.XmlObjectSerializerReadContext");
					PreserveMethods (xosrc);
					TypeDefinition xrd = GetType ("System.Runtime.Serialization", "System.Runtime.Serialization.XmlReaderDelegator");
					PreserveMethods (xrd);
					break;
				case "CollectionDataContract":
					// ensure the nested type, DictionaryEnumerator and GenericDictionaryEnumerator`2, can be created thru reflection
					foreach (var nt in type.NestedTypes)
						PreserveConstructors (nt);
					break;
				}

				if (system_runtime_serialization)
					break;
				system_runtime_serialization = true;
				// if we're keeping this assembly and use the Serialization namespace inside user code then we
				// must bring the all the members decorated with [Data[Contract|Member]] attributes from the SDK
				var members = ApplyPreserveAttribute.DataContract;
				foreach (var member in members)
					MarkMetadata (member);
				members.Clear ();
				break;
			case "System.Xml.Serialization":
				if (system_xml_serialization)
					break;
				switch (type.Name) {
				case "XmlIgnoreAttribute":
					break;
				default:
					// if we're keeping this assembly and use the Serialization namespace inside user code
					// then we must bring the all the members decorated with [Xml*] attributes from the SDK
					system_xml_serialization = true;
					members = ApplyPreserveAttribute.XmlSerialization;
					foreach (var member in members)
						MarkMetadata (member);
					members.Clear ();
					break;
				}
				break;
			}
		}

		protected virtual IMetadataTokenProvider FilterExtraSerializationMembers (IMetadataTokenProvider provider)
		{
			return provider;
		}

		void MarkMetadata (IMetadataTokenProvider tp)
		{
			var provider = FilterExtraSerializationMembers (tp);
			if (provider == null)
				return;

			TypeReference tr = (provider as TypeReference);
			if (tr != null) {
				PreserveType (tr.Resolve ());
				return;
			}
			MethodReference mr = (provider as MethodReference);
			if (mr != null) {
				PreserveMethod (mr.Resolve ());
				return;
			}
			PropertyDefinition pd = (provider as PropertyDefinition);
			if (pd != null) {
				if (pd.GetMethod != null)
					PreserveMethod (pd.GetMethod);
				if (pd.SetMethod != null)
					PreserveMethod (pd.SetMethod);
				return;
			}
			// TODO: we should unify this code with xamarin-macios MobileMarkStep
			// once we move this code to mark step, we should mark events, fields and properties
		}

		protected void PreserveConstructors (TypeDefinition type)
		{
			if ((type == null) || !type.HasMethods)
				return;
			foreach (MethodDefinition ctor in type.Methods) {
				if (ctor.IsConstructor)
					PreserveMethod (type, ctor);
			}
		}

		protected AssemblyDefinition GetAssembly (string assemblyName)
		{
			AssemblyDefinition ad;
			context.TryGetLinkedAssembly (assemblyName, out ad);
			return ad;
		}

		protected TypeDefinition GetType (string assemblyName, string typeName)
		{
			AssemblyDefinition ad = GetAssembly (assemblyName);
			return ad == null ? null : GetType (ad, typeName);
		}

		protected TypeDefinition GetType (AssemblyDefinition assembly, string typeName)
		{
			return assembly.MainModule.GetType (typeName);
		}

		void PreserveMethods (TypeDefinition type)
		{
			if (type == null)
				return;

			if (!type.HasMethods)
				return;

			foreach (MethodDefinition method in type.Methods)
				PreserveMethod (type, method);
		}

		void PreserveMethod (TypeDefinition type, MethodDefinition method)
		{
			Annotations.AddPreservedMethod (type, method);
		}

		void PreserveMethod (MethodDefinition md)
		{
			if (md == null)
				return;
			if (md.DeclaringType == null)
				return;

			PreserveMethod (md.DeclaringType, md);
		}

		void PreserveType (TypeDefinition type)
		{
			if (type != null)
				Annotations.SetPreserve (type, Mono.Linker.TypePreserve.All);
		}
	}
}
