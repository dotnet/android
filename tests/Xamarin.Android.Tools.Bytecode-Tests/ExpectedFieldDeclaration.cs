using System;
using System.Linq;

using Xamarin.Android.Tools.Bytecode;

using NAssert   = NUnit.Framework.Assert;

namespace Xamarin.Android.Tools.BytecodeTests {

	class ExpectedFieldDeclaration {

		public  string              Name;
		public  string              Descriptor;
		public  string              GenericDescriptor;
		public  string              ConstantValue;
		public  bool                Deprecated;
		public  FieldAccessFlags    AccessFlags;

		public void Assert (FieldInfo field)
		{
			NAssert.AreEqual (AccessFlags,          field.AccessFlags,      string.Format ("Access flags for field '{0}' ({1}) doesn't match expected ({2})!", Name, field.AccessFlags, AccessFlags));
			NAssert.AreEqual (Name,                 field.Name,             string.Format ("Field name '{0}' doesn't match expected '{1}'!", Name, field.Name));
			NAssert.AreEqual (Descriptor,           field.Descriptor,       string.Format ("Descriptor for field '{0}' ({1}) doesn't match expected ({2})!", Name, field.Descriptor, Descriptor));
			NAssert.AreEqual (GenericDescriptor,    field.GetSignature (),  string.Format ("GenericDescriptor for field '{0}' ({1}) doesn't match expected ({2})!", Name, field.GetSignature (), GenericDescriptor));
			NAssert.AreEqual (Deprecated,           field.Attributes.Get<DeprecatedAttribute>() != null,
					string.Format ("Deprecated for field '{0}' ({1}) doesn't match expected ({2})!", Name, !Deprecated, Deprecated));
			if (ConstantValue != null) {
				ConstantValueAttribute  cvalue  = (ConstantValueAttribute) field.Attributes.FirstOrDefault (a => a.Name == "ConstantValue");
				NAssert.IsNotNull (cvalue,  string.Format ("No constant found for field '{0}'!", Name));
				NAssert.AreEqual (ConstantValue, cvalue.Constant.ToString (),
						string.Format ("ConstantValue for field '{0}' ({1}) doesn't match expected ({2})!", Name, cvalue.Constant.ToString (), ConstantValue));
			}
		}
	}
}

