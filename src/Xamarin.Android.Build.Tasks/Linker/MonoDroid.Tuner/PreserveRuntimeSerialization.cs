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
			return assembly.Name.Name == "System.Runtime.Serialization";
		}

		public override void ProcessType (Mono.Cecil.TypeDefinition type)
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
				break;
			}
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
	}
}
