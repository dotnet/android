using System;
using System.Collections.Generic;

namespace MonoDroid.Generation {

	public interface ISymbol  {

		string DefaultValue { get; }
		string FullName { get; }
		string JavaName { get; }
		string JniName { get; }
		string NativeType { get; }
		bool   IsEnum { get; }
		bool   IsArray { get; }
		string ElementType { get; }
		string ReturnCast { get; }

		// Only apply ToNative/FromNative for marshal methods. This is used when
		// we want to change the marshal type to be blittable, like bool -> byte.
		// But it will not be used for normal invocations, like InvokeVirtualBooleanMethod, etc.
		bool   OnlyFormatOnMarshal { get => false; }

		string GetObjectHandleProperty (CodeGenerationOptions opt, string variable);

		string GetGenericType (Dictionary<string, string> mappings);

		string FromNative (CodeGenerationOptions opt, string var_name, bool owned, bool isMarshal = true)
		{
			if (OnlyFormatOnMarshal && !isMarshal)
				return var_name;

			return FromNative (opt, var_name, owned);
		}

		string FromNative (CodeGenerationOptions opt, string var_name, bool owned);
		string ToNative (CodeGenerationOptions opt, string var_name, Dictionary<string, string> mappings = null);

		bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context);

		string[] PreCallback (CodeGenerationOptions opt, string var_name, bool owned);
		string[] PostCallback (CodeGenerationOptions opt, string var_name);

		string[] PreCall (CodeGenerationOptions opt, string var_name);
		string Call (CodeGenerationOptions opt, string var_name);
		string[] PostCall (CodeGenerationOptions opt, string var_name);

		bool NeedsPrep {get;}
	}
}
