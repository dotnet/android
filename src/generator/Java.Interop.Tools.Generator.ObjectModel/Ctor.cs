using System;
using System.IO;
using System.Xml;

namespace MonoDroid.Generation
{
	public class Ctor : MethodBase
	{
		public Ctor (GenBase declaringType) : base (declaringType)
		{
		}

		public string CustomAttributes { get; set; }
		public bool IsNonStaticNestedType { get; set; }
		public string JniSignature { get; private set; }
		public bool MissingEnclosingClass { get; set; }

		public string ID => "id_ctor" + IDSignature;

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList tps, CodeGeneratorContext context)
		{
			if (MissingEnclosingClass)
				return false;

			if (!base.OnValidate (opt, tps, context))
				return false;

			JniSignature = "(" + Parameters.JniSignature + ")V";

			return true;
		}
	}
}
