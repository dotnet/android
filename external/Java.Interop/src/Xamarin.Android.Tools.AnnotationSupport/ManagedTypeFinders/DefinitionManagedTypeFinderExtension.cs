using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	// Processes *Def (IntDef,StringDef, etc.) and sets ManagedInfo on each AnnotationValue constant values.
	public class DefinitionManagedTypeFinderExtension : ManagedTypeFinderExtension
	{
		public DefinitionManagedTypeFinderExtension (ManagedTypeFinder m)
			: base (m)
		{
		}

		ManagedTypeFinder _ {
			get { return ManagedTypeFinder; }
		}

		public override void ProcessAnnotation (AnnotatedItem item)
		{
			if (item.ManagedInfo.Type.Name == null)
				return;
			
			foreach (var a in item.Annotations) {
				if (a.Values == null || a.Name.Contains ('.') || !a.Name.EndsWith ("Def", StringComparison.Ordinal))
					continue;
				for (int i = 0; i < a.Values.Count; i++) {
					var v = a.Values [i];
					if (v.Name != "value")
						continue;
					ProcessAnnotationValue (item, a, v);
				}
			}
		}

		void ProcessAnnotationValue (AnnotatedItem item, AnnotationData a, AnnotationValue v)
		{
			var x = new ConstantDefinitionExtension ();
			x.ConstantKind =
				 a.Name == "IntDef" ? ConstantKind.IntDef :
				 a.Name == "StringDef" ? ConstantKind.StringDef :
				 ConstantKind.Other;
			x.TargetManagedTypeName = GetTargetManagedTypeName (item);
			x.Flag = a.Values.Any (_ => _.Name == "flag");
			a.SetExtension (x);

			x.ManagedConstants = v.ValueAsArray.Select (s => new ManagedMemberInfo ()).ToArray ();

			if (v.ArrayItemCommonPrefix != null) {
				// Most of the *Def values share the same type as the const fields' declaring type.
				var vManagedType = _.GetContextManagedType (v.ArrayItemCommonPrefix);
				if (vManagedType == null) {
					_.Errors.Add (string.Format ("Managed type for '{0}' specified in the annotation '{1}' on '{2}' was not found",
								   v.ArrayItemCommonPrefix, a.Name, item.Name));
				} else {
					var missingConsts = new List<string> ();
					for (int c = 0; c < x.ManagedConstants.Count; c++) {
						var m = x.ManagedConstants [c];
						var mf = _.GetDefinitionField (vManagedType, v.ValueAsArray [c]);
						if (mf == null)
							// it is most likely removed constants due to enumification.
							missingConsts.Add (v.ValueAsArray [c]);
						else {
							_.SetName (m, vManagedType);
							m.MemberName = _.GetDefinitionName (mf);
						}
					}
					if (missingConsts.Count != 0 && /*x.ManagedConstants.Count != missingConsts.Count*/!x.IsTargetAlreadyEnumified)
						_.Errors.Add (string.Format ("Warning: For '{0}', managed constants are partially missing in {1}: {2}",
									   item.Name, v.ArrayItemCommonPrefix, string.Join (", ", missingConsts)));
				}
			} else {
				// Sometimes (namely "PendingIntent flags") have different declaring types and
				// in that case this lookup is somewhat complicated - we find the "type name . field name" matches.
				// As of XA 5.3, nothing should matter - the only pattern that falls here is about
				// wherever PendingIntentFlags apply.
				for (int c = 0; c < v.ValueAsArray.Count; c++) {
					var javaConst = v.ValueAsArray [c];
					var candidate = _.ContextTypes.Keys.Where (j => !string.IsNullOrEmpty (j) && (javaConst.StartsWith (j, StringComparison.Ordinal)))
								    .Select (j => _.GetContextManagedType (j))
								    .Select (mn => new { Type = mn, Field = _.GetFields (mn).FirstOrDefault (f => javaConst.EndsWith ("." + _.GetJavaName (f), StringComparison.Ordinal)) })
								    .FirstOrDefault (z => z.Field != null);
					if (candidate == null)
						continue;
					var m = x.ManagedConstants [c];
					_.SetName (m, candidate.Type);
					m.MemberName = _.GetDefinitionName (candidate.Field);
					break;
				}
			}
		}
	}
}
