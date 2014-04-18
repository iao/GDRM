using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.OptionalModules.World.NPC;
using GD.Interfaces;
namespace GD
{
    class NPCManager
    {
        private static NPCManager _instance = new NPCManager();
        public static NPCManager Instance { get { return _instance; } }

        private HashSet<NPCharacter> non_playing_characters = new HashSet<NPCharacter>();
        public IEnumerable<NPCharacter> NPCs { get { return non_playing_characters; } }

        public void AddNPC(NPCharacter new_character)
        {
            lock (non_playing_characters)
            {
                this.non_playing_characters.Add(new_character);
            }
        }

        public void RemoveNPC(NPCharacter old_character)
        {
            //makes sure the NPC has been despawned before removing it.
            old_character.Kill();
            lock (non_playing_characters)
            {
                this.non_playing_characters.Remove(old_character);
            }
        }

        public void DespawnAll()
        {
            lock (non_playing_characters)
            {
                foreach (NPCharacter character in non_playing_characters)
                {
                    character.Spawn(false);
                }
            }
        }

        public void RespawnAll()
        {
            lock (non_playing_characters)
            {
                foreach (NPCharacter character in non_playing_characters)
                {
                    character.Spawn(true);
                }
            }
        }

        public void Clear()
        {
            lock (this.non_playing_characters)
            {
                foreach (NPCharacter npc in this.non_playing_characters.ToList())
                {
                    RemoveNPC(npc);
                }
            }
        }

        public void ProcessInstantMessage(IClientAPI client, UUID npc_id, string message)
        {
            NPCharacter npc = null;
            lock (this.non_playing_characters)
            {
                foreach (NPCharacter character in this.non_playing_characters)
                {
                    if (character.AgentId == npc_id)
                    {
                        npc = character;
                    }
                }
            }
            if (npc != null)
            {
                npc.InstantMessage(client, message);
            }
        }

        public bool TryGetByUUID(UUID uuid, out NPCharacter found_npc)
        {
            lock (this.non_playing_characters)
            {
                foreach (NPCharacter npc in this.non_playing_characters)
                {
                    if (npc.AgentId == uuid)
                    {
                        found_npc = npc;
                        return true;
                    }
                }
            }
            found_npc = null;
            return false;
        }

        public bool IsNPC(IClientAPI client_api)
        {
            return client_api is NPCAvatar;
        }
    }
}
