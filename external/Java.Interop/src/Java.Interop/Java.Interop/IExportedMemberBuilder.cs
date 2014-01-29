using System;
using System.Collections.Generic;

namespace Java.Interop {

	public interface IExportedMemberBuilder {

		IEnumerable<JniNativeMethodRegistration> GetExportedMemberRegistrations (Type declaringType);
	}
}

