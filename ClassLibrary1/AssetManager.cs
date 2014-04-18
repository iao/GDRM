using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Framework;
using System.IO;
using OpenMetaverse.StructuredData;
using OpenMetaverse;
using System.Xml;
using GD.Time;

namespace GD
{
    class AssetManager
    {
        private static AssetManager _instance = new AssetManager();
        public static AssetManager Instance { get { return _instance; } }

        private Dictionary<string, UUID> animations;
        private Dictionary<string, UUID> sounds;
        private Dictionary<string, List<NPCEventTrigger>> scripts;
		private Dictionary<string, List<NPCEventProximityTrigger>> proximityScripts;
        private Dictionary<string, List<AvatarAppearance>> appearances;
        private Dictionary<string, NPCEventGroup> events;
        private Dictionary<string, List<NPCConvoTrigger>> convos;
        public AvatarAppearance DefaultAppearance { get; private set; }

        public string AppearanceDir {get; set; }
        public string ScriptDir { get; set; }
        public string AnimationFile { get; set; }
        public string SoundFile { get; set; }
        public string EventsDir { get; set; }
        public string ConvosDir { get; set; }

        private AssetManager()
        {
            this.AppearanceDir = null;
            this.ScriptDir = null;
            this.AnimationFile = null;
            this.SoundFile = null;
            this.EventsDir = null;
            this.ConvosDir = null;
            this.Clear();
        }

        public void Clear()
        {
            animations = new Dictionary<string, UUID>();
            sounds = new Dictionary<string, UUID>();
            scripts = new Dictionary<string, List<NPCEventTrigger>>();
			proximityScripts = new Dictionary<string, List<NPCEventProximityTrigger>>();
            appearances = new Dictionary<string,List<AvatarAppearance>>();
            events = new Dictionary<string, NPCEventGroup>();
            convos = new Dictionary<string, List<NPCConvoTrigger>>();
            this.SetDefault();
        }

        public void LoadAssets()
        {
            this.LoadAppearances();
            this.LoadEvents();
            this.LoadScripts();
            this.LoadAnimations();
            this.LoadSounds();
            this.LoadConvos();
        }

        #region appearance

        public bool TryGetAppearance(string appearance_name, int appearance_number, out AvatarAppearance appearance)
        {
            List<AvatarAppearance> appearance_list;
            if (this.appearances.TryGetValue(appearance_name, out appearance_list))
            {
                int list_size = appearance_list.Count;
                int list_position = appearance_number % list_size;
                list_position = (list_position < 0)?list_position + list_size:list_position;
                appearance = appearance_list[list_position];
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Appearance does not exist: '{0}'. Using default.", appearance_name);
                appearance = DefaultAppearance;
                return false;
            }
        }

        private void LoadAppearances()
        {
            if (Directory.Exists(this.AppearanceDir))
            {
                //adds all of the appearances from the main appearance directory (eg. appearances with only one option).
                Dictionary<string, AvatarAppearance> main_directory_appearances = this.LoadAppearanceDirectory(this.AppearanceDir);
                foreach (string key_string in main_directory_appearances.Keys)
                {
                    List<AvatarAppearance> appearance_list = new List<AvatarAppearance>();
                    appearance_list.Add(main_directory_appearances[key_string]);
                    this.appearances.Add(key_string, appearance_list);
                }

                foreach (string directory in Directory.GetDirectories(this.AppearanceDir))
                {
                    Dictionary<string, AvatarAppearance> sub_directory_appearances = this.LoadAppearanceDirectory(directory);
                    List<AvatarAppearance> appearances = new List<AvatarAppearance>();
                    foreach (AvatarAppearance appearance in sub_directory_appearances.Values)
                    {
                        appearances.Add(appearance);
                    }
                    string appearance_name = directory.Split(Path.DirectorySeparatorChar).Last();
                    if (this.appearances.ContainsKey(appearance_name))
                    {
                        AvatarAppearance existing_apperance = this.appearances[appearance_name].First();
                        appearances.Add(existing_apperance);
                        this.appearances.Remove(appearance_name);
                    }
                    this.appearances.Add(appearance_name, appearances);
                }
            }
            else
            {
                GDRM.log.ErrorFormat("[AssetManager]: Appearance directory does not exist: '{0}'", this.AppearanceDir);
            }
            this.SetDefault();
        }

        private Dictionary<string, AvatarAppearance> LoadAppearanceDirectory(string directory_location)
        {
            Dictionary<string, AvatarAppearance> appearances = new Dictionary<string, AvatarAppearance>();
            if (Directory.Exists(this.AppearanceDir))
            {
                int attempts = 0;
                int success = 0;
                foreach (string file_name in Directory.GetFiles(directory_location, "*.xml"))
                {
                    try
                    {
                        attempts += 1;
                        AvatarAppearance appearance = this.LoadAppearanceFile(file_name);
                        if (appearance != null)
                        {
                            success += 1;
                            appearances.Add(this.IsolateFilename(file_name), appearance);
                        }

                    }
                    catch (Exception e)
                    {
                        GDRM.log.ErrorFormat("[AssetManager]: Failed to load appearance '{0}': {1}", file_name, e.Message);
                    }
                }
                GDRM.log.InfoFormat("[AssetManager]: {0}/{1} appearances loaded from directory '{2}'", success, attempts, directory_location);
            }
            else
            {
                GDRM.log.ErrorFormat("[AssetManager]: Appearance directory does not exist: '{0}'", directory_location);
            }
            return appearances;
        }

        private AvatarAppearance LoadAppearanceFile(string file_location)
        {
            AvatarAppearance appearance = new AvatarAppearance();

            //load the file then deserialise it into an appearance.
            string file_contents = File.ReadAllText(file_location);

            if (file_contents != null)
            {
                appearance.Unpack((OSDMap)OSDParser.DeserializeLLSDXml(file_contents));
                return appearance;
            }
            else
            {
                GDRM.log.ErrorFormat("[AsseteManager]: Failed to load appearance '{0}': File is empty", file_location);
                return null;
            }
        }

        private void SetDefault()
        {
            List<AvatarAppearance> default_appearances;
            if (appearances.TryGetValue("default", out default_appearances) && default_appearances.Count > 0)
            {
                this.DefaultAppearance = default_appearances[0];
            }
            else
            {
                DefaultAppearance = new AvatarAppearance();
            }
        }
        
        #endregion

        #region animation

        public bool TryGetAnimation(string animation_name, out UUID animation)
        {
            if (this.animations.TryGetValue(animation_name, out animation))
            {
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Animation does not exist: '{0}'.", animation_name);
                animation = UUID.Zero;
                return false;
            }
        }

        private void LoadAnimations()
        {
            try
            {
                XmlDocument xml_doc = new XmlDocument();
                XmlTextReader reader = new XmlTextReader(this.AnimationFile);
                xml_doc.Load(reader);
                reader.Close();
                XmlElement xml_root = xml_doc.DocumentElement;
                if (xml_root != null && xml_root.LocalName.Equals("animations"))
                {
                    foreach (XmlElement xml_animation in xml_root.ChildNodes)
                    {
                        if (xml_animation.LocalName.Equals("animation"))
                        {
                            string animation_name = xml_animation["name"].InnerText;
                            string animation_uuid_string = xml_animation["uuid"].InnerText;
                            UUID animation_uuid = new UUID(animation_uuid_string);
                            this.animations.Add(animation_name, animation_uuid);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GDRM.log.ErrorFormat("[AssetManager]: Could not load animations: " + e.Message);
            }
        }

        #endregion

        #region sound

        public bool TryGetSound(string sound_name, out UUID sound)
        {
            if (this.sounds.TryGetValue(sound_name, out sound))
            {
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Sound does not exist: '{0}'.", sound_name);
                sound = UUID.Zero;
                return false;
            }
        }

        private void LoadSounds()
        {
            try
            {
                XmlDocument xml_doc = new XmlDocument();
                XmlTextReader reader = new XmlTextReader(this.SoundFile);
                xml_doc.Load(reader);
                reader.Close();
                XmlElement xml_root = xml_doc.DocumentElement;
                if (xml_root != null && xml_root.LocalName.Equals("sounds"))
                {
                    foreach (XmlElement xml_sound in xml_root.ChildNodes)
                    {
                        if (xml_sound.LocalName.Equals("sound"))
                        {
                            string sound_name = xml_sound["name"].InnerText;
                            string sound_uuid_string = xml_sound["uuid"].InnerText;
                            UUID sound_uuid = new UUID(sound_uuid_string);
                            this.sounds.Add(sound_name, sound_uuid);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GDRM.log.ErrorFormat("[AssetManager]: Could not load sounds: " + e.Message);
            }
        }

        #endregion

        #region script

        public bool TryGetScript(string script_name, out List<NPCEventTrigger> npc_event_triggers, out List<NPCEventProximityTrigger> npc_event_proximity_triggers)
        {
			if (this.scripts.TryGetValue(script_name, out npc_event_triggers) && this.proximityScripts.TryGetValue(script_name, out npc_event_proximity_triggers))
            {
                npc_event_triggers = npc_event_triggers.ToList();
				npc_event_proximity_triggers = npc_event_proximity_triggers.ToList();
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Script does not exist: '{0}'.", script_name);
                npc_event_triggers = new List<NPCEventTrigger>();
                return false;
            }
        }

        private void LoadScripts()
        {
            if (Directory.Exists(this.ScriptDir))
            {
                int attempts = 0;
                int success = 0;
                foreach (string file_name in Directory.GetFiles(this.ScriptDir, "*.xml"))
                {
                    try
                    {
                        attempts += 1;
                        if (this.LoadScript(file_name)) success += 1;
                    }
                    catch (Exception e)
                    {
                        GDRM.log.ErrorFormat("[AssetManager]: Failed to load script '{0}': {1}", file_name, e.Message);
                    }
                }
                GDRM.log.InfoFormat("[AssetManager]: {0}/{1} scripts loaded.", success, attempts);
            }
            else
            {
                GDRM.log.ErrorFormat("[AssetManager]: Script directory does not exist: '{0}'", this.ScriptDir);
            }
        }

        private bool LoadScript(string file_location)
        {
            string[] path_segments = file_location.Split(Path.DirectorySeparatorChar);
            string file_name = path_segments.Last();
            string script_name = file_name.Substring(0, file_name.Length - 4);


            XmlDocument xml_doc = new XmlDocument();
            XmlTextReader reader = new XmlTextReader(file_location);
            xml_doc.Load(reader);
            reader.Close();
            XmlElement xml_root = xml_doc.DocumentElement;
            if (xml_root != null && xml_root.LocalName.Equals("events"))
            {
                List<NPCEventTrigger> npc_event_triggers = new List<NPCEventTrigger>();
				List<NPCEventProximityTrigger> npc_event_proximity_triggers = new List<NPCEventProximityTrigger>();
                foreach (XmlElement xml_script in xml_root.ChildNodes)
                {
                    if (xml_script.LocalName.Equals("event"))
                    {
                        string event_name = xml_script["name"].InnerText;
                        string event_time_string = xml_script["trigger_time"].InnerText;
                        DateTime event_time = TimeManager.GetDateTimeByString(event_time_string);

                        NPCEventGroup event_group;
                        if (this.TryGetEventGroup(event_name, out event_group))
                        {
                           npc_event_triggers.Add(new NPCEventTrigger(event_time, event_group));
                        }
                    }
					else if(xml_script.LocalName.Equals("proximity_event"))
					{
						int radias;
						Int32.TryParse(xml_script["radias"].InnerText, out radias);
						string enterEvent = (xml_script["enter_event"] != null)?(xml_script["enter_event"].InnerText):null;
						string exitEvent = (xml_script["exit_event"] != null)?(xml_script["exit_event"].InnerText):null;
						string nodeName = (xml_script["node"] != null)?(xml_script["node"].InnerText):null;
						NPCEventGroup enter_group;
						if (enterEvent == null || !this.TryGetEventGroup(enterEvent, out enter_group))
						{
							enter_group = null;
						}
						NPCEventGroup exit_group;
						if (exitEvent == null || !this.TryGetEventGroup(exitEvent, out exit_group))
						{
							exit_group = null;
						}
						npc_event_proximity_triggers.Add(new NPCEventProximityTrigger(radias, enter_group, exit_group, nodeName));
					}
                }
				GDRM.log.DebugFormat("[AssetManager]: Script loaded: '{0}' as '{1}' with {2} events triggers and {3} proximity triggers.", file_location, script_name, npc_event_triggers.Count, npc_event_proximity_triggers.Count);
                this.scripts.Add(script_name, npc_event_triggers);
				this.proximityScripts.Add(script_name, npc_event_proximity_triggers);
                return true;
            }
            return false;
        }

        #endregion

        #region events

        public bool TryGetEventGroup(string event_name, out NPCEventGroup event_group)
        {
            if (this.events.TryGetValue(event_name, out event_group))
            {
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Event does not exist: '{0}'.", event_name);
                event_group = null;
                return false;
            }
        }

        private void LoadEvents()
        {
            if (Directory.Exists(this.EventsDir))
            {
                int attempts = 0;
                int success = 0;
                foreach (string file_name in Directory.GetFiles(this.EventsDir, "*.xml"))
                {
                    try
                    {
                        attempts += 1;
                        if (this.LoadEventFile(file_name)) success += 1;
                    }
                    catch (Exception e)
                    {
                        GDRM.log.ErrorFormat("[AssetManager]: Failed to load event '{0}': {1}", file_name, e.Message);
                    }
                }
                GDRM.log.InfoFormat("[AssetManager]: {0}/{1} events loaded.", success, attempts);
            }
            else
            {
                GDRM.log.ErrorFormat("[AssetManager]: Events directory does not exist: '{0}'", this.EventsDir);
            }
            this.SetDefault();
        }

        private bool LoadEventFile(string file_location)
        {


            XmlDocument xml_doc = new XmlDocument();
            XmlTextReader reader = new XmlTextReader(file_location);
            xml_doc.Load(reader);
            reader.Close();
            XmlElement xml_root = xml_doc.DocumentElement;
            if (xml_root != null && xml_root.LocalName.Equals("events"))
            {
                foreach (XmlElement xml_script in xml_root.ChildNodes)
                {
                    if (xml_script.LocalName.Equals("event"))
                    {
                        string event_name = xml_script["name"].InnerText;
                        string max_participants_string = xml_script["max"].InnerText;
                        int max_participants = int.Parse(max_participants_string);

                        XmlElement xml_instructions = xml_script["instructions"];

                        List<NPCInstruction> npc_instructions = new List<NPCInstruction>();
                        foreach (XmlElement xml_instruction in xml_instructions.ChildNodes)
                        {
                            switch (xml_instruction.LocalName)
                            {
                                case NPCSayInstruction.Identifier:
                                    string message = xml_instruction["message"].InnerText;
                                    npc_instructions.Add(new NPCSayInstruction(message));
                                    break;
                                case NPCAnimationInstruction.Identifier:
                                    string animation_name = xml_instruction["animation_name"].InnerText;
                                    int animation_time = int.Parse(xml_instruction["animation_duration"].InnerText);
                                    npc_instructions.Add(new NPCAnimationInstruction(animation_name, animation_time));
                                    break;
                                case NPCNavigationInstruction.Identifier:
                                    string end_point = xml_instruction["node_name"].InnerText;
                                    npc_instructions.Add(new NPCNavigationInstruction(end_point));
                                    break;
                                case NPCAppearanceInstruction.Identifier:
                                    string appearance = xml_instruction["appearance_name"].InnerText;
                                    npc_instructions.Add(new NPCAppearanceInstruction(appearance));
                                    break;
                                case NPCLookAtInstruction.Identifier:
                                    string node_name = xml_instruction["node_name"].InnerText;
                                    npc_instructions.Add(new NPCLookAtInstruction(node_name));
                                    break;
                                case NPCWaitInstruction.Identifier:
                                    int wait_milliseconds = int.Parse(xml_instruction["wait_milliseconds"].InnerText);
                                    npc_instructions.Add(new NPCWaitInstruction(wait_milliseconds));
                                    break;
                                case NPCSpawnInstruction.Identifier:
                                    bool spawned = bool.Parse(xml_instruction["spawned"].InnerText);
                                    npc_instructions.Add(new NPCSpawnInstruction(spawned));
                                    break;
								case NPCSetMedia.Identifier:
									string url = xml_instruction["url"].InnerText;
									npc_instructions.Add(new NPCSetMedia(url));
									break;
								case NPCClearMedia.Identifier:
									npc_instructions.Add(new NPCClearMedia());
									break;
								case NPCRepeat.Identifier:
									npc_instructions.Add(new NPCRepeat());
									break;
								case NPCDialog.Identifier:
									string dialogMessage = xml_instruction["message"].InnerText;
									string options = xml_instruction["options"].InnerText;
									string correct = xml_instruction["correct"].InnerText;
									npc_instructions.Add(new NPCDialog(dialogMessage, options, correct));
									break;
								case NPCLock.Identifier:
									GDRM.log.WarnFormat("[AssetManager]: Creating lock step {0}", xml_instruction.OuterXml);
									string keyName = xml_instruction["KeyName"].InnerText;
									int radius;
									Int32.TryParse(xml_instruction["Radius"].InnerText, out radius);
									npc_instructions.Add(new NPCLock(keyName, radius));
									GDRM.log.WarnFormat("[AssetManager]: Created lock step {0}", xml_instruction.OuterXml);
									break;
								case NPCSit.Identifier:
									string sitTarget_uuid_string = xml_instruction["SitTarget"].InnerText;
									UUID SitTarget = new UUID(sitTarget_uuid_string);
									npc_instructions.Add(new NPCSit(SitTarget));
									break;
								default:
									GDRM.log.WarnFormat("[AssetManager]: Event '{0}' contains unexpected function '{1}'. Instruction will be ignored.", file_location, xml_instruction.LocalName);
									break;
                            }
                        }
                        
                        for (int i = 0; i < max_participants; i++)
                        {
                            NPCEvent new_event = new NPCEvent();
                            foreach (NPCInstruction instruction in npc_instructions)
                            {
                                new_event.AddInstruction(instruction);
                            }
                            this.AddEvent(event_name, new_event);
                            GDRM.log.DebugFormat("[AssetManager]: Event '{0}' added from events file '{1}' with {2} instructions.", event_name, file_location, new_event.Instructions.Count);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private void AddEvent(string name, NPCEvent new_event)
        {
            NPCEventGroup event_group;
            if (!this.events.TryGetValue(name, out event_group))
            {
                event_group = new NPCEventGroup();
                this.events.Add(name, event_group);
            }
            event_group.AddEvent(new_event);
        }

        #endregion

        #region convo

        public bool TryGetConvo(string convo_name, out List<NPCConvoTrigger> convo)
        {
            if (this.convos.TryGetValue(convo_name, out convo))
            {
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AssetManager]: Convo does not exist: '{0}'.", convo_name);
                convo = null;
                return false;
            }
        }

        private void LoadConvos()
        {
            if (Directory.Exists(this.ConvosDir))
            {
                int attempts = 0;
                int success = 0;
                foreach (string file_name in Directory.GetFiles(this.ConvosDir, "*.xml"))
                {
                    try
                    {
                        attempts += 1;
                        if (this.LoadConvoFile(file_name)) success += 1;
                    }
                    catch (Exception e)
                    {
                        GDRM.log.ErrorFormat("[AssetManager]: Failed to load convo '{0}': {1}", file_name, e.Message);
                    }
                }
                GDRM.log.InfoFormat("[AssetManager]: {0}/{1} convos loaded.", success, attempts);
            }
            else
            {
                GDRM.log.ErrorFormat("[AssetManager]: Convos directory does not exist: '{0}'", this.ConvosDir);
            }
            this.SetDefault();
        }

        private bool LoadConvoFile(string file_location)
        {
            XmlDocument xml_doc = new XmlDocument();
            XmlTextReader reader = new XmlTextReader(file_location);
            xml_doc.Load(reader);
            reader.Close();
            XmlElement xml_root = xml_doc.DocumentElement;
            if (xml_root != null && xml_root.LocalName.Equals("convos"))
            {
                foreach (XmlElement xml_convo in xml_root.GetElementsByTagName("convo"))
                {
                    string convo_name = xml_convo["name"].InnerText;
                    List<NPCConvoTrigger> convo_triggers = new List<NPCConvoTrigger>();
                    foreach (XmlElement xml_trigger in xml_convo.GetElementsByTagName("trigger"))
                    {
                        string message = xml_trigger["message"].InnerText;
                        string sound = (xml_trigger["sound"] != null) ? xml_trigger["sound"].InnerText : null;

                        List<String> trigger_texts = new List<String>();
                        foreach (XmlElement xml_trigger_text in xml_trigger.GetElementsByTagName("trigger_text"))
                        {
                            trigger_texts.Add(xml_trigger_text.InnerText);
                        }
                        if (trigger_texts.Count == 0 || message == null)
                        {
                            GDRM.log.WarnFormat("[AssetManager]: Failed to load convo trigger in convo '{0}' in file '{1}'. Invalid attributes (missing message, or trigger_text).", convo_name, file_location);
                        }
                        else
                        {
                            convo_triggers.Add(new NPCConvoTrigger(trigger_texts, message, sound));
                        }

                    }
                    lock (this.convos)
                    {
                        this.convos.Add(convo_name, convo_triggers);
                    }
                }
                return true;
            }
            return false;
        }

        #endregion

        private string IsolateFilename(string path)
        {
            string[] path_segments = path.Split(Path.DirectorySeparatorChar);
            string file_name_long = path_segments.Last();
            string file_name = file_name_long.Substring(0, file_name_long.Count() - 4);
            return file_name;
        }
    }
}
