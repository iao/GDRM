using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using GD.Time;
using GD.Interfaces;
using OpenSim.Region.ScriptEngine.Shared.Api.Interfaces;
using System.Diagnostics;

namespace GD
{
    class PyNPCharacter : NPCharacter
    {
        public String ScriptName { get; set; }

        private List<NPCEventTrigger> event_triggers = new List<NPCEventTrigger>();
		private List<NPCEventProximityTriggerState> event_proximity_triggers = new List<NPCEventProximityTriggerState>();
        private NPCEventGroup current_event_group = null;
        private NPCEvent current_event = null;
		private Boolean isProximityTriggered = false;
		private int blocking_triggers = 0;

        //private Timer event_timer;

        public PyNPCharacter(string first_name, string last_name, String appearance, String convo, Location starting_location, String script_name)
            : base(first_name, last_name, appearance, convo, starting_location)
        {
            this.SetScript(script_name);
        }

        private void SetScript(string script_name)
        {
            this.ScriptName = script_name;
            this.ClearEvents();
            List<NPCEventTrigger> new_events;
			List<NPCEventProximityTrigger> new_proximity_events;
			List<NPCEventProximityTriggerState> new_proximity_events_state = new List<NPCEventProximityTriggerState>();
            if (AssetManager.Instance.TryGetScript(this.ScriptName, out new_events, out new_proximity_events))
            {
                this.event_triggers = new_events;
                ITimeManager time_manager = Managers.GetTimeManager(this.StartingLocation.Scene);
                time_manager.OnTimeChange += CheckForNewEvent;
				foreach (NPCEventProximityTrigger pevent in new_proximity_events)
				{
					new_proximity_events_state.Add(new NPCEventProximityTriggerState(pevent));
				}
				this.event_proximity_triggers = new_proximity_events_state;
            }
        }
        
        private void CheckForNewEvent(DateTime new_time)
        {
			CheckLocation ();
			NPCEventProximityTriggerState event_proximity_trigger;
			NPCEventGroup new_event_groupP = FindProximityEvent (out event_proximity_trigger);
			if(new_event_groupP != null)
			{
				if (event_proximity_trigger.Triggered) blocking_triggers++;
				else blocking_triggers--;

				if (this.current_event != null) this.current_event.Release();
				
				this.ClearInstructions();
				this.current_event_group = new_event_groupP;
				if (this.current_event_group == null) return;
				this.current_event = new_event_groupP.GetEvent();
				if (this.current_event != null)
				{
					foreach (NPCInstruction instruction in this.current_event.Instructions)
					{
						this.QueueInstruction(instruction);
					}
					this.StartHandlingQueue();
				}
				else
				{
					this.current_event_group = null;
				}
				isProximityTriggered = true;
				return;
			}
			if(isProximityTriggered)
			{

				if(this.instructions.Count != 0 || blocking_triggers != 0) return;
				else 
				{
					isProximityTriggered = false;
					this.current_event_group = null;
				}
			}

            NPCEventGroup new_event_group = CalculateCurrentEventGroup(new_time);
            if (new_event_group != this.current_event_group)
            {
                if (this.current_event != null) this.current_event.Release();

                this.ClearInstructions();
                this.current_event_group = new_event_group;
                if (this.current_event_group == null) return;
                this.current_event = new_event_group.GetEvent();
                if (this.current_event != null)
                {
                    foreach (NPCInstruction instruction in this.current_event.Instructions)
                    {
                        this.QueueInstruction(instruction);
                    }
                    this.StartHandlingQueue();
                }
                else
                {
                    this.current_event_group = null;
                }
            }
        }

        
        private NPCEventGroup CalculateCurrentEventGroup(DateTime now)
        {
            NPCEventTrigger best_match_event_trigger = null;
            int best_minutes_between = int.MaxValue;
            lock (this.event_triggers)
            {
                foreach (NPCEventTrigger npc_event_trigger in this.event_triggers)
                {
                    int minutes_between = MinutesBetween(npc_event_trigger.TriggerTime, now);
                    if (minutes_between >= 0 && minutes_between < best_minutes_between)
                    {
                        best_match_event_trigger = npc_event_trigger;
                        best_minutes_between = minutes_between;
                    }
                }
            }
            return (best_match_event_trigger != null) ? best_match_event_trigger.EventGroup : null;
        }

		private NPCEventGroup FindProximityEvent(out NPCEventProximityTriggerState event_proximity_trigger)
		{
			NPCEventGroup first_event = null;
			event_proximity_trigger = null;
			lock(this.event_proximity_triggers)
			{
				foreach (NPCEventProximityTriggerState npc_event_proximity_trigger in this.event_proximity_triggers)
				{
					first_event = npc_event_proximity_trigger.GetCurrentEvent(this, Scene);
					if(first_event != null)
					{
						event_proximity_trigger = npc_event_proximity_trigger;
						break;
					}
				}
			}
			return first_event;
		}

        private int MinutesBetween(DateTime a, DateTime b)
        {
            int time_a = a.Hour * 60 + a.Minute;
            int time_b = b.Hour * 60 + b.Minute;
            int difference = time_b - time_a;
            return (difference < 0) ? difference + 60 * 24 : difference;
        }

        
        public override void Kill()
        {
            this.ClearEvents();
            base.Kill();
        }
        
        private void ClearEvents()
        {
            lock (this.event_triggers)
            {
                this.ClearInstructions();
                //if (this.event_timer != null) this.event_timer.Dispose();
                if (this.current_event != null) this.current_event.Release();
                this.current_event = null;
                //this.event_timer = null;
                this.current_event_group = null;
                this.event_triggers.Clear();
				this.event_proximity_triggers.Clear();
            }
        }
        
        private void AddEventTrigger(NPCEventTrigger new_event_trigger)
        {
            lock (this.event_triggers)
            {
                this.event_triggers.Add(new_event_trigger);
            }
        }

        #region instructions
        
        private List<NPCInstruction> instructions = new List<NPCInstruction>();

        public void QueueInstruction(NPCInstruction new_instruction)
        {
            lock (this.instructions)
            {
                this.instructions.Add(new_instruction);
            }
        }
        
        public void StartHandlingQueue()
        {
            if (this.instructions.Count > 0)
            {
                this.InvokeNextInstruction();
            }
        }
        
        public void ClearInstructions()
        {
            lock (this.instructions)
            {
                this.instructions.Clear();
                this.CancelNavigation();
                this.StopAnimate();
                this.CancelWait();
				this.CancelDialog();
            }
        }

        
        private void NextInstruction(Object threadContext)
        {
            lock (this.instructions)
            {
                if (this.instructions.Count > 0)
                {
                    NPCInstruction instruction = this.instructions.First();
                    this.instructions.RemoveAt(0);
                    if (instruction is NPCSayInstruction)
                    {
                        NPCSayInstruction say_instruction = (NPCSayInstruction)instruction;
                        this.Say(say_instruction.Message, InvokeNextInstruction);
                    }
                    else if (instruction is NPCNavigationInstruction)
                    {
                        NPCNavigationInstruction navigation_instruction = (NPCNavigationInstruction)instruction;
                        this.NavigateTo(navigation_instruction.EndPointName, InvokeNextInstruction);
                    }
                    else if (instruction is NPCAnimationInstruction)
                    {
                        NPCAnimationInstruction animation_instruction = (NPCAnimationInstruction)instruction;
                        this.Animate(animation_instruction.AnimationName, animation_instruction.AnimationLength, InvokeNextInstruction);
                    }
                    else if (instruction is NPCAppearanceInstruction)
                    {
                        NPCAppearanceInstruction appearance_instruction = (NPCAppearanceInstruction)instruction;
                        this.SetAppearance(appearance_instruction.AppearanceName, InvokeNextInstruction);
                    }
                    else if (instruction is NPCLookAtInstruction)
                    {
                        NPCLookAtInstruction look_at_instruction = (NPCLookAtInstruction)instruction;
                        this.LookAt(look_at_instruction.NodeName, InvokeNextInstruction);
                    }
                    else if (instruction is NPCWaitInstruction)
                    {
                        NPCWaitInstruction wait_instruction = (NPCWaitInstruction)instruction;
                        this.Wait(wait_instruction.WaitMilliseconds, InvokeNextInstruction);
                    }
                    else if (instruction is NPCSpawnInstruction)
                    {
                        NPCSpawnInstruction spawn_instruction = (NPCSpawnInstruction)instruction;
                        this.Spawn(spawn_instruction.Spawn);
                        InvokeNextInstruction();
                    }
					else if (instruction is NPCSetMedia)
					{
						NPCSetMedia set_media_instruction = (NPCSetMedia)instruction;
						this.SetMedia(set_media_instruction.URL);
						InvokeNextInstruction();
					}
					else if (instruction is NPCClearMedia)
					{
						this.ClearMedia();
						InvokeNextInstruction();
					}
					else if (instruction is NPCRepeat)
					{
						this.current_event_group = null;
						InvokeNextInstruction();
					}
					else if (instruction is NPCDialog)
					{
						NPCDialog dialog_instruction = (NPCDialog)instruction;
						this.Dialog(dialog_instruction.Message, dialog_instruction.Options, dialog_instruction.Correct, InvokeNextInstruction);
					}
					else if (instruction is NPCLock)
					{
						NPCLock dialog_instruction = (NPCLock)instruction;
						this.Lock(dialog_instruction.Radius, dialog_instruction.KeyName, InvokeNextInstruction);
					}
					else if (instruction is NPCSit)
					{
						NPCSit dialog_instruction = (NPCSit)instruction;
						this.Sit(dialog_instruction.SitTarket, InvokeNextInstruction);
					}
                }
            }
        }

        public void InvokeNextInstruction()
        {
            //new Thread(() => this.NextInstruction()).Start();
            ThreadPool.QueueUserWorkItem(this.NextInstruction);
        }
        #endregion
    }
}

