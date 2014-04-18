using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using GD.Interfaces;
namespace GD
{
    class Managers
    {
        private static Dictionary<Scene, IPCManager> pc_managers = new Dictionary<Scene, IPCManager>();
        public static void SetPCManager(Scene scene, IPCManager pc_manager)
        {
            pc_managers.Add(scene, pc_manager);
        }

        public static IPCManager GetPCManager(Scene scene)
        {
            if (pc_managers.ContainsKey(scene))
            {
                return pc_managers[scene];
            }
            else
            {
                return null;
            }
        }

        private static Dictionary<Scene, ICommandManager> command_managers = new Dictionary<Scene, ICommandManager>();
        public static void SetCommandManager(Scene scene, ICommandManager command_manager)
        {
            command_managers.Add(scene, command_manager);
        }

        public static ICommandManager GetCommandManager(Scene scene)
        {
            if (command_managers.ContainsKey(scene))
            {
                return command_managers[scene];
            }
            else
            {
                return null;
            }
        }

        private static Dictionary<Scene, IInteractionManager> interaction_managers = new Dictionary<Scene, IInteractionManager>();
        public static void SetInteractionManager(Scene scene, IInteractionManager interaction_manager)
        {
            interaction_managers.Add(scene, interaction_manager);
        }

        public static IInteractionManager GetInteractionManager(Scene scene)
        {
            if (interaction_managers.ContainsKey(scene))
            {
                return interaction_managers[scene];
            }
            else
            {
                return null;
            }
        }

        private static Dictionary<Scene, ITimeManager> time_managers = new Dictionary<Scene, ITimeManager>();
        public static void SetTimeManager(Scene scene, ITimeManager time_manager)
        {
            time_managers.Add(scene, time_manager);
        }

        public static ITimeManager GetTimeManager(Scene scene)
        {
            if (time_managers.ContainsKey(scene))
            {
                return time_managers[scene];
            }
            else
            {
                return null;
            }
        }

        private static Dictionary<Scene, ILightSourceManager> lightsource_managers = new Dictionary<Scene, ILightSourceManager>();
        public static void SetLightSourceManager(Scene scene, ILightSourceManager lightsource_manager)
        {
            lightsource_managers.Add(scene, lightsource_manager);
        }

        public static ILightSourceManager GetLightSourceManager(Scene scene)
        {
            if (lightsource_managers.ContainsKey(scene))
            {
                return lightsource_managers[scene];
            }
            else
            {
                return null;
            }
        }

        private static Dictionary<Scene, IGDRM> gdrms = new Dictionary<Scene, IGDRM>();
        public static void SetGDRM(Scene scene, IGDRM gdrm)
        {
            gdrms.Add(scene, gdrm);
        }

        public static IGDRM GetGDRM(Scene scene)
        {
            if (gdrms.ContainsKey(scene))
            {
                return gdrms[scene];
            }
            else
            {
                return null;
            }
        }
    }
}
