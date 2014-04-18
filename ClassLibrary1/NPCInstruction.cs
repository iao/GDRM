using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;

namespace GD
{
    class NPCEventTrigger{
        public DateTime TriggerTime { get; private set; }
        public NPCEventGroup EventGroup { get; private set; }
        
        public NPCEventTrigger(DateTime trigger_time, NPCEventGroup event_group)
        {
            this.TriggerTime = trigger_time;
            this.EventGroup = event_group;
        }
    }

	class NPCEventProximityTrigger{
		public int Radias { get; private set; }
		public NPCEventGroup EnterGroup { get; private set; }
		public NPCEventGroup ExitGroup { get; private set; }
		public string Node { get; private set; }
		
		public NPCEventProximityTrigger(int radias, NPCEventGroup enter_group, NPCEventGroup exit_group, string node)
		{
			this.Radias = radias;
			this.EnterGroup = enter_group;
			this.ExitGroup = exit_group;
			this.Node = node;
		}
	}

	class NPCEventProximityTriggerState
	{
		public NPCEventProximityTrigger ProximityTrigger { get; private set; }
		public Boolean Triggered { get; private set; }

		public NPCEventProximityTriggerState (NPCEventProximityTrigger proximityTriger)
		{
			this.ProximityTrigger = proximityTriger;
		}

		public NPCEventGroup GetCurrentEvent(NPCharacter npc, Scene scene)
		{
			string nodeName = ProximityTrigger.Node;
			Vector3 position;
			if(nodeName != null)
			{
				GraphNode node = NavManager.Instance.GetNodeByName (nodeName);
				position = node.Location.Vector;
			}
			else
			{
				position = npc.Location.Vector;
			}
			IPCManager pcManager = Managers.GetPCManager (scene);
			List<PCharacter> pcs = pcManager.WithinRange (position, ProximityTrigger.Radias);
			if(Triggered && pcs.Count == 0)
			{
				Triggered = false;
				return ProximityTrigger.ExitGroup;
			}
			else if(!Triggered && pcs.Count != 0)
			{
				Triggered = true;
				return ProximityTrigger.EnterGroup;
			}
			else
			{
				return null;
			}
		}
	}

    class NPCEventGroup
    {
        private List<NPCEvent> events = new List<NPCEvent>();

        public NPCEvent GetEvent()
        {
            List<NPCEvent> events_available = null;
            lock(events) {
                events_available = events.ToList(); 
            }
            NPCEvent current_event = null;
            Random r = new Random();
            while (current_event == null && events_available.Count > 0)
            {
                int index = r.Next(events_available.Count);
                if (events_available.ElementAt(index).Lock())
                {
                    current_event = events_available.ElementAt(index);
                }
                else
                {
                    events_available.RemoveAt(index);
                }
            }
            return current_event;
        }

        public void AddEvent(NPCEvent new_event)
        {
            lock (this.events)
            {
                this.events.Add(new_event);
            }
        }
    }

    class NPCEvent
    {
        public List<NPCInstruction> Instructions { get { lock(this._instructions) return this._instructions.ToList(); } }
        private List<NPCInstruction> _instructions = new List<NPCInstruction>();
        private bool is_locked = false;

        public void AddInstruction(NPCInstruction new_instruction){
            lock (this._instructions)
            {
                this._instructions.Add(new_instruction);
            }
        }

        public bool IsLocked()
        {
            lock (this)
            {
                return is_locked;
            }
        }

        public bool Lock()
        {
            lock (this)
            {
                if (is_locked)
                {
                    return false;
                }
                else
                {
                    this.is_locked = true;
                    return true;
                }
            }
        }

        public void Release()
        {
            lock (this)
            {
                is_locked = false;
            }
        }
    }

    abstract class NPCInstruction {}

    class NPCSayInstruction : NPCInstruction
    {
        public const string Identifier = "Say";

        public string Message { get; private set; }

        public string Sound { get; private set; }

        public NPCSayInstruction(string message, string sound)
        {
            this.Message = message;
            this.Sound = sound;
        }

        public NPCSayInstruction(string message) : this(message, null)
        {}
    }

    class NPCNavigationInstruction : NPCInstruction
    {
        public const string Identifier = "NavigateTo";

        public string EndPointName { get; private set; }

        public NPCNavigationInstruction(string end_point_name)
        {
            this.EndPointName = end_point_name;
        }
    }

    class NPCAnimationInstruction : NPCInstruction
    {
        public const string Identifier = "DoAnimation";

        public string AnimationName { get; private set; }
        public int AnimationLength { get; private set; }

        public NPCAnimationInstruction(string animation_name, int animation_length)
        {
            this.AnimationName = animation_name;
            this.AnimationLength = animation_length;
        }
    }

    class NPCAppearanceInstruction : NPCInstruction
    {
        public const string Identifier = "SetAppearance";

        public string AppearanceName { get; private set; }

        public NPCAppearanceInstruction(string appearance_name)
        {
            this.AppearanceName = appearance_name;
        }
    }

    class NPCLookAtInstruction : NPCInstruction
    {
        public const string Identifier = "LookAt";

        public string NodeName { get; private set; }

        public NPCLookAtInstruction(string node_name)
        {
            this.NodeName = node_name;
        }
    }

    class NPCWaitInstruction : NPCInstruction
    {
        public const string Identifier = "Wait";

        public int WaitMilliseconds { get; private set; }

        public NPCWaitInstruction(int wait_milliseconds)
        {
            this.WaitMilliseconds = wait_milliseconds;
        }
    }
    
    class NPCSpawnInstruction : NPCInstruction
    {
        public const string Identifier = "Spawn";

        public bool Spawn { get; private set; }

        public NPCSpawnInstruction(bool spawn)
        {
            this.Spawn = spawn;
        }
    }

	class NPCSetMedia : NPCInstruction
	{
		public const string Identifier = "SetMedia";
		
		public string URL { get; private set; }
		
		public NPCSetMedia(string url)
		{
			this.URL = url;
		}
	}

	class NPCClearMedia : NPCInstruction
	{
		public const string Identifier = "ClearMedia";
		
		public NPCClearMedia()
		{
		}
	}

	class NPCRepeat : NPCInstruction
	{
		public const string Identifier = "Repeat";
		
		public NPCRepeat()
		{
		}
	}

	class NPCDialog : NPCInstruction
	{
		public const string Identifier = "Dialog";

		public string Message { get; private set;}
		public string[] Options { get; private set; }
		public string Correct { get; private set; }

		public NPCDialog (string message, string options, string correct)
		{
			this.Message = message;
			this.Options = options.Split(',');
			this.Correct = correct;
		}
	}

	class NPCLock : NPCInstruction
	{
		public const string Identifier = "Lock";
		
		public string KeyName { get; private set;}
		public int Radius { get; private set; }
		
		public NPCLock (string keyName, int radius)
		{
			this.KeyName = keyName;
			this.Radius = radius;
		}
	}

	class NPCSit : NPCInstruction
	{
		public const string Identifier = "Sit";
		
		public UUID SitTarket { get; private set;}
		
		public NPCSit (UUID sitTarget)
		{
			this.SitTarket = sitTarget;
		}
	}

    class NPCConvoTrigger
    {
        public List<String> Triggers { get; private set; }
        public String Message;
        public String Sound;

        public NPCConvoTrigger(List<String> triggers, String message, String sound)
        {
            this.Triggers = triggers;
            this.Message = message;
            this.Sound = sound;
        }

        public NPCConvoTrigger(List<String> triggers, String message) : this(triggers, message, null)
        {
          
        }
    }
}
