using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using MonoDroid.Utils;

namespace MonoDroid.Generation
{

	public abstract class Field : ApiVersionsSupport.IApiAvailability {

		public virtual bool IsAcw {
			get { return true; }
		}

		public abstract bool IsDeprecated { get; }

		public abstract string DeprecatedComment { get; }

		public abstract bool IsFinal { get; }

		public abstract bool IsStatic { get; }

		public abstract string JavaName { get; }

		public abstract bool IsEnumified { get; }

		public abstract string TypeName { get; }

		public abstract string Name { get; set; }

		public abstract string Value { get; }

		public abstract string Visibility { get; }

		public int ApiAvailableSince { get; set; }

		protected abstract Parameter SetterParameter { get; }

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
		
		ISymbol symbol;

		ParameterList setParameters;

		internal string GetMethodPrefix {
			get { return (Symbol is SimpleSymbol || Symbol.IsEnum) ? StringRocks.MemberToPascalCase (Symbol.JavaName) : "Object"; }
		}

		public bool IsConst {
			get { return IsFinal && IsStatic; }
		}

		public bool NeedsProperty {
			get { return !IsStatic || !IsFinal || String.IsNullOrEmpty (Value) || Symbol.IsArray || !primitive_types.Contains (Symbol.JavaName); }
		}

		public ISymbol Symbol {
			get { return symbol; }
		}

		internal string ID {
			get { return JavaName + "_jfieldId"; }
		}

		internal ParameterList SetParameters {
			get { return setParameters; }
		}

		public string Annotation { get; internal set; }

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			symbol = opt.SymbolTable.Lookup (TypeName, type_params);
			
			if (symbol == null || !symbol.Validate (opt, type_params)) {
				Report.Warning (0, Report.WarningField + 0, "unexpected field type {0} {1}.", TypeName, opt.ContextString);
				return false;
			}

			setParameters = new ParameterList () {
				SetterParameter,
			};
			if (!setParameters.Validate (opt, type_params))
				throw new NotSupportedException (
					string.Format ("Unable to generate setter parameter list {0}", opt.ContextString));

			return true;
		}
	}
}
