using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceListenerProperty : PropertyWriter
	{
		public InterfaceListenerProperty (InterfaceGen iface, string name, string nameSpec, string methodName, string fullDelegateName, CodeGenerationOptions opt)
		{
			Name = name;
			PropertyType = new TypeReferenceWriter (opt.GetOutputName (fullDelegateName)) { Nullable = opt.SupportNullableReferenceTypes };

			IsPublic = true;

			HasGet = true;

			var handlerPrefix = iface.Methods.Count > 1 ? methodName : string.Empty;

			GetBody.Add ($"{opt.GetOutputName (iface.FullName)}Implementor{opt.NullableOperator} impl = Impl{name};");
			GetBody.Add ($"return impl == null ? null : impl.{handlerPrefix}Handler;");

			HasSet = true;

			SetBody.Add ($"{opt.GetOutputName (iface.FullName)}Implementor{opt.NullableOperator} impl = Impl{name};");
			SetBody.Add ($"if (impl == null) {{");
			SetBody.Add ($"\timpl = new {opt.GetOutputName (iface.FullName)}Implementor ({(iface.NeedsSender ? "this" : string.Empty)});");
			SetBody.Add ($"\tImpl{name} = impl;");
			SetBody.Add ($"}} else");
			SetBody.Add ($"impl.{nameSpec}Handler = value;");
		}
	}
}
