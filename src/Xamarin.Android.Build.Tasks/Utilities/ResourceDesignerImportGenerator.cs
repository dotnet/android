using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class ResourceDesignerImportGenerator
	{
		CodeTypeDeclaration primary;
		string primary_name;
		HashSet<string> resourceFields = new HashSet<string> ();
		TaskLoggingHelper Log;

		string CreateIdentifier (string type, string field)
		{
			return $"{primary_name}.{type}.{field}";
		}

		public ResourceDesignerImportGenerator (string ns, CodeTypeDeclaration applicationResourceDesigner, TaskLoggingHelper log)
		{
			this.primary = applicationResourceDesigner;
			primary_name = ns + (ns.Length > 0 ? "." : "") + primary.Name;
			Log = log;

			foreach (CodeTypeMember type in primary.Members) {
				var decl = type as CodeTypeDeclaration;
				if (decl == null)
					continue;
				foreach (CodeTypeMember field in decl.Members)
					resourceFields.Add (CreateIdentifier (type.Name, field.Name));
			}
		}

		public void CreateImportMethods (IEnumerable<ITaskItem> libraries)
		{
			var method = new CodeMemberMethod () { Name = "UpdateIdValues", Attributes = MemberAttributes.Public | MemberAttributes.Static };
			primary.Members.Add (method);
			foreach (var assemblyPath in libraries) {
				using (var pe = new PEReader (File.OpenRead (assemblyPath.ItemSpec))) {
					var reader = pe.GetMetadataReader ();
					var resourceDesignerName = GetResourceDesignerClass (reader);
					if (string.IsNullOrEmpty (resourceDesignerName)) {
						Log.LogDebugMessage ($"Could not find 'ResourceDesignerAttribute' in {assemblyPath}");
						continue;
					}
					string aliasMetaData = assemblyPath.GetMetadata ("Aliases");
					bool hasAlias = !string.IsNullOrEmpty (aliasMetaData);
					foreach (var handle in reader.TypeDefinitions) {
						var typeDefinition = reader.GetTypeDefinition (handle);
						if (!typeDefinition.IsNested) {
							continue;
						}
						var declaringType = reader.GetTypeDefinition (typeDefinition.GetDeclaringType ());
						var declaringTypeName = $"{reader.GetString (declaringType.Namespace)}.{reader.GetString (declaringType.Name)}";
						if (declaringTypeName == resourceDesignerName) {
							if (hasAlias) {
								string [] aliases = aliasMetaData.Split (new [] {','}, StringSplitOptions.RemoveEmptyEntries);
								foreach (var alias in aliases) {
									string aliasName = alias.Trim ();
									if (string.Compare ("global", aliasName, StringComparison.Ordinal) == 0)
										continue;
									declaringTypeName = $"{aliasName}::{declaringTypeName}";
									break;
								}
							}
							CreateImportFor (declaringTypeName, typeDefinition, method, reader, hasAlias);
						}
					}
				}
			}
		}

		string GetResourceDesignerClass (MetadataReader reader)
		{
			// Looking for:
			// [assembly: Android.Runtime.ResourceDesignerAttribute("MyApp.Resource", IsApplication=true)]

			var assembly = reader.GetAssemblyDefinition ();
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				var fullName = reader.GetCustomAttributeFullName (attribute, Log);
				if (fullName == "Android.Runtime.ResourceDesignerAttribute") {
					var values = attribute.GetCustomAttributeArguments ();
					foreach (var arg in values.NamedArguments) {
						// application resource IDs are constants, cannot merge.
						if (arg.Name == "IsApplication" && arg.Value is bool isApplication && isApplication) {
							return null;
						}
					}
					return (string) values.FixedArguments.First ().Value;
				}
			}
			return null;
		}

		void CreateImportFor (string declaringTypeFullName, TypeDefinition type, CodeMemberMethod method, MetadataReader reader, bool hasAlias)
		{
			var typeName = reader.GetString (type.Name);
			var srcClassRef = new CodeTypeReferenceExpression (
				new CodeTypeReference ($"{primary_name}.{typeName}", CodeTypeReferenceOptions.GlobalReference));
			CodeTypeReferenceOptions options = !hasAlias ? CodeTypeReferenceOptions.GlobalReference : CodeTypeReferenceOptions.GenericTypeParameter;
			var dstClassRef = new CodeTypeReferenceExpression (
				new CodeTypeReference ($"{declaringTypeFullName}.{typeName}", options));
			foreach (var handle in type.GetFields ()) {
				var fieldName = reader.GetString (reader.GetFieldDefinition (handle).Name);
				var dstField = new CodeFieldReferenceExpression (dstClassRef, fieldName);
				var srcField = new CodeFieldReferenceExpression (srcClassRef, fieldName);
				var fieldIdentifier = CreateIdentifier (typeName, fieldName);
				if (!resourceFields.Contains (fieldIdentifier)) {
					Log.LogDebugMessage ($"Value not found for {fieldIdentifier}, skipping...");
					continue;
				}
				// This simply assigns field regardless of whether it is int or int[].
				method.Statements.Add (new CodeAssignStatement (dstField, srcField));
			}
		}
	}
}

