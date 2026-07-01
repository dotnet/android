using System;
using System.Collections.Generic;
using System.Xml;

using MonoDroid.Utils;

namespace MonoDroid.Generation {

	public class ColorSymbol : SimpleSymbol, ISymbol {

		public ColorSymbol ()
			: base ("default (global::Android.Graphics.Color)",
					"int",
					"Android.Graphics.Color",
					"I",
					"int",
					"new global::Android.Graphics.Color ({0})",
					"{0}.ToArgb ()")
		{
		}

#if false
		public string DefaultValue {
			get {return 
		}

		public string FullName {
			get {return "Android.Graphics.Color";}
		}

		public bool IsGeneric {
			get {return false;}
		}

		public string JavaName {
			get {return "int";}
		}

		public string JniName {
			get {return "I";}
		}

		public string NativeType {
			get {return "int";}
		}

		public string FromNative (CodeGenerationOptions opt, string var_name, bool owned)
		{
			return string.Format ("new global::Android.Graphics.Color ({0})", var_name);
		}

		public string ToNative (CodeGenerationOptions opt, string var_name)
		{
			return string.Format ("{0}.ToArgb ()", var_name);
		}

		public bool Validate (GenericParameterDefinitionList type_params)
		{
			return true;
		}
#endif
	}
}


