using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceMemberAlternativeClass : ClassWriter
	{
		readonly List<TypeWriter> sibling_classes = new List<TypeWriter> ();

		// Historically .NET has not allowed interface implemented fields or constants, so we
		// initially worked around that by moving them to an abstract class, generally
		// IMyInterface -> MyInterfaceConsts
		// This was later expanded to accomodate static interface methods, creating a more appropriately named class
		// IMyInterface -> MyInterface
		// In this case the XXXConsts class is [Obsolete]'d and simply inherits from the newer class
		// in order to maintain backward compatibility.
		// If we're creating a binding that supports DIM, we remove the XXXConsts class as they've been
		// [Obsolete:iserror] for a long time, and we add [Obsolete] to the interface "class".
		public InterfaceMemberAlternativeClass (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var should_obsolete = opt.SupportInterfaceConstants && opt.SupportDefaultInterfaceMethods;

			Name = iface.HasManagedName
				? iface.Name.Substring (1) + "Consts"
				: iface.Name.Substring (1);

			Inherits = "Java.Lang.Object";

			IsPublic = true;
			IsAbstract = true;

			UsePriorityOrder = true;

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, iface, opt);

			Attributes.Add (new RegisterAttr (iface.RawJniName, noAcw: true, additionalProperties: iface.AdditionalAttributeString ()) { AcwLast = true });

			if (should_obsolete)
				Attributes.Add (new ObsoleteAttr ($"Use the '{iface.FullName}' type. This class will be removed in a future release.") { WriteGlobal = true, NoAtSign = true });

			Constructors.Add (new ConstructorWriter { Name = Name, IsInternal = true });

			var needs_class_ref = AddFields (iface, should_obsolete, opt, context);
			AddMethods (iface, should_obsolete, opt);

			if (needs_class_ref || iface.Methods.Where (m => m.IsStatic).Any ())
				Fields.Add (new PeerMembersField (opt, iface.RawJniName, Name, false));

			if (!iface.HasManagedName && !opt.SupportInterfaceConstants)
				sibling_classes.Add (new InterfaceConstsForwardClass (iface));
		}

		void AddMethods (InterfaceGen iface, bool shouldObsolete, CodeGenerationOptions opt)
		{
			foreach (var method in iface.Methods.Where (m => m.IsStatic)) {
				var original = method.Deprecated;

				if (shouldObsolete && string.IsNullOrWhiteSpace (method.Deprecated))
					method.Deprecated = $"Use '{iface.FullName}.{method.AdjustedName}'. This class will be removed in a future release.";

				Methods.Add (new BoundMethod (iface, method, opt, true));

				var name_and_jnisig = method.JavaName + method.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
				var gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !iface.ContainsMethod (name_and_jnisig);

				if (gen_string_overload || method.IsReturnCharSequence)
					Methods.Add (new BoundMethodStringOverload (method, opt));

				if (method.Asyncify)
					Methods.Add (new MethodAsyncWrapper (method, opt));

				method.Deprecated = original;
			}
		}

		bool AddFields (InterfaceGen iface, bool shouldObsolete, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var seen = new HashSet<string> ();

			var original_fields = DeprecateFields (iface, shouldObsolete);
			var needs_class_ref = AddInterfaceFields (iface, iface.Fields, seen, opt, context);
			RestoreDeprecatedFields (original_fields);

			foreach (var i in iface.GetAllImplementedInterfaces ().OfType<InterfaceGen> ()) {
				AddInlineComment ($"// The following are fields from: {i.JavaName}");

				original_fields = DeprecateFields (i, shouldObsolete);
				needs_class_ref = AddInterfaceFields (i, i.Fields, seen, opt, context) || needs_class_ref;
				RestoreDeprecatedFields (original_fields);
			}

			return needs_class_ref;
		}

		bool AddInterfaceFields (InterfaceGen iface, List<Field> fields, HashSet<string> seen, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var needs_property = false;

			foreach (var f in fields) {
				if (iface.ContainsName (f.Name)) {
					Report.LogCodedWarning (0, SourceWriterExtensions.GetFieldCollisionMessage (iface, f), f, iface.FullName, f.Name, iface.JavaName);
					continue;
				}

				if (seen.Contains (f.Name)) {
					Report.LogCodedWarning (0, Report.WarningDuplicateField, f, iface.FullName, f.Name, iface.JavaName);
					continue;
				}

				if (f.Validate (opt, iface.TypeParameters, context)) {
					seen.Add (f.Name);
					needs_property = needs_property || f.NeedsProperty;

					if (f.NeedsProperty)
						Properties.Add (new BoundFieldAsProperty (iface, f, opt));
					else
						Fields.Add (new BoundField (iface, f, opt));
				}
			}

			return needs_property;
		}

		List<(Field field, bool deprecated, string comment)> DeprecateFields (InterfaceGen iface, bool shouldObsolete)
		{
			var original_fields = iface.Fields.Select (f => (f, f.IsDeprecated, f.DeprecatedComment)).ToList ();

			if (!shouldObsolete)
				return original_fields;

			foreach (var f in iface.Fields) {
				// Only use this derprecation if it's not already deprecated for another reason
				if (!f.IsDeprecated) {
					f.IsDeprecated = true;
					f.DeprecatedComment = $"Use '{iface.FullName}.{f.Name}'. This class will be removed in a future release."; ;
				}
			}

			return original_fields;
		}

		void RestoreDeprecatedFields (List<(Field field, bool deprecated, string comment)> fields)
		{
			foreach (var (field, deprecated, comment) in fields) {
				field.IsDeprecated = deprecated;
				field.DeprecatedComment = comment;
			}
		}

		public override void Write (CodeWriter writer)
		{
			base.Write (writer);

			WriteSiblingClasses (writer);
		}

		public void WriteSiblingClasses (CodeWriter writer)
		{
			foreach (var sibling in sibling_classes) {
				writer.WriteLine ();
				sibling.Write (writer);
			}
		}
	}

	public class InterfaceConstsForwardClass : ClassWriter
	{
		public InterfaceConstsForwardClass (InterfaceGen iface)
		{
			Name = iface.Name.Substring (1) + "Consts";
			Inherits = iface.Name.Substring (1);

			IsPublic = true;
			IsAbstract = true;

			Attributes.Add (new RegisterAttr (iface.RawJniName, noAcw: true, additionalProperties: iface.AdditionalAttributeString ()));
			Attributes.Add (new ObsoleteAttr ($"Use the '{iface.Name.Substring (1)}' type. This type will be removed in a future release.", true) { NoAtSign = true, WriteGlobal = true });

			Constructors.Add (new ConstructorWriter {
				Name = Name,
				IsPrivate = true
			});
		}
	}
}
