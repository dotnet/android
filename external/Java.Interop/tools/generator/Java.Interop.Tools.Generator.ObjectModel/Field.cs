using System;
using System.Collections.Generic;
using MonoDroid.Utils;

namespace MonoDroid.Generation
{
	public class Field : ApiVersionsSupport.IApiAvailability, ISourceLineInfo
	{
		public string Annotation { get; set; }
		public int ApiAvailableSince { get; set; }
		public string DeprecatedComment { get; set; }
		public bool IsAcw { get; set; }
		public bool IsDeprecated { get; set; }
		public bool IsDeprecatedError { get; set; }
		public bool IsEnumified { get; set; }
		public bool IsFinal { get; set; }
		public bool IsStatic { get; set; }
		public ParameterList SetParameters { get; private set; }
		public ISymbol Symbol { get; private set; }
		public string JavaName { get; set; }
		public string Name { get; set; }
		public bool NotNull { get; set; }
		public Parameter SetterParameter { get; set; }
		public string TypeName { get; set; }
		public string Value { get; set; }
		public string Visibility { get; set; }

		public int LineNumber { get; set; } = -1;
		public int LinePosition { get; set; } = -1;
		public string SourceFile { get; set; }

		internal string GetMethodPrefix => TypeNameUtilities.GetCallPrefix (Symbol);

		internal string ID => JavaName + "_jfieldId";

		public bool IsConst => IsFinal && IsStatic;

		public bool NeedsProperty => !IsStatic || !IsFinal || string.IsNullOrEmpty (Value) || Symbol.IsArray || !primitive_types.Contains (Symbol.JavaName);

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			Symbol = opt.SymbolTable.Lookup (TypeName, type_params);

			if (Symbol == null || !Symbol.Validate (opt, type_params, context)) {
				Report.LogCodedWarning (0, Report.WarningUnexpectedFieldType, this, TypeName, context.GetContextTypeMember ());
				return false;
			}

			if (!string.IsNullOrEmpty (Value) && Symbol != null && Symbol.FullName == "char" && !Value.StartsWith ("(char)"))
				Value = "(char)" + Value;

			SetParameters = new ParameterList {
				SetterParameter,
			};

			if (!SetParameters.Validate (opt, type_params, context))
				throw new NotSupportedException (
					string.Format ("Unable to generate setter parameter list {0}", context.ContextString));

			return true;
		}

		static readonly HashSet<string> primitive_types = new HashSet<string> {
			"boolean",
			"char",
			"byte",
			"short",
			"int",
			"long",
			"float",
			"double",
			// while technically not a primitive type, Strings have the feature that
			// their value is stored within bytecode and their value is thus accessible
			// within the api.xml description.
			"java.lang.String",
		};
	}
}
