using System;

namespace Android.App {
	sealed partial class InstrumentationAttribute : Attribute {
		public string Name { get; set; }
	}
}

