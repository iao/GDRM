using System;
using System.Collections.Generic;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace GD
{
	[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ForceSit")]
	public class ForceSit : INonSharedRegionModule
	{
		#region IRegionModuleBase implementation
		public void Initialise (Nini.Config.IConfigSource source)
		{
		}
		public void Close ()
		{
		}
		public void AddRegion (OpenSim.Region.Framework.Scenes.Scene scene)
		{
		}
		public void RemoveRegion (OpenSim.Region.Framework.Scenes.Scene scene)
		{
		}
		public void RegionLoaded (OpenSim.Region.Framework.Scenes.Scene scene)
		{
			scene.EventManager.OnChatFromWorld += OnChatFromWorld;
		}
		public string Name {
			get {
				return "ForceSit";
			}
		}
		public Type ReplaceableInterface {
			get {
				return null;
			}
		}
		#endregion

		private void OnChatToClient(UUID senderID, HashSet<UUID> receiverIDs, string message, ChatTypeEnum type, Vector3 fromPos, string fromName, ChatSourceType src, ChatAudibleLevel level)
		{
			GDRM.log.WarnFormat("[ForceSit]: {0}: {1}", fromName, message);
		}

		private void OnChatFromWorld(object sender, OSChatMessage chat)
		{
			GDRM.log.WarnFormat("[ForceSit]: {0}: {1}", chat.From, chat.Message);
			string uuid = chat.Message.Split (':')[1];
			GDRM.log.WarnFormat("[ForceSit]: sit {0}", uuid);
			PCharacter character;
			IPCManager pcManager = Managers.GetPCManager ((Scene)chat.Scene);
			UUID agentID;
			if(UUID.TryParse (uuid, out agentID) && pcManager.TryGet(agentID, out character))
			{
				Vector3 cameraEyeOffset = Vector3.Zero;
				Vector3 cameraAtOffset = Vector3.Zero;
				bool forceMouselook = false;
				
				SceneObjectPart part = character.Presence.FindNextAvailableSitTarget(targetID);
				if (part == null)
					return;
				
				// TODO: determine position to sit at based on scene geometry; don't trust offset from client
				// see http://wiki.secondlife.com/wiki/User:Andrew_Linden/Office_Hours/2007_11_06 for details on how LL does it
				
				if (PhysicsActor != null)
					m_sitAvatarHeight = PhysicsActor.Size.Z;
				
				bool canSit = false;
				Vector3 pos = part.AbsolutePosition + offset;
				
				if (part.IsSitTargetSet && part.SitTargetAvatar == UUID.Zero)
				{
					//                    m_log.DebugFormat(
					//                        "[SCENE PRESENCE]: Sitting {0} on {1} {2} because sit target is set and unoccupied",
					//                        Name, part.Name, part.LocalId);
					
					offset = part.SitTargetPosition;
					sitOrientation = part.SitTargetOrientation;
					canSit = true;
				}
				else
				{
					if (Util.GetDistanceTo(AbsolutePosition, pos) <= 10)
					{
						//                    m_log.DebugFormat(
						//                        "[SCENE PRESENCE]: Sitting {0} on {1} {2} because sit target is unset and within 10m",
						//                        Name, part.Name, part.LocalId);
						
						AbsolutePosition = pos + new Vector3(0.0f, 0.0f, m_sitAvatarHeight);
						canSit = true;
					}
					//                else
					//                {
					//                    m_log.DebugFormat(
					//                        "[SCENE PRESENCE]: Ignoring sit request of {0} on {1} {2} because sit target is unset and outside 10m",
					//                        Name, part.Name, part.LocalId);
					//                }
				}
				
				if (canSit)
				{
					if (PhysicsActor != null)
					{
						// We can remove the physicsActor until they stand up.
						RemoveFromPhysicalScene();
					}
					
					part.AddSittingAvatar(UUID);
					
					cameraAtOffset = part.GetCameraAtOffset();
					cameraEyeOffset = part.GetCameraEyeOffset();
					forceMouselook = part.GetForceMouselook();
					
					ControllingClient.SendSitResponse(
						part.UUID, offset, sitOrientation, false, cameraAtOffset, cameraEyeOffset, forceMouselook);
					
					m_requestedSitTargetUUID = part.UUID;
					
					HandleAgentSit(ControllingClient, UUID);
					
					// Moved here to avoid a race with default sit anim
					// The script event needs to be raised after the default sit anim is set.
					part.ParentGroup.TriggerScriptChangedEvent(Changed.LINK);
				}
			}
		}
	}
}

