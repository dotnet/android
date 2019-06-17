using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;


namespace MonoDroid.Generation {

	public class Property {

		string name;

		public Property (string name)
		{
			this.name = name;
		}

		public Method Getter {get; set;}
		public Method Setter {get; set;}

		public bool IsGeneric {
			get { return Getter.IsGeneric; }
		}

		// This is a workaround for generaing compatibility for Android.Graphics.Drawables.ColorDrawable.SetColor (wrt bug #4288).
		public bool GenerateDispatchingSetter { get; set; }

		internal string AdjustedName {
			get { return Getter.ReturnType.StartsWith ("Java.Lang.ICharSequence") ? name + "Formatted" : name; }
		}

		public string Name {
			get { return name; }
			set { name = value; }
		}

		public string Type {
			get { return Setter != null ? Setter.Parameters [0].Type : Getter.ReturnType; }
		}
	}
}

