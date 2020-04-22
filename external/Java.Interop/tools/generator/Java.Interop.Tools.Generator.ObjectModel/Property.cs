using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using MonoDroid.Generation.Utilities;

namespace MonoDroid.Generation
{
	public class Property
	{
		private Method setter;

		public Property (string name)
		{
			Name = name;
		}

		public Method Getter {get; set;}

		public Method Setter {
			get => setter;
			set {
				setter = value;

				if (Getter?.RetVal?.NotNull == true)
					Setter.Parameters.First ().NotNull = true;
			}
		}

		public bool IsGeneric => Getter.IsGeneric;

		// This is a workaround for generaing compatibility for Android.Graphics.Drawables.ColorDrawable.SetColor (wrt bug #4288).
		public bool GenerateDispatchingSetter { get; set; }

		internal string AdjustedName =>
			Getter.ReturnType.StartsWith ("Java.Lang.ICharSequence") ? Name + "Formatted" : Name;

		public string Name { get; set; }

		public string Type => Setter != null ? Setter.Parameters [0].Type : Getter.ReturnType;

		public void AutoDetectEnumifiedOverrideProperties (AncestorDescendantCache cache)
		{
			if (Type != "int")
				return;

			var classes = cache.GetAncestorsAndDescendants (Getter.DeclaringType);
			classes = classes.Concat (classes.SelectMany (x => x.GetAllImplementedInterfaces ()));

			foreach (var t in classes) {
				foreach (var candidate in t.Properties.Where (p => p.Name == Name)) {
					if (Getter.JniSignature != candidate.Getter.JniSignature)
						continue;
					if (candidate.Getter.IsReturnEnumified)
						Getter.RetVal.SetGeneratedEnumType (candidate.Getter.RetVal.FullName);
				}
			}
		}
	}
}
