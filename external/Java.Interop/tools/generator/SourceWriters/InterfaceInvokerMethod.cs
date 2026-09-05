using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace generator.SourceWriters
{
	public class InterfaceInvokerMethod : MethodWriter
	{
		readonly MethodCallback method_callback;
		readonly CodeGenerationOptions opt;

		public InterfaceInvokerMethod (InterfaceGen iface, Method method, CodeGenerationOptions opt)
		{
			this.opt = opt;

			Name = method.AdjustedName;
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			IsPublic = true;
			IsUnsafe = true;
			IsStatic = method.IsStatic;

			if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1) {
				method_callback = new MethodCallback (iface, method, opt, null, method.IsReturnCharSequence, iface.FullName + "Invoker");
			}
			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			this.AddMethodParameters (method.Parameters, opt);
			SourceWriterExtensions.AddMethodBody (Body, method, opt, $"_members_{method.DeclaringType.JavaFullNameId}");
		}

		public override void Write (CodeWriter writer)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.XAJavaInterop1) {
				method_callback?.Write (writer);
			}

			base.Write (writer);
		}
	}
}
