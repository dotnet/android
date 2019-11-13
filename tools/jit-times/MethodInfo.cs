using System.Collections.Generic;

namespace jittimes {
	public class MethodInfo {
		public enum State {
			None,
			Begin,
			Done,
		};

		public string method;

		public State state;

		public Timestamp begin, done;
		public Timestamp total, self;

		public List<MethodInfo> inner;

		public void AddInner (MethodInfo info)
		{
			if (inner == null)
				inner = new List<MethodInfo> ();

			inner.Add (info);
		}

		public bool CalcSelfTime ()
		{
			self = total;

			if (inner == null)
				return false;

			foreach (var im in inner)
				self -= im.total;

			return true;
		}
	}
}
