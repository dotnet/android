using System;

namespace Java.Lang.Reflect {

	partial class InvocationTargetException {

		[Obsolete ("Use the Cause property. The Clause property was bound in error, and DOES NOT EXIST.", error:true)]
		public virtual Java.Lang.Throwable Clause {
			get {return Cause;}
		}
	}
}
