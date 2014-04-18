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
namespace GD.Command
{
    delegate void CommandCallback(PCharacter player, String parameters);

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CommandManager")]
    class CommandManager : INonSharedRegionModule, ICommandManager
    {
        private static char command_prefix = '/';
        //private static CommandManager _instance = new CommandManager();
        //public static CommandManager Instance { get { return _instance; } }

        public string Name { get { return "CommandManager"; } }
        public Type ReplaceableInterface { get { return null; } }

        

        private Dictionary<String, Delegate> command_callbacks = new Dictionary<String, Delegate>();

        public CommandManager()
        {
            
            //this.RegisterCommand("edit", this.CommandEdit);
        }

        private Scene scene;
        public void AddRegion(Scene scene)
        {
            this.scene = scene;
            scene.EventManager.OnChatFromClient += RecieveOSChatMessage;
            Managers.SetCommandManager(scene, this);
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnChatFromClient -= RecieveOSChatMessage;
            this.scene = null;
        }

        public void RegisterCommand(String command, CommandCallback callback)
        {
            lock (this.command_callbacks)
            {
                //if the there doesn't exist an event for this command, create it
                if (!this.command_callbacks.ContainsKey(command))
                {
                    this.command_callbacks.Add(command, null);
                }

                //add the callback to the command's event.
                this.command_callbacks[command] = ((CommandCallback)this.command_callbacks[command]) + callback;
            }
        }

        public void DeregisterCommand(String command, CommandCallback callback)
        {
            lock (this.command_callbacks)
            {
                if (this.command_callbacks.ContainsKey(command))
                {
                    //removes the callback from that command event
                    this.command_callbacks[command] = ((CommandCallback)this.command_callbacks[command]) - callback;
                    if (this.command_callbacks[command] == null)
                    {
                        this.command_callbacks.Remove(command);
                    }
                }
            }
        }

        private void RecieveMessage(String message, PCharacter player)
        {
            //check to see if the message is a command
            if (!this.IsCommand(message)) return;

            //isolates the first work and removes the command character
            String command = message.Split(' ')[0].Substring(1);
            lock (this.command_callbacks)
            {
                Delegate command_delegate;
                if (this.command_callbacks.TryGetValue(command, out command_delegate))
                {
                    CommandCallback callback = (CommandCallback)command_delegate;
                    if (callback != null)
                    {
                        //isolate any text after the command (if there is any)
                        String parameters = (message.Length > command.Length + 1) ? message.Substring(command.Length + 2) : "";
                        
                        //trigger the (all the) registered functions associated with the command
                        callback(player, parameters);
                    }
                }
                else
                {
                    player.SendMessage("Command not recognized.");
                }
            }
        }


        private void RecieveOSChatMessage(Object sender, OSChatMessage message)
        {
            PCharacter player;
            IPCManager pc_manager = Managers.GetPCManager(this.scene);
            if (pc_manager.TryGet(message.Sender.AgentId, out player))
            {
                this.RecieveMessage(message.Message, player);
            }

        }

        private bool IsCommand(String message)
        {
            char[] message_chars = message.ToCharArray();
            if (message_chars.Length == 0) return false;
            if (message_chars[0] != command_prefix) return false;
            return true;
        }

        private void CommandBroadcast(PCharacter character, string parameters)
        {
            if (!character.IsAdmin()) return;
            if (parameters.Count() > 0)
            {
                IPCManager pc_manager = Managers.GetPCManager(this.scene);
                pc_manager.SendMessageToAll(parameters);
                character.SendMessage("Broadcast sent.");
            }
            else
            {
                character.SendMessage("Usage: " + command_prefix + "broadcast <a message>");
            }
        }

        private void CommandReload(PCharacter character, string parameters)
        {
            IGDRM gdrm = Managers.GetGDRM(this.scene);
            gdrm.Reload();
        }

        public void RegionLoaded(Scene scene)
        {
            this.RegisterCommand("broadcast", this.CommandBroadcast);
            this.RegisterCommand("reload", this.CommandReload);
        }

        public void Close()
        {
        }

        public void Initialise(IConfigSource config)
        {
        }
    }
}
