using System;
using System.IO;
using System.Xml;

namespace MonoDroid.Generation
{

	public abstract class Ctor : MethodBase {

		protected Ctor (GenBase declaringType)
			: base (declaringType)
		{
		}
		
		public abstract bool IsNonStaticNestedType { get; }
		public abstract string CustomAttributes { get; }

		string jni_sig;
		public string JniSignature {
			get { return jni_sig; }
		}

		public string ID {
			get { return "id_ctor" + IDSignature; }
		}

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList tps)
		{
			if (!base.OnValidate (opt, tps))
				return false;
			jni_sig = "(" + Parameters.JniSignature + ")V";
			return true;
		}
	}
}
