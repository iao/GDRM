using System;
using GD.Time;

namespace GD.Interfaces
{
	public interface ITimeManager
	{
		DateTime Time {
			get;
			set;
		}

		event TimeUpdate OnTimeChange;
	}
}

