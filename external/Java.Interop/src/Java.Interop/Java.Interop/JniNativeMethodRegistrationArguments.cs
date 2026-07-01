#nullable enable

using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public struct JniNativeMethodRegistrationArguments
	{
		const string invalidStateMessage = nameof(JniNativeMethodRegistrationArguments) + " state is invalid. Please use constructor with parameters.";

		public ICollection<JniNativeMethodRegistration> Registrations {
			get { return _registrations ?? throw new InvalidOperationException (invalidStateMessage); }
		}
		public string? Methods { get; }
		ICollection<JniNativeMethodRegistration> _registrations;

		public JniNativeMethodRegistrationArguments (ICollection<JniNativeMethodRegistration> registrations, string? methods)
		{
			_registrations = registrations ?? throw new ArgumentNullException (nameof (registrations));
			Methods = methods;
		}

		public void AddRegistrations (IEnumerable<JniNativeMethodRegistration> registrations)
		{
			if (_registrations == null)
				throw new InvalidOperationException (invalidStateMessage);

			if (registrations is List<JniNativeMethodRegistration> list) {
				list.AddRange (registrations);
			} else {
				foreach (var registration in registrations)
					_registrations.Add (registration);
			}
		}
	}
}
