using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools.LogcatParse {

	public class Grefs {

		const string FilePrefix           = @"\[monodroid-gref\] ";
		const string AndroidPrefix        = @"(\d\d-\d\d \d\d:\d\d:[^:]+: )?I/monodroid-gref\(\s*(?<pid>{0})\): ";
		const string Android10Prefix      = @"(\d\d-\d\d \d\d:\d\d:[^ ]+ (?<pid>{0}) \d+ )?I monodroid-gref: ";

		const string Prefix               = "^(" + FilePrefix +
		                                    "|" + AndroidPrefix +
		                                    "|" + Android10Prefix +
		                                    ")?";
		const string ThreadAndStack       = @"(thread (?<thread>'[^']+'\([^)]+\)))?(?<stack>.*)$";
		const string AddGrefFormat        = Prefix + @"\+g\+ grefc (?<gcount>\d+) gwrefc (?<wgcount>\d+) obj-handle (?<handle>0x[0-9A-Fa-f]+)/(?<handle_type>.) -> new-handle (?<ghandle>0x[0-9A-Fa-f]+)/(?<ghandle_type>.) from " + ThreadAndStack;
		const string AddWgrefFormat       = Prefix + @"\+w\+ grefc (?<gcount>\d+) gwrefc (?<wgcount>\d+) obj-handle (?<handle>0x[0-9A-Fa-f]+)/(?<handle_type>.) -> new-handle (?<whandle>0x[0-9A-Fa-f]+)/(?<whandle_type>.) from " + ThreadAndStack;
		const string AtFormat             = Prefix + @"(?<stack>\s+at.*$)";
		const string DisposingFormat      = Prefix + @"Disposing handle (?<handle>0x[0-9A-Fa-f]+)$";
		const string FinalizingFormat     = Prefix + @"Finalizing (?<type>[^ ]+) handle (?<handle>0x[0-9A-Fa-f]+)$";
		const string HandleFormat         = Prefix + @"handle (?<handle>0x[0-9A-Fa-f]+); key_handle (?<key_handle>0x[0-9A-Fa-f]+): Java Type: `(?<jtype>[^`]+)`; MCW type: `(?<mtype>.*)`$";
		const string RemoveGrefFormat     = Prefix + @"-g- grefc (?<gcount>\d+) gwrefc (?<wgcount>\d+) handle (?<handle>0x[0-9A-Fa-f]+)/(?<handle_type>.) from " + ThreadAndStack;
		const string RemoveWgrefFormat    = Prefix + @"-w- grefc (?<gcount>\d+) gwrefc (?<wgcount>\d+) handle (?<handle>0x[0-9A-Fa-f]+)/(?<handle_type>.) from " + ThreadAndStack;

		public static Grefs Parse (string filename, int? pid = null, GrefParseOptions options = 0)
		{
			using (var source = new StreamReader (filename))
				return Parse (source, pid, options);
		}

		public static Grefs Parse (TextReader reader, int? pid = null, GrefParseOptions options = 0)
		{
			var r = new Grefs () {
				ParseOptions = options,
			};
			var h = r.CreateHandlers (pid);

			int    lineNumber = 0;
			string line;
			while ((line = reader.ReadLine ()) != null) {
				lineNumber++;
				foreach (var e in h) {
					var m = e.Key.Match (line);
					if (m.Success) {
						e.Value (m, lineNumber);
						break;
					}
				}
			}

			return r;
		}

		HashSet<JniHandleInfo>      GlobalRefs  = new HashSet<JniHandleInfo>();
		HashSet<JniHandleInfo>      WeakRefs    = new HashSet<JniHandleInfo>();

		List<PeerInfo>      allocated   = new List<PeerInfo> ();
		List<PeerInfo>      alive       = new List<PeerInfo> ();

		public GrefParseOptions         ParseOptions    {get; private set;}
		public IList<PeerInfo>          AllocatedPeers  {get; private set;}
		public IEnumerable<PeerInfo>    AlivePeers {
			get {return alive;}
		}

		public int                      GrefCount {
			get {return GlobalRefs.Count;}
		}
		public int                      WeakGrefCount {
			get {return WeakRefs.Count;}
		}

		public Grefs ()
		{
			AllocatedPeers  = new ReadOnlyCollection<PeerInfo> (allocated);
		}

		public Dictionary<string, int>  GetAliveJniTypeCounts ()
		{
			return AlivePeers.Select (p => p.JniType)
				.Distinct()
				.ToDictionary (t => t ?? "<null>", t => AlivePeers.Count (p => p.JniType == t));
		}

		public IEnumerable<string>      GetDistinctStacksForJniType (string jniType)
		{
			return AllocatedPeers.Where (p => p.JniType == jniType)
				.SelectMany (p => p.Handles.Select (h => p.GetStackTraceForHandle (h)))
				.Distinct ();
		}

		Dictionary<Regex, Action<Match, int>> CreateHandlers (int? pid)
		{
			string pidv   = pid.HasValue ? pid.ToString () : @"\d+";

			string addGm  = string.Format (AddGrefFormat, pidv);
			string addWm  = string.Format (AddWgrefFormat, pidv);
			string atm    = string.Format (AtFormat, pidv);
			string dm     = string.Format (DisposingFormat, pidv);
			string fm     = string.Format (FinalizingFormat, pidv);
			string hm     = string.Format (HandleFormat, pidv);
			string remGm  = string.Format (RemoveGrefFormat, pidv);
			string remWm  = string.Format (RemoveWgrefFormat, pidv);

			PeerInfo    curPeer = null;

			JniHandleInfo   curHandle = default (JniHandleInfo);

			return new Dictionary<Regex, Action<Match, int>> {
				{ new Regex (addGm), (m, l) => {
						var h = GetHandle (m, "ghandle");
						var p = GetLastOrCreatePeerInfo (m, h);
						p.AppendStackTraceForHandle (h, m.Groups ["stack"].Value);
						GlobalRefs.Add (h);
						CheckCounts (m, l);

						curPeer     = p;
						curHandle   = h;
				} },
				{ new Regex (addWm), (m, l) => {
						var h = GetHandle (m, "whandle");
						var p = GetLastOrCreatePeerInfo (m, h);
						p.AppendStackTraceForHandle (h, m.Groups ["stack"].Value);
						WeakRefs.Add (h);
						CheckCounts (m, l);

						curPeer     = p;
						curHandle   = h;
				} },
				{ new Regex (dm), (m, l) => {
						var p = GetAlivePeerInfo (m);
						if (p == null) {
							LogWarning (GrefParseOptions.CheckAlivePeers, $"at line {l}: could not find PeerInfo for disposed handle {GetHandle (m, "handle")}");
							return;
						}
						p.Disposed  = true;
				} },
				{ new Regex (fm), (m, l) => {
						var p = GetAlivePeerInfo (m);
						if (p == null) {
							LogWarning (GrefParseOptions.CheckAlivePeers, $"at line {l}: could not find PeerInfo for finalized handle {GetHandle (m, "handle")}");
							return;
						}
						p.Finalized = true;
				} },
				{ new Regex (hm), (m, l) => {
						var p = GetAlivePeerInfo (m);
						if (p == null) {
							LogWarning (GrefParseOptions.CheckAlivePeers, $"at line {l}: could not find PeerInfo for handle {GetHandle (m, "handle")}");
							return;
						}
						if (!string.IsNullOrEmpty (p.KeyHandle)) {
							LogWarning (GrefParseOptions.CheckAlivePeers, $"at line {l}: Attempting to re-set p.KeyHandle {p.KeyHandle} for `{p.JniType}` to {m.Groups ["key_handle"].Value} for {m.Groups ["jtype"].Value}");
							return;
						}
						p.KeyHandle = m.Groups ["key_handle"].Value;
						p.JniType   = m.Groups ["jtype"].Value;
						p.McwType   = m.Groups ["mtype"].Value;
				} },
				{ new Regex (remGm), (m, l) => {
						var h = GetHandle (m, "handle");
						var p = GetAlivePeerInfo (m);
						if (p == null) {
							LogWarning (GrefParseOptions.CheckAlivePeers, $"at line {l}: could not find PeerInfo to remove gref for handle {h}");
						}
						GlobalRefs.Remove (h);
						if (p != null) {
							p.Handles.Remove (h);
							p.RemovedHandles.Add (h);
						}
						if (p != null && !WeakRefs.Any (w => p.Handles.Contains (w))) {
							p.Collected = true;
							p.DestroyedOnThread = m.Groups ["thread"].Value;
							alive.Remove (p);
						}
						CheckCounts (m, l);

						curPeer     = null;
						curHandle   = default (JniHandleInfo);
				} },
				{ new Regex (remWm), (m, l) => {
						var h = GetHandle (m, "handle");
						var p = GetAlivePeerInfo (m);
						WeakRefs.Remove (h);
						if (p == null) {
							// Means that the instance has been collected; this is fine.
						} else {
							p.Handles.Remove (h);
							p.RemovedHandles.Add (h);
							if (!GlobalRefs.Any (g => p.Handles.Contains (g))) {
								p.Collected = true;
								p.DestroyedOnThread = m.Groups ["thread"].Value;
								alive.Remove (p);
							}
						}
						CheckCounts (m, l);

						curPeer     = null;
						curHandle   = default (JniHandleInfo);
				} },
				{ new Regex (atm), (m, l) => {
						if (curPeer == null || curHandle == null)
							return;
						curPeer.AppendStackTraceForHandle (curHandle, m.Groups ["stack"].Value);
				} },
			};
		}

		static JniHandleInfo GetHandle (Match m, string handleGroup)
		{
			var handle  = m.Groups [handleGroup].Value;
			var type    = m.Groups [handleGroup + "_type"].Value;
			var thread  = m.Groups ["thread"].Value;
			return new JniHandleInfo (
					handle,
					string.IsNullOrEmpty (type) || type.Length < 1? JniHandleType.None : JniHandleInfo.GetHandleType (type [0]),
					thread);
		}

		void CheckCounts (Match m, int lineNumber)
		{
			if ((ParseOptions & GrefParseOptions.CheckCounts) == 0)
				return;
			string message = null;
			int gc;
			if (int.TryParse (m.Groups ["gcount"].Value, out gc) && gc != GrefCount) {
				message = string.Format (" grefc mismatch at line {0}: expected {1,5}, actual {2,5}; line: {3}",
						lineNumber, GrefCount, gc, m.Groups[0]);
			}
			if (int.TryParse (m.Groups ["wgcount"].Value, out gc) && gc != WeakGrefCount) {
				message = string.Format ("wgrefc mismatch at line {0}; expected {1,5}, actual {2,5}; line: {3}",
						lineNumber, WeakGrefCount, gc, m.Groups[0]);
			}
			if (message != null) {
				LogWarning (GrefParseOptions.CheckCounts, message);
			}
		}

		void LogWarning (GrefParseOptions type, string message)
		{
			Console.WriteLine ("Warning: {0}", message);

			GrefParseOptions shouldThrow    = type | GrefParseOptions.ThrowExceptionOnMismatch;
			if ((ParseOptions & shouldThrow) == shouldThrow)
				throw new InvalidOperationException (message);
		}

		PeerInfo GetAlivePeerInfo (Match m)
		{
			var h = GetHandle (m, "handle");
			var i = alive.FindLastIndex (p => p.Handles.Contains(h));
			if (i >= 0)
				return alive [i];
			return null;
		}

		PeerInfo GetLastOrCreatePeerInfo (Match m, JniHandleInfo newHandle)
		{
			var h = GetHandle (m, "handle");
			var i = alive.FindLastIndex (p => p.Handles.Contains (h));
			if (i >= 0)
				return alive [i];
			var pid = m.Groups ["pid"].Value;
			var peer = new PeerInfo (pid) {
				CreatedOnThread = m.Groups ["thread"].Value,
				Handles = {
					newHandle,
				},
				// Not *technically* removed, but `h` can't appear in Handles either, because of possible handle "reuse"
				RemovedHandles = {
					h,
				},
			};
			allocated.Add (peer);
			alive.Add (peer);
			return peer;
		}
	}
}
