using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	public class ResourceDesignerImportGenerator
	{
		CodeTypeDeclaration primary;
		string primary_name;

		public ResourceDesignerImportGenerator (string ns, CodeTypeDeclaration applicationResourceDesigner)
		{
			this.primary = applicationResourceDesigner;
			primary_name = ns + (ns.Length > 0 ? "." : "") + primary.Name;
		}

		public void CreateImportMethods (IEnumerable<AssemblyDefinition> libraries)
		{
			var method = new CodeMemberMethod () { Name = "UpdateIdValues", Attributes = MemberAttributes.Public | MemberAttributes.Static };
			primary.Members.Add (method);
			foreach (var ass in libraries) {
				var att = ass.CustomAttributes.FirstOrDefault (
					ca => ca.AttributeType.FullName == "Android.Runtime.ResourceDesignerAttribute");
				if (att == null)
					continue;
				if ((bool) att.Properties.First (p => p.Name == "IsApplication").Argument.Value == true)
					continue; // application resource IDs are constants, cannot merge.
				var td = ass.Modules.SelectMany (m => m.Types).FirstOrDefault (
					t => t.FullName == (string)att.ConstructorArguments.First ().Value);

				// F# has no nested types, so we need special care.
				if (td.NestedTypes.Any ())
					CreateImportFor (true, td.NestedTypes, method);
				else
					CreateImportFor (false, ass.Modules.SelectMany (m => m.Types).Where (t => !td.Equals (t) && t.FullName.StartsWith (td.FullName, StringComparison.Ordinal)), method);
			}
		}

		void CreateImportFor (bool isNestedSrc, IEnumerable<TypeDefinition> types, CodeMemberMethod method)
		{
			foreach (var type in types) {
				// If the library was written in F#, those resource ID classes are not nested but rather combined with '_'.
				var srcClassRef = new CodeTypeReferenceExpression (
					new CodeTypeReference (primary_name + (isNestedSrc ? '.' : '_') + type.Name, CodeTypeReferenceOptions.GlobalReference));
				// destination language may not support nested types, but they should take care of such types by themselves.
				var dstClassRef = new CodeTypeReferenceExpression (
					new CodeTypeReference (type.FullName.Replace ('/', '.'), CodeTypeReferenceOptions.GlobalReference));
				foreach (var field in type.Fields) {
					var dstField = new CodeFieldReferenceExpression (dstClassRef, field.Name);
					var srcField = new CodeFieldReferenceExpression (srcClassRef, field.Name);
					// This simply assigns field regardless of whether it is int or int[].
					method.Statements.Add (new CodeAssignStatement (dstField, srcField));
				}
			}
		}
	}
}

