using System;
using System.Collections.Generic;
using GD.Time;

namespace GD.Interfaces
{
	interface ILightSourceManager
	{
		void Clear ();

		void RemoveLightSource (LightSource lightSource);

		void AddLightSource (LightSource lightSource);

		IEnumerable<LightSource> LightSources {
			get;
		}
	}
}

