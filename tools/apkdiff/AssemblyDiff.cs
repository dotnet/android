using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace apkdiff {
	public class AssemblyDiff : EntryDiff {

		MetadataReader reader1;
		MetadataReader reader2;

		public AssemblyDiff ()
		{
		}

		public override string Name { get { return "Assemblies"; } }

		TypeDefinition GetTypeDefinition (MetadataReader reader, TypeDefinitionHandle handle, out string fullName)
		{
			var typeDef = reader.GetTypeDefinition (handle);
			var name = reader.GetString (typeDef.Name);
			var nspace = reader.GetString (typeDef.Namespace);

			fullName = "";

			if (typeDef.IsNested) {
				string declTypeFullName;

				GetTypeDefinition (reader, typeDef.GetDeclaringType (), out declTypeFullName);
				fullName += declTypeFullName + "/";
			}

			if (!string.IsNullOrEmpty (nspace))
				fullName += nspace + ".";

			fullName += name;

			return typeDef;
		}

		public override void Compare (string file, string other, string padding)
		{
			using (var per1 = new PEReader (File.OpenRead (file))) {
				using (var per2 = new PEReader (File.OpenRead (other))) {

					reader1 = per1.GetMetadataReader ();
					reader2 = per2.GetMetadataReader ();

					var types1 = new Dictionary<string, TypeDefinition> (reader1.TypeDefinitions.Count);
					var types2 = new Dictionary<string, TypeDefinition> (reader2.TypeDefinitions.Count);

					string fullName;

					foreach (var typeHandle in reader1.TypeDefinitions) {
						var td = GetTypeDefinition (reader1, typeHandle, out fullName);
						types1 [fullName] = td;
					}

					foreach (var typeHandle in reader2.TypeDefinitions) {
						var td = GetTypeDefinition (reader2, typeHandle, out fullName);
						types2 [fullName] = td;
					}

					foreach (var pair in types1) {
						if (!types2.ContainsKey (pair.Key)) {
							Console.WriteLine ($"{padding}  -             Type {pair.Key}");
						} else
							CompareTypes (types1 [pair.Key], types2 [pair.Key], padding + "  ");
					}

					foreach (var pair in types2) {
						if (!types1.ContainsKey (pair.Key)) {
							Console.WriteLine ($"{padding}  +             Type {pair.Key}");
						}
					}
				}
			}
		}

		string GetTypeName (MetadataReader reader, EntityHandle handle)
		{
			string fullName = "";

			if (handle.Kind == HandleKind.TypeDefinition) {
				GetTypeDefinition (reader, (TypeDefinitionHandle) handle, out fullName);

				return fullName;
			}

			if (handle.Kind != HandleKind.TypeReference)
				return null;

			var typeRef = reader.GetTypeReference ((TypeReferenceHandle)handle);
			var nspace = reader.GetString (typeRef.Namespace);

			if (!string.IsNullOrEmpty (nspace))
				fullName += nspace + ".";

			return fullName += reader.GetString (typeRef.Name);
		}

		Dictionary<string, CustomAttribute> GetCustomAttributes (MetadataReader reader, CustomAttributeHandleCollection cac)
		{
			var dict = new Dictionary<string, CustomAttribute> ();

			foreach (var handle in cac) {
				var ca = reader.GetCustomAttribute (handle);
				var cHandle = ca.Constructor;

				string typeName;

				switch (cHandle.Kind) {
				case HandleKind.MethodDefinition:
					var methodDef = reader.GetMethodDefinition ((MethodDefinitionHandle)cHandle);

					typeName = GetTypeName (reader, methodDef.GetDeclaringType ());
					break;
				case HandleKind.MemberReference:
					var memberDef = reader.GetMemberReference ((MemberReferenceHandle)cHandle);

					typeName = GetTypeName (reader, memberDef.Parent);
					break;
				default:
					Program.Warning ($"Unexpected EntityHandle kind: {cHandle.Kind}");
					continue;
				}

				dict [typeName] = ca;
			}

			return dict;
		}

		void CompareCustomAttributes (CustomAttributeHandleCollection cac1, CustomAttributeHandleCollection cac2, string padding)
		{
			var dict1 = GetCustomAttributes (reader1, cac1);
			var dict2 = GetCustomAttributes (reader2, cac2);

			foreach (var pair in dict1) {
				if (!dict2.ContainsKey (pair.Key)) {
					Console.WriteLine ($"{padding}  -             CustomAttribute {pair.Key}");
				}
			}

			foreach (var pair in dict2) {
				if (!dict1.ContainsKey (pair.Key)) {
					Console.WriteLine ($"{padding}  +             CustomAttribute {pair.Key}");
				}
			}
		}

		void CompareTypes (TypeDefinition type1, TypeDefinition type2, string padding)
		{
			CompareCustomAttributes (type1.GetCustomAttributes (), type2.GetCustomAttributes (), padding);
		}
	}
}
