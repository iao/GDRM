using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using GD.Time;
using GD.Interfaces;

using Mono.Addins;

using Nini.Config;
using GD.Command;

namespace GD.Interfaces
{
	interface ICommandManager
	{
		void RegisterCommand(String command, CommandCallback callback);
	}


    
}
