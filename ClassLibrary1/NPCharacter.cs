using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Framework;
using System.Threading;
using OpenSim.Region.OptionalModules.World.NPC;
using OpenSim.Region.CoreModules.Avatar.Gestures;
using OpenSim.Region.Physics.Manager;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using System.Diagnostics;

namespace GD
{
	public class NPCharacter
	{
		public UUID AgentId { get; private set; }

		public Location Location { get { return (IsSpawned) ? new Location (this.Scene, this.Position) : null; } }

		public Location StartingLocation { get; set; }

		public String FirstName { get; set; }

		public String LastName { get; set; }

		public String Appearance { get; set; }

		public String Convo { get; set; }

		public bool IsSpawned { get; private set; }

		public Scene Scene = null;

		private Vector3 Position { get { return this.ScenePresence.AbsolutePosition; } }

		private ISoundModule SoundModule { get { return this.Scene.RequestModuleInterface<ISoundModule> (); } }

		private INPCModule NPCModule { get { return this.Scene.RequestModuleInterface<INPCModule> (); } }

		public ScenePresence ScenePresence { get { return (IsSpawned) ? this.Scene.GetScenePresence (this.AgentId) : null; } }

		public delegate void EndInstruction ();

		private GraphNode current_node;
		private int dialogChannel;
		private object navigation_lock = new object ();
		private object destination_lock = new object ();
		private object timer_lock = new object ();
		private bool walking = false;
		private HashSet<PCharacter> DialogClients = new HashSet<PCharacter> ();

		public NPCharacter (string first_name, string last_name, String appearance, String convo, Location starting_location)
		{
			this.FirstName = first_name;
			this.LastName = last_name;
			this.Appearance = appearance;
			this.Convo = convo;
			this.StartingLocation = starting_location;
			this.IsSpawned = false;

			AssetManager.Instance.TryGetConvo (this.Convo, out this.convo_triggers);
		}

        #region wait

		private Timer wait_timer = null;
		private object wait_lock = new object ();

		public void Wait (int milliseconds, EndInstruction end_instruction)
		{
			if (IsSpawned) {
				lock (wait_lock) {
					this.CancelWait ();
					wait_timer = new Timer (WaitFinish, end_instruction, milliseconds, System.Threading.Timeout.Infinite);
				}
			}
		}

		public void CancelWait ()
		{
			lock (wait_lock) {
				if (wait_timer != null) {
					wait_timer.Dispose ();
					wait_timer = null;
				}
			}
		}

		private void WaitFinish (object callback)
		{
			lock (wait_lock) {
				EndInstruction end_instruction = (EndInstruction)callback;
				if (end_instruction != null)
					end_instruction.Invoke ();
			}
		}

        #endregion

        #region spawn

		public void Spawn (bool set_spawn)
		{
			if (set_spawn && !this.IsSpawned) {
				this.Scene = this.StartingLocation.Scene;
				this.AgentId = NPCModule.CreateNPC (this.FirstName, this.LastName, StartingLocation.Vector /*new Vector3(40,40,50)*/, UUID.Zero, false, StartingLocation.Scene, new AvatarAppearance ());
				this.IsSpawned = true;
				this.dialogChannel = this.AgentId.GetHashCode ();
				this.SetAppearance (this.Appearance, null);
				NPCAvatar avatar = this.ScenePresence.ControllingClient as NPCAvatar;
				avatar.OnChatToNPC += OnChatToNPC;
			} else if (!set_spawn && this.IsSpawned) {
				this.CancelNavigation ();
				this.StopAnimate ();
				this.CancelWait ();
				this.NPCModule.DeleteNPC (this.AgentId, this.Scene);
				this.IsSpawned = false;
			}
		}

		private void OnChatToNPC (string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, UUID ownerID, byte source, byte audible)
		{
			PCharacter character;
			IPCManager pcManager = Managers.GetPCManager (this.Scene);
			if (type == 4 || type == 5 || !pcManager.TryGet (fromAgentID, out character))
				return;

			message = message.Replace (",", "").Replace (".", "").ToLower ();
			foreach (String word in message.Split(' ')) {
				lock (this.convo_triggers) {
					foreach (NPCConvoTrigger trigger in this.convo_triggers) {
						foreach (String trigger_text in trigger.Triggers) {
							if (message.Contains (trigger_text)) {
								//client.SendInstantMessage(new GridInstantMessage(this.Scene, this.AgentId, this.FirstName + " " + this.LastName, client.AgentId, (byte)InstantMessageDialog.MessageFromAgent, trigger.Message, false, client.StartPos));
								this.NPCModule.Say (this.AgentId, this.Scene, trigger.Message);
								if (trigger.Sound != null)
									this.SendSoundToClient (character.ClientApi, trigger.Sound);
								return;
							}
						}
					}
				}
			}
		}

		public virtual void Kill ()
		{
			this.Spawn (false);
		}

        #endregion

        #region speach

		private List<NPCConvoTrigger> convo_triggers;

		public void Say (string message, EndInstruction end_instruction)
		{
			if (IsSpawned) {
				this.NPCModule.Say (this.AgentId, this.Location.Scene, message);
				if (end_instruction != null)
					end_instruction.Invoke ();
			}
		}

		public void SendSoundToClient (IClientAPI client, String sound_string)
		{
			UUID sound_uuid;
			if (AssetManager.Instance.TryGetSound (sound_string, out sound_uuid)) {
				client.SendPlayAttachedSound (sound_uuid, client.AgentId, this.AgentId, 3.0f, 0);
			}
		}

		public void InstantMessage (IClientAPI client, String message)
		{
			message = message.Replace (",", "").Replace (".", "").ToLower ();
			foreach (String word in message.Split(' ')) {
				lock (this.convo_triggers) {
					foreach (NPCConvoTrigger trigger in this.convo_triggers) {
						foreach (String trigger_text in trigger.Triggers) {
							if (message.Contains (trigger_text)) {
								client.SendInstantMessage (new GridInstantMessage (this.Scene, this.AgentId, this.FirstName + " " + this.LastName, client.AgentId, (byte)InstantMessageDialog.MessageFromAgent, trigger.Message, false, client.StartPos));
								if (trigger.Sound != null)
									this.SendSoundToClient (client, trigger.Sound);
								return;
							}
						}
					}
				}
			}
		}

		//public void AddConvoTrigger(String trigger_text, NPCSayInstruction instruction)
		//{
		//    lock (this.convo_triggers)
		//    {
		//        if (trigger_text != null && instruction != null && this.convo_triggers.ContainsKey(trigger_text) == false)
		//        {
		//            this.convo_triggers.Add(trigger_text, instruction);
		//        }
		//    }
		//}

        #endregion

        #region appearance

		public void SetAppearance (string appearance_string, EndInstruction end_instruction)
		{
			if (this.IsSpawned) {
				this.Appearance = appearance_string;
				AvatarAppearance appearance;
				if (!AssetManager.Instance.TryGetAppearance (this.Appearance, this.GetHashCode (), out appearance)) {
					appearance = AssetManager.Instance.DefaultAppearance;
				}
				this.NPCModule.SetNPCAppearance (this.AgentId, appearance, this.Scene);
				this.ScenePresence.SetHeight(appearance.AvatarHeight);
				if (end_instruction != null)
					end_instruction.Invoke ();
			}
		}

        #endregion 

        #region movement

		public void LookAt (string node_name, EndInstruction end_instruction)
		{
			Location location = null;
			if (IsSpawned) {
				switch (node_name) {
					case "north":
						location = new Location (this.Scene, new Vector3 (0, float.MaxValue, 0));
						break;
					case "northeast":
						location = new Location (this.Scene, new Vector3 (float.MaxValue, float.MaxValue, 0));
						break;
					case "east":
						location = new Location (this.Scene, new Vector3 (float.MaxValue, 0, 0));
						break;
					case "southeast":
						location = new Location (this.Scene, new Vector3 (float.MaxValue, float.MinValue, 0));
						break;
					case "south":
						location = new Location (this.Scene, new Vector3 (0, float.MinValue, 0));
						break;
					case "southwest":
						location = new Location (this.Scene, new Vector3 (float.MinValue, float.MinValue, 0));
						break;
					case "west":
						location = new Location (this.Scene, new Vector3 (float.MinValue, 0, 0));
						break;
					case "northwest":
						location = new Location (this.Scene, new Vector3 (float.MinValue, float.MaxValue, 0));
						break;
					default:
						GraphNode node = NavManager.Instance.GetNodeByName (node_name);
						if (node != null)
							location = node.Location;
						break;
				}
				if (location != null)
					this.LookAt (location);
				else
					GDRM.log.WarnFormat ("[NPCharacter]: '{0}' could not look at node '{1}' as that node does not exist", this.FirstName + " " + this.LastName, node_name);
				if (end_instruction != null)
					end_instruction.Invoke ();
			}
		}

		private void WalkTo (Location location)
		{
			if (IsSpawned) {
				this.MoveTo (location, false, false);
			}
		}

		private void RunTo (Location location)
		{
			if (IsSpawned) {
				this.MoveTo (location, false, true);
			}
		}

		private void FlyTo (Location location)
		{
			if (IsSpawned) {
				this.MoveTo (location, true, false);
			}
		}

		private void TeleportTo (Location location)
		{
			if (this.IsSpawned) {
				lock (this.navigation_lock) {
					this.ScenePresence.Teleport (location.Vector);
					//this.Despawn();
					//this.Spawn(location);
				}
			}
		}

		private void MoveTo (Location location, bool fly, bool run)
		{
			if (IsSpawned) {
				if (location.Scene != this.Scene) {
					this.TeleportTo (location);
				} else {
					this.ScenePresence.MoveToTarget (location.Vector, !fly, !fly);
					this.ScenePresence.SetAlwaysRun = run;
				}
			}
		}

		//TODO: private
		public void LookAt (Location location)
		{
			if (this.IsSpawned) {
				Vector3 look_at = Vector3.Subtract (location.Vector, this.Position);
				float angle = (float)Math.Atan2 (look_at.Y, look_at.X);
				Quaternion rotation = Quaternion.CreateFromAxisAngle (new Vector3 (0, 0, 1), angle);
				//Quaternion reverse = Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), angle + (float)(Math.PI/2));


				this.ScenePresence.PhysicsActor.APIDDamping = 1;
				this.ScenePresence.PhysicsActor.APIDStrength = 0.5f;
				//GDRM.log.ErrorFormat("Position of " + this.FirstName + " is: {0},{1},{2}. Postion of eye is: {3},{4},{5}. Difference = {6},{7},{8}. Angle = {9}. New rotation = {10}. Current rotation = {11}", this.Position.X, this.Position.Y, this.Position.Z, location.X, location.Y, location.Z, look_at.X, look_at.Y, look_at.Z, angle * (180/Math.PI), rotation, this.ScenePresence.Rotation);
				//this.ScenePresence.Rotation = reverse;
				this.ScenePresence.Rotation = rotation; // Quaternion.Multiply(rotation, this.ScenePresence.Rotation);
			}
		}

        #endregion

        #region animation

		private UUID current_animation = UUID.Zero;
		private Timer animation_timer = null;
		private object animation_lock = new Object ();

		public void Animate (string animation_string, int seconds, EndInstruction end_instruction)
		{
			if (this.IsSpawned) {
				UUID animation;
				AssetManager.Instance.TryGetAnimation (animation_string, out animation);
				lock (this.animation_lock) {
					this.StopAnimate ();
					this.ScenePresence.Animator.AddAnimation (animation, animation);
					int time_period = (seconds == 0) ? System.Threading.Timeout.Infinite : seconds * 1000;
					this.animation_timer = new Timer (StopAnimateCallback, end_instruction, time_period, System.Threading.Timeout.Infinite);
					this.current_animation = animation;
				}
			}
		}

		private void StopAnimateCallback (object callback)
		{
			this.StopAnimate ();
			EndInstruction end_instruction = (EndInstruction)callback;
			if (end_instruction != null)
				end_instruction.Invoke ();
		}

		public void StopAnimate ()
		{
			if (this.IsSpawned) {
				lock (this.animation_lock) {
					if (animation_timer != null) {
						this.ScenePresence.Animator.RemoveAnimation (this.current_animation, false);
						this.current_animation = UUID.Zero;
						this.animation_timer.Dispose ();
						this.animation_timer = null;
					}
				}
			}
		}

        #endregion

        #region navigation

		private List<GraphNode> route = new List<GraphNode> ();
		private Timer navigation_task = null;
		private Location last_position = null;
		private int movement_stuck_count = 0;
		private int navigation_timer_period = 300;

		public bool NavigateTo (string node_name, EndInstruction end_instruction)
		{
			lock (this.destination_lock) {
				NavManager nav_man = NavManager.Instance;
				GraphNode end_point = nav_man.GetNodeByName (node_name);
				walking = true;
				if (end_point != null) {
					return this.NavigateTo (end_point, end_instruction);
				}
				return false;
			}
		}

		private bool NavigateTo (GraphNode target, EndInstruction end_instruction)
		{
			//Make sure any existing navigation is cancelled
			this.CancelNavigation ();

			NavManager nav_man = NavManager.Instance;
			GraphNode start_node = nav_man.GetNearestNode (this.Location);
			if (start_node != null) {
				lock (this.navigation_lock) {
					List<GraphNode> route = nav_man.GetRoute (start_node, target);
					if (route != null) {
						this.route = route;
						current_node = this.route.ElementAt (0);
						if (route.Count > 0)
							this.WalkTo (current_node.Location);
						lock (this.timer_lock) {
							if (this.navigation_task != null) {
								this.navigation_task.Dispose ();
							}
							string frames = "";
							StackTrace stackTrace = new StackTrace ();           // get call stack
							StackFrame[] stackFrames = stackTrace.GetFrames ();  // get method calls (frames)
							
							// write call stack method names
							foreach (StackFrame stackFrame in stackFrames) {
								frames += stackFrame.GetMethod ().Name + " ";   // write method name
							}
							GDRM.log.WarnFormat ("[NavManager]: creating timer. {0}", frames);
							this.navigation_task = new Timer (WalkStep, end_instruction, 0, this.navigation_timer_period);
						}
						return true;
					} else {
						GDRM.log.WarnFormat ("[NavManager]: Could not calculate route to node '{0}'.", target.Name); 
					}
				}
			} else {
				GDRM.log.WarnFormat ("[NavManager]: Could not find a node near to {0} {1} to start navigation", this.FirstName, this.LastName);
			}
			return false;
		}

		public void CancelNavigation ()
		{
			lock (this.navigation_lock) {
				lock (this.timer_lock) {
					if (this.navigation_task != null) {
						this.navigation_task.Dispose ();
						this.navigation_task = null;
					}
				}
				this.movement_stuck_count = 0;
				this.last_position = null;
				this.route = new List<GraphNode> ();
			}
		}

		public void CheckLocation ()
		{
			lock (this.destination_lock) {
				if (!this.IsSpawned || this.route == null || this.current_node == null || this.route.Count != 0)
					return;
				if (walking) {
					if (this.Location != null && current_node.Location != null && Location.FlatDistanceBetween (this.Location, current_node.Location) < 1f) {
						Quaternion rotation = this.ScenePresence.Rotation;
						this.WalkTo (this.Location); //stops the avatar from continuing to try and walk to unreachable areas without triggering a gliding avatar bug.
						this.walking = false;
						GDRM.log.WarnFormat ("[NavManager]: Walking ended no route.");
						this.ScenePresence.Rotation = rotation; //the above command resets rotation, which 9/10 times is not beneficial.
					}
				} else {
					if (Location.FlatDistanceBetween (current_node.Location, this.Location) > 1.5f) {
						GDRM.log.WarnFormat ("[NavManager]: Navigating to node '{0}'.", current_node.Location.Vector);
						NavigateTo (current_node, null);
					}
				}
			}
		}

		private void WalkStep (object param)
		{
			lock (this.destination_lock) {
				EndInstruction end_instruction = (EndInstruction)param;
				lock (this.navigation_lock) {
					if (!this.IsSpawned || this.route == null || this.route.Count == 0) {
						this.CancelNavigation ();
						Timer t = (Timer)param;
						t.Dispose ();
						GDRM.log.WarnFormat ("[NavManager]: No route or not spawned1.");
						if (end_instruction != null)
							end_instruction.Invoke ();
						return;
					}

					Location target_position = route.ElementAt (0).Location;
					Location current_position = this.Location;



					if (Location.FlatDistanceBetween (current_position, target_position) < 1f) {
						this.route.RemoveAt (0);
						if (this.route.Count > 0) {
							current_node = this.route.ElementAt (0);
							this.WalkTo (current_node.Location);
						} else {
							//this.ScenePresence.ResetMoveToTarget();
							this.CancelNavigation ();
							Quaternion rotation = this.ScenePresence.Rotation;
							this.WalkTo (this.Location); //stops the avatar from continuing to try and walk to unreachable areas without triggering a gliding avatar bug.
							this.walking = false;
							this.ScenePresence.Rotation = rotation; //the above command resets rotation, which 9/10 times is not beneficial.
							if (end_instruction != null)
								end_instruction.Invoke ();
						}
						return;
					}


					//Makes sure the avatar isnt stuck.
					if (last_position != null && Location.FlatDistanceBetween (last_position, current_position) < 0.1f) {
						this.WalkTo (target_position);
						movement_stuck_count++;

						//if the avatar has been stuck for a while, teleport them to end of the route
						if (movement_stuck_count == 4) {
							movement_stuck_count = 0;
							GDRM.log.ErrorFormat ("[NavManager]: Character {0} could not navigate from {1} to {2}. Teleporting to end of route.", this.FirstName + " " + this.LastName, current_position.Vector, target_position.Vector);
							this.current_node = this.route.Last ();
							this.TeleportTo (current_node.Location);
							this.CancelNavigation ();
							if (end_instruction != null)
								end_instruction.Invoke ();
						}
					} else {
						//if the avatar isn't stuck, the count stuck is reset.
						movement_stuck_count = 0;
					}
					this.last_position = current_position;
				}
			}
		}

        #endregion
        
		#region Media

		public void SetMedia (string url)
		{
			if (this.IsSpawned && this.ScenePresence.HasAttachments()) {
				List<SceneObjectGroup> attachments = this.ScenePresence.GetAttachments ();
				SceneObjectPart part = attachments [0].RootPart;
				IMoapModule module = Scene.RequestModuleInterface<IMoapModule> ();
				if (null == module)
					return;
				MediaEntry me = module.GetMediaEntry (part, 0);
				if (null == me)
					me = new MediaEntry ();
				me.CurrentURL = url;
				me.AutoPlay = true;
				module.SetMediaEntry (part, 0, me);
			}
		}

		public void ClearMedia ()
		{
			if (this.IsSpawned && this.ScenePresence.HasAttachments()) {
				List<SceneObjectGroup> attachments = this.ScenePresence.GetAttachments ();
				SceneObjectPart part = attachments [0].RootPart;
				IMoapModule module = Scene.RequestModuleInterface<IMoapModule> ();
				if (null == module)
					return;
				module.ClearMediaEntry (part, 0);
			}
		}

		#endregion

		#region Dialog

		public void Dialog (string message, string[] options, string correct, EndInstruction endInstruction)
		{
			//IDialogModule dm = Scene.RequestModuleInterface<IDialogModule>();
			//if (dm != null) {
			IPCManager pcManager = Managers.GetPCManager (this.Scene);
			PCharacter pc = pcManager.ClosestPC (Location.Vector);
			if (pc != null) {
				pc.AddChatListener (this, HandleOnChatFromClient, new DialogParms (endInstruction, correct));
				DialogClients.Add (pc);
				//dm.SendDialogToUser(
				//	pc.AgentId, "", new UUID(), this.AgentId,
				//	message, new UUID("00000000-0000-2222-3333-100000001000"), dialogChannel, options);

				ScenePresence sp = this.Scene.GetScenePresence (pc.AgentId);
				if (sp != null) {
					sp.ControllingClient.SendDialog ("", new UUID (), this.AgentId,
					                                this.FirstName, this.LastName, message, new UUID ("00000000-0000-2222-3333-100000001000"), dialogChannel,
					                                options);
				}
			}
			/*}
			else
			{
				GDRM.log.WarnFormat("[NPCharacter]: dialog module not avalible.");
			}*/
		}

		void HandleOnChatFromClient (object sender, OSChatMessage c)
		{
			if (c.Channel == dialogChannel) {
				IPCManager pcManager = Managers.GetPCManager (this.Scene);
				PCharacter pc;
				if (!pcManager.TryGet (c.Sender.AgentId, out pc))
					return;
				DialogParms param = pc.RemoveChatListener (this, HandleOnChatFromClient);
				DialogClients.Remove (pc);
				if (param == null)
					return;
				if (param.CorrectResponse == c.Message) {
					this.NPCModule.Say (this.AgentId, this.Scene, "Correct");
					if (param.Insturction != null)
						param.Insturction.Invoke ();
				} else {
					this.NPCModule.Say (this.AgentId, this.Scene, "Wrong");
				}
			}
		}

		public void CancelDialog ()
		{
			foreach (PCharacter pc in DialogClients) {
				pc.RemoveChatListener (this, HandleOnChatFromClient);
			}
			DialogClients.Clear ();
		}

		public class DialogParms
		{
			public EndInstruction Insturction { get; private set; }

			public string CorrectResponse { get; private set; }

			public DialogParms (EndInstruction endInstruction, string correctResponse)
			{
				this.Insturction = endInstruction;
				this.CorrectResponse = correctResponse;
			}
		}

		#endregion

		#region Lock

		public void Lock (int radius, string name, EndInstruction end_instruction)
		{
			if (IsSpawned) {
				IPCManager pcManager = Managers.GetPCManager (this.Scene);
				Vector3 position = this.Location.Vector;
				List<PCharacter> pcs = pcManager.WithinRange (position, radius);
				bool foundKey = false;
				foreach (PCharacter pc in pcs) {
					if (pc.Presence.HasAttachments ()) {
						List<SceneObjectGroup> attachments = pc.Presence.GetAttachments ();
						foreach (SceneObjectGroup prim in attachments) {
							if (prim.Name == name) {
								foundKey = true;
								goto LoopEnd;
							}
						}
					}
				}
				LoopEnd:
				if (foundKey && end_instruction != null)
					end_instruction.Invoke ();

			}
		}

		#endregion

		#region Sit

		public void Sit (UUID sitTarget, EndInstruction end_instruction)
		{
			if (IsSpawned) {
				ScenePresence presence = this.ScenePresence;
				presence.HandleAgentRequestSit(null, this.AgentId, sitTarget, Vector3.Zero);
				if (end_instruction != null)
					end_instruction.Invoke ();
			}
		}

		#endregion
	}
}
