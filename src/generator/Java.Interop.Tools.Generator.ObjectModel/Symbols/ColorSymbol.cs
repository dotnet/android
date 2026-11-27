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
	}
}


