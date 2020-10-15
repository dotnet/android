using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDroid.Generation
{
	public class CodeGeneratorContext
	{
		public Stack<GenBase> ContextTypes { get; } = new Stack<GenBase> ();
		public List<Method> ContextGeneratedMethods { get; set; } = new List<Method> ();
		public Field ContextField { get; set; }
		public MethodBase ContextMethod { get; set; }

		public GenBase ContextType => ContextTypes.Any () ? ContextTypes.Peek () : null;
		string ContextFieldString => ContextField != null ? "in field " + ContextField.Name + " " : null;
		string ContextMethodString => ContextMethod != null ? "in method " + ContextMethod.Name + " " : null;
		string ContextTypeString => ContextType != null ? "in managed type " + ContextType.FullName : null;
		public string ContextString => ContextFieldString + ContextMethodString + ContextTypeString;

		public string GetContextTypeMember ()
		{
			var parts = new List<string> ();

			// Type name
			if (!string.IsNullOrEmpty (ContextType?.FullName))
				parts.Add (ContextType?.FullName);

			// Member name
			if (ContextMethod != null)
				parts.Add ($"{ContextMethod.Name} ({string.Join (", ", ContextMethod?.Parameters.Select (p => p.InternalType).ToArray ())})");
			else if (ContextField != null)
				parts.Add (ContextField.Name);

			return string.Join (".", parts);
		}
	}
}
