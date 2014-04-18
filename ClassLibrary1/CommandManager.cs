using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;

namespace GD
{
    class CommandManager
    {
        private static char command_prefix = '/';
        private static CommandManager _instance = new CommandManager();
        public static CommandManager Instance { get { return _instance; } }
     
        public void AddRegion(Scene scene)
        {
            scene.EventManager.OnChatFromClient += RecieveChat;
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnChatFromClient -= RecieveChat;
        }

        private void RecieveChat(Object sender, OSChatMessage message)
        {
            if (IsCommand(message) && IsAdminMessage(message))
            {
                GDRM.log.InfoFormat("[CommandManager]: Handling command: '{0}' by '{1}'", message.Message, message.Sender.Name);
                HandleCommand(message);
            }
        }

        private bool IsCommand(OSChatMessage message)
        {
            char[] message_chars = message.Message.ToCharArray();
            if (message_chars.Length == 0) return false;
            if (message_chars[0] != command_prefix) return false;
            return true;
        }

        private bool IsAdminMessage(OSChatMessage message)
        {
            Scene message_scene = (Scene) message.Scene;
            UUID message_sender = message.SenderUUID;
            IEstateModule estate_module = message_scene.RequestModuleInterface<IEstateModule>();
            return estate_module.IsManager(message_sender);
        }

        private void HandleCommand(OSChatMessage message)
        {
            string command = message.Message.Split(' ')[0].Substring(1);
            string parameters = (message.Message.Length > command.Length + 1) ? message.Message.Substring(command.Length + 2) : "";
            Scene scene = (Scene) message.Scene;
            IClientAPI sender = message.Sender;
            IDialogModule dialog_module = scene.RequestModuleInterface<IDialogModule>();
            Action<IDialogModule, IClientAPI, Scene, string> command_handler;
            switch (command)
            {
                case "broadcast":
                    command_handler = CommandBroadcast;
                    break;
                case "reload":
                    command_handler = CommandReload;
                    break;
                case "npc":
                    command_handler = CommandNPC;
                    break;
                case "edit":
                    command_handler = CommandEdit;
                    break;
                case "time":
                    command_handler = CommandTime;
                    break;
                default:
                    command_handler = CommandDefault;
                    break;
            }
            command_handler(dialog_module, sender, scene, parameters);
        }

        private void CommandBroadcast(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            if (parameters.Length > 0)
            {
                GraphNode node = NavManager.Instance.GetNodeByName("praying1");
                foreach (NPCharacter npc in NPCManager.Instance.NPCs)
                {
                    string[] coords_string = parameters.Split(',');
                    float x = float.Parse(coords_string[0]);
                    float y = float.Parse(coords_string[1]);
                    float z = float.Parse(coords_string[2]);
                    npc.LookAt(new Location(node.Location.Scene, new Vector3(x,y,z)));

                }
                dialog_module.SendGeneralAlert(parameters);
                dialog_module.SendAlertToUser(sender, "Broadcast sent.");
            }
            else
            {
                dialog_module.SendAlertToUser(sender, "Usage: " + command_prefix + "broadcast <a message>");
            }
        }

        private void CommandReload(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            GDRM.Instance.Reload();
        }

        private void CommandNPC(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            if (parameters.Equals("on") || parameters.Equals("off"))
            {
                if (parameters.Equals("off"))
                {
                    NPCManager.Instance.DespawnAll();
                    dialog_module.SendAlertToUser(sender, "NPCs have been turned OFF.");
                }
                else if (parameters.Equals("on"))
                {
                    NPCManager.Instance.RespawnAll();
                    dialog_module.SendAlertToUser(sender, "NPCs have been turned ON.");
                }
            }
            else
            {
                dialog_module.SendAlertToUser(sender, "Command usage: " + command_prefix + "npc [on/off]");
            }
        }

        private void CommandEdit(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            //if the command has been validly used
            if (parameters.Equals("on") || parameters.Equals("off"))
            {
                bool turn_on = (parameters == "on");
                InteractionManager interaction_manager = InteractionManager.Instance;

                //if the command is actually trying to *toggle* the interaction state
                if (turn_on != interaction_manager.Interactable)
                {
                    if (turn_on)
                    {
                        NPCManager.Instance.DespawnAll();
                        InteractionManager.Instance.StartInteraction(sender.AgentId);
                    }
                    else
                    {
                        InteractionManager.Instance.StopInteraction();
                        GDRM.Instance.Save();
                        GDRM.Instance.Reload();
                    }
                    dialog_module.SendAlertToUser(sender, "Editting has been turned " + parameters);
                }
                else
                {
                    dialog_module.SendAlertToUser(sender, "Editting is already " + parameters);
                }
            }
            else
            {
                dialog_module.SendAlertToUser(sender, "Command usage: " + command_prefix + "edit [on/off]");
            }
        }

        private void CommandDefault(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            dialog_module.SendAlertToUser(sender, "Command not recognized");
        }

        private void CommandTime(IDialogModule dialog_module, IClientAPI sender, Scene scene, string parameters)
        {
            if (parameters == null || parameters.Equals(""))
            {
                DateTime now = GDRM.GetTime();
                dialog_module.SendAlertToUser(sender, "The time is: " + now.ToShortTimeString());
            }
            else
            {
                try
                {
                    GDRM.SetTime(GDRM.GetDateTimeByString(parameters));
                }
                catch (Exception e)
                {
                    dialog_module.SendAlertToUser(sender, e.Message);
                }
            }
        }
    }
}
