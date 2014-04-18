using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using GD.Command;
using GD.Time;
using OpenSim.Region.Framework.Interfaces;

namespace GD
{
	interface IPCManager
	{
		bool TryGet (UUID agent_id, out PCharacter character);

		void SendMessageToAll (string parameters);

		List<PCharacter> WithinRange(Vector3 fromRegionPos, int radias);
		PCharacter ClosestPC(Vector3 fromRegionPos);
	}
}

