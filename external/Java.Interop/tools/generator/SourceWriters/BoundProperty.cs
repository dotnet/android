using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundProperty : PropertyWriter
	{
		readonly MethodCallback getter_callback;
		readonly MethodCallback setter_callback;

		public BoundProperty (GenBase gen, Property property, CodeGenerationOptions opt, bool withCallbacks = true, bool forceOverride = false)
		{
			Name = property.AdjustedName;
			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property.Getter.RetVal));

			SetVisibility (gen is InterfaceGen ? string.Empty : property.Getter.IsAbstract && property.Getter.RetVal.IsGeneric ? "protected" : (property.Setter ?? property.Getter).Visibility);

			IsUnsafe = true;
			HasGet = true;

			var is_virtual = property.Getter.IsVirtual && (property.Setter == null || property.Setter.IsVirtual);

			if (is_virtual && withCallbacks) {
				IsVirtual = true;
				IsShadow = gen.RequiresNew (property);

				getter_callback = new MethodCallback (gen, property.Getter, opt, property.AdjustedName, false);

				if (property.Setter != null)
					setter_callback = new MethodCallback (gen, property.Setter, opt, property.AdjustedName, false);
			}

			if (forceOverride || ShouldForceOverride (property)) {
				IsVirtual = false;
				IsOverride = true;
			}

			if ((property.Getter ?? property.Setter).IsStatic) {
				IsStatic = true;
				IsVirtual = false;
				IsOverride = false;
			}  else if (gen.BaseSymbol != null) {
				// It should be using AdjustedName instead of Name, but ICharSequence ("Formatted") properties are not caught by this...
				var base_prop = gen.BaseSymbol.GetPropertyByName (property.Name, true);

				// If the matching base getter we found is a DIM, we do not override it, it should stay virtual
				if (base_prop != null && !base_prop.Getter.IsInterfaceDefaultMethod) {
					IsVirtual = false;
					IsOverride = true;
				}
			}

			// Allow user to override our virtual/override logic
			if (!forceOverride && (property.Getter ?? property.Setter).ManagedOverride?.ToLowerInvariant () == "virtual") {
				IsVirtual = true;
				IsOverride = false;
			} else if (!forceOverride && (property.Getter ?? property.Setter).ManagedOverride?.ToLowerInvariant () == "override") {
				IsVirtual = false;
				IsOverride = true;
			}

			// Unlike [Register], [Obsolete] cannot be put on property accessors, so we can apply them only under limited condition...
			if (property.Getter.Deprecated != null && (property.Setter == null || property.Setter.Deprecated != null))
				Attributes.Add (new ObsoleteAttr (property.Getter.Deprecated.Replace ("\"", "\"\"").Trim () + (property.Setter != null && property.Setter.Deprecated != property.Getter.Deprecated ? " " + property.Setter.Deprecated.Replace ("\"", "\"\"").Trim () : null)));

			SourceWriterExtensions.AddMethodCustomAttributes (GetterAttributes, property.Getter);

			if (gen.IsGeneratable)
				GetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Getter.JavaName}'{property.Getter.Parameters.GetMethodXPathPredicate ()}]\"");

			GetterAttributes.Add (new RegisterAttr (property.Getter.JavaName, property.Getter.JniSignature, property.Getter.IsVirtual ? property.Getter.GetConnectorNameFull (opt) : string.Empty, additionalProperties: property.Getter.AdditionalAttributeString ()));

			SourceWriterExtensions.AddMethodBody (GetBody, property.Getter, opt);

			if (property.Setter != null) {
				HasSet = true;

				if (gen.IsGeneratable)
					SetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Setter.JavaName}'{property.Setter.Parameters.GetMethodXPathPredicate ()}]\"");

				SourceWriterExtensions.AddMethodCustomAttributes (SetterAttributes, property.Setter);
				SetterAttributes.Add (new RegisterAttr (property.Setter.JavaName, property.Setter.JniSignature, property.Setter.IsVirtual ? property.Setter.GetConnectorNameFull (opt) : string.Empty, additionalProperties: property.Setter.AdditionalAttributeString ()));


				var pname = property.Setter.Parameters [0].Name;
				property.Setter.Parameters [0].Name = "value";
				SourceWriterExtensions.AddMethodBody (SetBody, property.Setter, opt);
				property.Setter.Parameters [0].Name = pname;

			} else if (property.GenerateDispatchingSetter) {
				HasSet = true;
				SetterComments.Add ("// This is a dispatching setter");
				SetBody.Add ($"Set{property.Name} (value);");
			}
		}

		public override void Write (CodeWriter writer)
		{
			// Need to write our property callbacks first
			getter_callback?.Write (writer);
			setter_callback?.Write (writer);

			base.Write (writer);
		}

		bool ShouldForceOverride (Property property)
		{
			// <TechnicalDebt>
			// This is a special workaround for AdapterView inheritance.
			// (How it is special? They have hand-written bindings AND brings generic
			// version of AdapterView<T> in the inheritance, also added by metadata!)
			//
			// They are on top of fragile hand-bound code, and when we are making changes
			// in generator, they bite. Since we are not going to bring API breakage
			// right now, we need special workarounds to get things working.
			//
			// So far, what we need here is to have AbsSpinner.Adapter compile.
			//
			// > platforms/*/src/generated/Android.Widget.AbsSpinner.cs(156,56): error CS0533:
			// > `Android.Widget.AbsSpinner.Adapter' hides inherited abstract member
			// > `Android.Widget.AdapterView<Android.Widget.ISpinnerAdapter>.Adapter
			//
			// It is because the AdapterView<T>.Adapter is hand-bound and cannot be
			// detected by generator!
			//
			// So, we explicitly treat it as a special-case.
			//
			// Then, Spinner, ListView and GridView instantiate them, so they are also special cases.
			// </TechnicalDebt>
			if (property.Name == "Adapter" &&
			    (property.Getter.DeclaringType.BaseGen.FullName == "Android.Widget.AdapterView" ||
			     property.Getter.DeclaringType.BaseGen.BaseGen != null && property.Getter.DeclaringType.BaseGen.BaseGen.FullName == "Android.Widget.AdapterView"))
				return true;
			// ... and the above breaks generator tests...
			if (property.Name == "Adapter" &&
			    (property.Getter.DeclaringType.BaseGen.FullName == "Xamarin.Test.AdapterView" ||
			     property.Getter.DeclaringType.BaseGen.BaseGen != null && property.Getter.DeclaringType.BaseGen.BaseGen.FullName == "Xamarin.Test.AdapterView"))
				return true;

			return false;
		}
	}
}
