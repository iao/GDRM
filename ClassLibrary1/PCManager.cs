using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using GD.Interfaces;

using Mono.Addins;

using Nini.Config;

namespace GD
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "PCManager")]
    class PCManager : INonSharedRegionModule, IPCManager
    { 
        public string Name { get { return "PCManager"; } }
        public Type ReplaceableInterface { get { return null; } }

        private Dictionary<UUID, PCharacter> playing_characters = new Dictionary<UUID, PCharacter>();

        //private Scene scene;
        public void AddRegion(Scene scene)
        {
            //this.scene = scene;
            scene.EventManager.OnNewClient += ConnectPC;
            scene.EventManager.OnClientClosed += ClosePC;
            Managers.SetPCManager(scene, this);
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnNewClient -= ConnectPC;
            scene.EventManager.OnClientClosed -= ClosePC;
            //this.scene = null;
        }

        public bool TryGet(UUID agent_id, out PCharacter character)
        {
            return this.playing_characters.TryGetValue(agent_id, out character);
        }

        private void ConnectPC(IClientAPI client)
        {
            if (NPCManager.Instance.IsNPC(client)) return;

            PCharacter new_character = new PCharacter(client);

            lock (playing_characters)
            {
                if (!this.playing_characters.ContainsKey(new_character.AgentId))
                {
                    this.playing_characters.Add(new_character.AgentId, new_character);
                }
            }
            GDRM.log.DebugFormat("[PCManager]: Client connect: {0}. Online characters: {1}", new_character.AgentId.ToString(), playing_characters.Count);
        }

        private void ClosePC(UUID agent_id, Scene scene)
        {
            PCharacter old_character;
            if (this.TryGet(agent_id, out old_character))
            {
                lock (playing_characters)
                {
                    this.playing_characters.Remove(old_character.AgentId);
                }
                old_character.Kill();
                GDRM.log.DebugFormat("[PCManager]: Client disconnect: {0}. Online characters: {1}", old_character.AgentId.ToString(), playing_characters.Count);
            }
        }

        public void SendMessageToAll(String message)
        {
            lock (this.playing_characters)
            {
                foreach (PCharacter player in this.playing_characters.Values)
                {
                    player.SendMessage(message);
                }
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public void Initialise(IConfigSource config)
        {
        }

		public List<PCharacter> WithinRange(Vector3 fromRegionPos, int radias)
		{
			List<PCharacter> pcs = new List<PCharacter> ();
			Vector3 toRegionPos;
			double dis;
			foreach (PCharacter pc in playing_characters.Values)
			{
				toRegionPos = pc.Presence.AbsolutePosition;
				dis = Math.Abs(Util.GetDistanceTo(toRegionPos, fromRegionPos));
				if(dis <= radias)
				{
					pcs.Add(pc);
				}
			}
			return pcs;
		}

		public PCharacter ClosestPC(Vector3 fromRegionPos)
		{
			PCharacter ret = null;
			double distance = Double.MaxValue;
			Vector3 toRegionPos;
			foreach (PCharacter pc in playing_characters.Values)
			{
				toRegionPos = pc.Presence.AbsolutePosition;
				double dis = Math.Abs(Util.GetDistanceTo(toRegionPos, fromRegionPos));
				if(dis < distance)
				{
					distance = dis;
					ret = pc;
				}
			}
			return ret;
		}
    }
}
