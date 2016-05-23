using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.LogcatParse {

	public enum JniHandleType {
		None,
		Local,
		Global,
		WeakGlobal,
	}

	public struct JniHandleInfo : IEquatable<JniHandleInfo> {

		public  readonly    string          Handle;
		public  readonly    JniHandleType   Type;
		public  readonly    string          CreatedOnThread;

		public JniHandleInfo (string handle, JniHandleType type = JniHandleType.None, string createdOnThread = null)
		{
			Handle          = handle;
			Type            = type;
			CreatedOnThread = createdOnThread;

			if (handle.Length > 2 && handle [handle.Length - 2] == '/') {
				var t   = handle [handle.Length - 1];
				Type    = GetHandleType (t);
				Handle  = handle.Substring (0, handle.Length - 2);
			}
		}

		public override int GetHashCode ()
		{
			return Handle.GetHashCode ();
		}

		public override bool Equals (object value)
		{
			var v   = value as JniHandleInfo?;
			if (!v.HasValue)
				return false;
			return Equals (v.Value);
		}

		public bool Equals (JniHandleInfo value)
		{
			return Handle == value.Handle;
		}

		public override string ToString ()
		{
			var suffix  = "";
			switch (Type) {
			case JniHandleType.Local:
				suffix = "/L";
				break;
			case JniHandleType.Global:
				suffix = "/G";
				break;
			case JniHandleType.WeakGlobal:
				suffix = "/W";
				break;
			}
			var thread = string.IsNullOrEmpty (CreatedOnThread)
				? ""
				: " from thread " + CreatedOnThread;
			return Handle + suffix + thread;
		}

		internal static JniHandleType GetHandleType (char c)
		{
			switch (c) {
			case 'L':   return JniHandleType.Local;
			case 'G':   return JniHandleType.Global;
			case 'W':   return JniHandleType.WeakGlobal;
			}
			return JniHandleType.None;
		}

		public static implicit operator JniHandleInfo (string handle)
		{
			return new JniHandleInfo (handle);
		}

		public static bool operator==(JniHandleInfo lhs, JniHandleInfo rhs)
		{
			return lhs.Equals (rhs);
		}

		public static bool operator!=(JniHandleInfo lhs, JniHandleInfo rhs)
		{
			return !lhs.Equals (rhs);
		}

		public static bool operator==(JniHandleInfo lhs, string rhs)
		{
			return lhs.Handle == rhs;
		}

		public static bool operator!=(JniHandleInfo lhs, string rhs)
		{
			return lhs.Handle != rhs;
		}

		public static bool operator==(string lhs, JniHandleInfo rhs)
		{
			return lhs == rhs.Handle;
		}

		public static bool operator!=(string lhs, JniHandleInfo rhs)
		{
			return lhs != rhs.Handle;
		}
	}

	[Flags]
	enum PeerInfoState {
		Alive,
		Collected   = 1 << 0,
		Disposed    = 1 << 1,
		Finalized   = 1 << 2,
	}

	public class PeerInfo {

		PeerInfoState                       state;

		public int?                         Pid         {get; internal set;}

		// Collected & Disposed can occur for any Peer type
		public bool                         Collected {
			get {return (state & PeerInfoState.Collected) != 0;}
			set {state |= PeerInfoState.Collected;}
		}
		public bool                         Disposed {
			get {return (state & PeerInfoState.Disposed) != 0;}
			set {state |= PeerInfoState.Disposed;}
		}

		// Finalized will only occur for IGCUserPeer types
		public bool                         Finalized {
			get {return (state & PeerInfoState.Finalized) != 0;}
			set {state |= PeerInfoState.Finalized;}
		}

		public bool Alive {
			get {return state == PeerInfoState.Alive;}
		}

		public string                       JniType     {get; internal set;}
		public string                       McwType     {get; internal set;}

		public string                       KeyHandle   {get; internal set;}
		public ISet<JniHandleInfo>          Handles     {get; private set;}

		public string                       CreatedOnThread     { get; internal set; }
		public string                       DestroyedOnThread   { get; internal set; }

		public IEnumerable<string>          StackTraces {
			get {return stacks.Values.Select (b => b.ToString ());}
		}

		Dictionary<JniHandleInfo, StringBuilder>    stacks  = new Dictionary<JniHandleInfo, StringBuilder> ();

		public PeerInfo (string pid)
		{
			Handles   = new HashSet<JniHandleInfo> ();
			KeyHandle = JniType = McwType = "";
			int p;
			if (int.TryParse (pid, NumberStyles.Integer, CultureInfo.InvariantCulture, out p))
			    Pid = p;
		}

		public string GetStackTraceForHandle (JniHandleInfo handle)
		{
			StringBuilder stack;
			if (stacks.TryGetValue (handle, out stack))
				return stack.ToString ();
			return null;
		}

		internal void AppendStackTraceForHandle (JniHandleInfo handle, string append)
		{
			if (!Handles.Contains (handle))
				Handles.Add (handle);

			StringBuilder stack;
			if (!stacks.TryGetValue (handle, out stack)) {
				stacks.Add (handle, new StringBuilder (append));
				return;
			}
			if (stack.Length > 0)
				stack.Append ("\n");
			stack.Append (append);

			if (append.Contains (".get_class_ref()")) {
				SetTypesFromStackTraceEntry (append, append.LastIndexOf (".get_class_ref()", StringComparison.Ordinal));
			} else if (append.EndsWith ("..cctor()", StringComparison.Ordinal)) {
				SetTypesFromStackTraceEntry (append, append.LastIndexOf ("..cctor()", StringComparison.Ordinal));
			} else if (append.Contains ("..ctor(")) {
				SetTypesFromStackTraceEntry (append, append.LastIndexOf ("..ctor(", StringComparison.Ordinal));
			}
		}

		void SetTypesFromStackTraceEntry (string entry, int end)
		{
			// Don't clobber previously auto-detected entries. Allows .ctor() -> .cctor() to preserve .cctor().
			if (!string.IsNullOrEmpty (JniType) && !string.IsNullOrEmpty (McwType))
				return;

			if (end < 0)
				return;
			int start   = entry.IndexOf (" at ", StringComparison.Ordinal);
			if (start < 0)
				return;
			start  += " at ".Length;
			var t   = entry.Substring (start, end - start);
			JniType = t + ".class";
			McwType = "typeof(" + t + ")";
		}

		public override string ToString ()
		{
			return string.Format ("PeerInfo(State='{0}' JniType='{1}' McwType='{2}' KeyHandle={3} Handles={4})",
					state, JniType, McwType, KeyHandle, Handles.Count);
		}
	}
}
