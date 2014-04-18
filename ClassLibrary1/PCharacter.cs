using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework.Client;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace GD
{
    class PCharacter
    {
        public UUID AgentId { get; private set; }

        public IClientAPI ClientApi { get; private set; }

		public IScenePresence Presence { get; private set; }

		private Dictionary<NPCharacter, NPCharacter.DialogParms> listeningClient = new Dictionary<NPCharacter, NPCharacter.DialogParms>();

        public PCharacter(IClientAPI client_api)
        {
            this.AgentId = client_api.AgentId;
            this.ClientApi = client_api;
            client_api.OnInstantMessage += InstantMessageHook;
			object presence;
			this.ClientApi.Scene.TryGetScenePresence (ClientApi.AgentId, out presence);
			this.Presence = (IScenePresence)presence;
        }

        public bool IsAdmin()
        {
            IEstateModule estate_module = this.ClientApi.Scene.RequestModuleInterface<IEstateModule>();
            return estate_module.IsManager(this.AgentId);
        }

        public void SendMessage(String message)
        {
            IDialogModule dialog_module = this.ClientApi.Scene.RequestModuleInterface<IDialogModule>();
            dialog_module.SendAlertToUser(ClientApi, message);
        }

        public void Kill()
        {
            ClientApi.OnInstantMessage -= InstantMessageHook;
        }

        private void InstantMessageHook(IClientAPI client, GridInstantMessage instant_message)
        {
            byte dialog = instant_message.dialog;
            if (!(dialog == (byte)InstantMessageDialog.MessageFromAgent || dialog == (byte)InstantMessageDialog.SessionSend))
            {
                return;
            }
            NPCManager.Instance.ProcessInstantMessage(client, new UUID(instant_message.toAgentID), instant_message.message);
        }

		public void AddChatListener(NPCharacter npc, ChatMessage callback, NPCharacter.DialogParms param)
		{
			if(!listeningClient.ContainsKey(npc))
			{
				listeningClient.Add(npc, param);
				ClientApi.OnChatFromClient += callback;
				GDRM.log.WarnFormat("[PCharacter]: Adding chat listener on {0} for {1} count {2}", AgentId, npc.AgentId, listeningClient.Count);
			}
			else
			{
				listeningClient[npc] = param;
				GDRM.log.WarnFormat("[PCharacter]: Replacing chat listener on {0} for {1} count {2}", AgentId, npc.AgentId, listeningClient.Count);
			}
		}

		public NPCharacter.DialogParms RemoveChatListener(NPCharacter npc, ChatMessage callback)
		{
			NPCharacter.DialogParms instruction = null;
			if(listeningClient.ContainsKey(npc))
			{
				instruction = listeningClient[npc];
				listeningClient.Remove(npc);
				ClientApi.OnChatFromClient -= callback;
				GDRM.log.WarnFormat("[PCharacter]: Removing chat listener on {0} for {1} count {2}", AgentId, npc.AgentId, listeningClient.Count);
			}
			return instruction;
		}
    }
}
