using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Threading;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Mono.Addins;
using GD.Command;
using GD.Time;
using GD.Interfaces;

[assembly: Addin("GroupDRegionModule", "0.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace GD
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GD")]
    public class GDRM : INonSharedRegionModule, IGDRM
    {

        //Static stuff needed to output descriptive text to console.
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public string Name { get { return "GDRM"; } }
        public Type ReplaceableInterface { get { return null; } }

        private Boolean enabled = false;

        private static Dictionary<string, Scene> scenes = new Dictionary<string, Scene>();

        public void Initialise(IConfigSource source)
        {
            //Makes sure NPC module is enabled
            if (source.Configs["NPC"] == null || !source.Configs["NPC"].Contains("Enabled") || !source.Configs["NPC"].GetBoolean("Enabled"))
            {
                GDRM.log.ErrorFormat("[{0}]: NPC module not available. OpenSim.ini [NPC] must be enabled.", Name);
                return;
            }
            //Instance = this;
            this.Configure(source.Configs["GDRM"]);
            AssetManager.Instance.AppearanceDir = this.appearances_dir;
            AssetManager.Instance.ScriptDir = this.script_dir;
            AssetManager.Instance.EventsDir = this.event_dir;
            AssetManager.Instance.AnimationFile = this.animation_file;
            AssetManager.Instance.SoundFile = this.sound_file;
            AssetManager.Instance.ConvosDir = this.convo_dir;
            GDRM.log.DebugFormat("[{0}]: Initialized", Name);
            enabled = true;
        }

        public void Close()
        {
            if (enabled)
            {
                GDRM.log.DebugFormat("[{0}]: Closed", Name);
            }
        }

        private string SCENE_NAME;

        private Scene scene;
        public void AddRegion(Scene scene)
        {
            this.scene = scene;
            this.SCENE_NAME = scene.RegionInfo.RegionName;
            if (enabled)
            {
                //PCManager.Instance.AddRegion(scene);
                //CommandManager.Instance.AddRegion(scene);
                //InteractionManager.Instance.AddRegion(scene);
                //LightSourceManager.Instance.AddRegion(scene);
                Managers.SetGDRM(scene, this);
                scenes.Add(scene.RegionInfo.RegionName, scene);
                GDRM.log.DebugFormat("[{0}]: Added region '{1}'", Name, scene.RegionInfo.RegionName);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (enabled && scene.RegionInfo.RegionName.Equals(SCENE_NAME))
            {
                //PCManager pc_manager;
                //CommandManager command_manager;
                //InteractionManager interaction_manager;
                //LightSourceManager lightsource_manager;
                //pc_manager.RemoveRegion(scene);
                //command_manager.Instance.RemoveRegion(scene);
                //InteractionManager.Instance.RemoveRegion(scene);
                //LightSourceManager.Instance.RemoveRegion(scene);
                scenes.Remove(scene.RegionInfo.RegionName);
                GDRM.log.DebugFormat("[{0}]: Removed region '{1}'", Name, scene.RegionInfo.RegionName);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (enabled)
            {
                //this.Reload();
                GDRM.log.DebugFormat("[{0}]: Loaded region '{1}'", Name, scene.RegionInfo.RegionName);
            }
        }

        //public void PostInitialise()
        //{
        //    if (enabled)
        //    {
        //    }
        //}

        public static Scene GetSceneByName(string name)
        {
            Scene scene;
            scenes.TryGetValue(name, out scene);
            if(scene == null) GDRM.log.ErrorFormat("[GDRM]: Requested to get scene by name failed: '{0}'", name);
            return scene;
        }

        #region load/save

        private static char dir_split = Path.DirectorySeparatorChar;

        private string script_dir = "GDRM" + dir_split + "scripts";
        private string appearances_dir = "GDRM" + dir_split + "appearances";
        private string event_dir = "GDRM" + dir_split + "events";
        private string animation_file = "GDRM" + dir_split + "animations" + dir_split + "animations.xml";
        private string sound_file = "GDRM" + dir_split + "sounds" + dir_split + "sounds.xml";
        private string convo_dir = "GDRM" + dir_split + "convos";
        private string main_xml = "GDRM" + dir_split;

        private void Configure(IConfig config)
        {
            if(config == null) return;
            if (config.Contains("script_dir")) script_dir = config.GetString("script_dir");
            if (config.Contains("appearance_dir")) appearances_dir = config.GetString("appearance_dir");
            if (config.Contains("animation_file")) animation_file = config.GetString("animation_file");
            if (config.Contains("sound_file")) sound_file = config.GetString("sound_file");
            if (config.Contains("event_dir")) event_dir = config.GetString("event_dir");
            if (config.Contains("convo_dir")) convo_dir = config.GetString("convo_dir");
            if (config.Contains("main_xml")) main_xml = config.GetString("main_xml");
        }

        public void Save()
        {
            try
            {
                XmlDocument xml_doc = new XmlDocument();
                XmlElement xml_root = xml_doc.CreateElement("gd");
                XmlElement xml_graph = xml_doc.CreateElement("graph");
                XmlElement xml_nodes = xml_doc.CreateElement("nodes");
                XmlElement xml_links = xml_doc.CreateElement("links");
                XmlElement xml_npcs = xml_doc.CreateElement("npcs");
                XmlElement xml_light_sources = xml_doc.CreateElement("light_sources");

                xml_doc.AppendChild(xml_root);
                xml_root.AppendChild(xml_graph);
                xml_root.AppendChild(xml_npcs);
                xml_root.AppendChild(xml_light_sources);
                xml_graph.AppendChild(xml_nodes);
                xml_graph.AppendChild(xml_links);



                GraphNode[] nodes = NavManager.Instance.Nodes.ToArray();
                Dictionary<GraphNode, string> node_descriptors = new Dictionary<GraphNode, string>();
                for (int i = 0; i < nodes.Length; i++)
                {
                    node_descriptors.Add(nodes[i], i.ToString());
                }

                foreach (GraphNode graph_node in nodes) 
                {
                    XmlElement new_node = xml_doc.CreateElement("node");
                    string node_id; node_descriptors.TryGetValue(graph_node, out node_id);
                    AddXmlNode(new_node, "id", graph_node.Location.Scene.RegionInfo.RegionName + ":" + node_id);
                    AddXmlNode(new_node, "name", graph_node.Name);
                    AddXmlNode(new_node, "x", graph_node.Location.X.ToString());
                    AddXmlNode(new_node, "y", graph_node.Location.Y.ToString());
                    AddXmlNode(new_node, "z", graph_node.Location.Z.ToString());
                    AddXmlNode(new_node, "scene", graph_node.Location.Scene.RegionInfo.RegionName);
                    xml_nodes.AppendChild(new_node);
                    foreach (GraphNode neighbour in graph_node.Neighbours)
                    {
                        XmlElement new_link = xml_doc.CreateElement("link");
                        string neighbour_id; node_descriptors.TryGetValue(neighbour, out neighbour_id);
                        AddXmlNode(new_link, "a", graph_node.Location.Scene.RegionInfo.RegionName + ":" + node_id.ToString());
                        AddXmlNode(new_link, "b", neighbour.Location.Scene.RegionInfo.RegionName + ":" + neighbour_id.ToString());
                        xml_links.AppendChild(new_link);
                    }
                }

                foreach (NPCharacter npc_character in NPCManager.Instance.NPCs)
                {
                    XmlElement new_node = xml_doc.CreateElement("npc");
                    AddXmlNode(new_node, "first_name", npc_character.FirstName);
                    AddXmlNode(new_node, "last_name", npc_character.LastName);
                    AddXmlNode(new_node, "appearance", npc_character.Appearance);
                    AddXmlNode(new_node, "convo", npc_character.Convo);
                    AddXmlNode(new_node, "script", ((PyNPCharacter)npc_character).ScriptName);
                    AddXmlNode(new_node, "x", npc_character.StartingLocation.X.ToString());
                    AddXmlNode(new_node, "y", npc_character.StartingLocation.Y.ToString());
                    AddXmlNode(new_node, "z", npc_character.StartingLocation.Z.ToString());
                    AddXmlNode(new_node, "scene", npc_character.StartingLocation.Scene.RegionInfo.RegionName);
                    xml_npcs.AppendChild(new_node);
                }

                ILightSourceManager lightsource_manager = Managers.GetLightSourceManager(this.scene);

                foreach (LightSource light_source in lightsource_manager.LightSources)
                {
                    XmlElement new_node = xml_doc.CreateElement("light_source");
                    AddXmlNode(new_node, "x", light_source.Location.X.ToString());
                    AddXmlNode(new_node, "y", light_source.Location.Y.ToString());
                    AddXmlNode(new_node, "z", light_source.Location.Z.ToString());
                    AddXmlNode(new_node, "scene", light_source.Location.Scene.RegionInfo.RegionName);
                    xml_light_sources.AppendChild(new_node);
                }

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                XmlWriter writer = XmlWriter.Create(this.main_xml + this.SCENE_NAME + ".xml", settings);
                xml_doc.Save(writer);
                writer.Close();
            }
            catch (Exception e)
            {
                GDRM.log.ErrorFormat("[GDRM]: Error saving data to file: {0}", e.Message);
            }
        }

        private void Load()
        {
            try
            {
                XmlDocument xml_doc = new XmlDocument();
                XmlTextReader reader = new XmlTextReader(this.main_xml + this.SCENE_NAME + ".xml");
                xml_doc.Load(reader);
                reader.Close();

                XmlElement xml_root = xml_doc.DocumentElement;
                if (xml_root != null && xml_root.LocalName.Equals("gd"))
                {
                    XmlElement xml_graph = xml_root["graph"];
                    XmlElement xml_npcs = xml_root["npcs"];
                    XmlElement xml_light_sources = xml_root["light_sources"];
                    if (xml_graph != null && xml_npcs != null)
                    {
                        LoadGraphXml(xml_graph);
                        LoadNPCsXml(xml_npcs);
                        LoadLightSourcesXml(xml_light_sources);
                    }
                    else
                    {
                        throw new Exception("No graph or npcs node");
                    }
                }
                else
                {
                    throw new Exception("Invalid root node");
                }
            }
            catch (Exception e)
            {
                GDRM.log.ErrorFormat("[GDRM]: Error loading data for region '{0}': {1}", this.SCENE_NAME, e.Message);
                throw e;
            }
        }

        public void Reload()
        {
            try { NPCManager.Instance.Clear(); }
            catch (Exception e) { GDRM.log.ErrorFormat("[GDRM]: NPCManager clearing failed: {0}", e.Message); }

            try { AssetManager.Instance.Clear(); }
            catch (Exception e) { GDRM.log.ErrorFormat("[GDRM]: AssetManager clearing failed: {0}", e.Message); }

            try { NavManager.Instance.Clear(); }
            catch (Exception e) { GDRM.log.ErrorFormat("[GDRM]: NavManager clearing failed: {0}", e.Message); }

            try { AssetManager.Instance.LoadAssets(); }
            catch (Exception e) { GDRM.log.ErrorFormat("[GDRM]: AssetManager load failed: {0}", e.Message); }

            ILightSourceManager lightsource_manager = Managers.GetLightSourceManager(this.scene);
            try { lightsource_manager.Clear(); }
            catch (Exception e) { GDRM.log.ErrorFormat("[GDRM]: LightSourceManager clearing failed: {0}", e.Message); }

            ITimeManager time_manager = Managers.GetTimeManager(this.scene);
            time_manager.Time = new DateTime(2013, 01, 01, 5, 40, 0);
            
            try
            {
                this.Load();
                NPCManager.Instance.RespawnAll();
            }
            catch (Exception e)
            {
                GDRM.log.ErrorFormat("[GDRM]: Reloading failed: {0}", e.Message);
            }
        }

        private void LoadGraphXml(XmlElement graph_element)
        {
            if (graph_element != null)
            {
                XmlElement xml_nodes = graph_element["nodes"];
                XmlElement xml_links = graph_element["links"];
                if (xml_nodes != null && xml_links != null)
                {
                    Dictionary<string, GraphNode> node_descriptors = new Dictionary<string, GraphNode>();
                    foreach (XmlElement xml_node in xml_nodes.ChildNodes)
                    {
                        if (xml_node.LocalName.Equals("node"))
                        {
                            XmlElement name_node = xml_node["name"];
                            XmlElement id_node = xml_node["id"];
                            Location location = this.LoadLocationXml(xml_node);
                            if (name_node != null && id_node != null && location != null)
                            {
                                string name = name_node.InnerText;
                                string id = id_node.InnerText;
                                GraphNode new_node = new GraphNode(location, name);
                                NavManager.Instance.AddNode(new_node);
                                node_descriptors.Add(id, new_node);
                            }
                            else
                            {
                                throw new Exception("[GDRM]: Error loading data from file: Node missing name or id.");
                            }
                        }
                    }

                    foreach (XmlElement xml_link in xml_links.ChildNodes)
                    {
                        if (xml_link.LocalName.Equals("link"))
                        {
                            if (xml_link["a"] != null && xml_link["b"] != null)
                            {
                                string a = xml_link["a"].InnerText;
                                string b = xml_link["b"].InnerText;
                                GraphNode node_a, node_b;
                                if (node_descriptors.TryGetValue(a, out node_a) && node_descriptors.TryGetValue(b, out node_b))
                                {
                                    NavManager.Instance.AddLink(node_a, node_b);
                                }
                                else
                                {
                                    throw new Exception("[GDRM]: Error loading data from file: Linked node doesn't exist: " + ((node_a == null) ? a : b));
                                }
                            }
                            else
                            {
                                throw new Exception("[GDRM]: Error loading data from file: Link missing either/both 'a' and/or 'b' node(s) to join.");
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("[GDRM]: Error loading data from file: No nodes, or no links, node.");
                }
            }
            else
            {
                throw new Exception("[GDRM]: Error loading data from file: No graph node.");
            }
        }

        private void LoadNPCsXml(XmlElement npcs_element)
        {
            if (npcs_element != null)
            {
                foreach (XmlElement xml_npc in npcs_element.ChildNodes)
                {
                    if (xml_npc.LocalName.Equals("npc"))
                    {
                        if (xml_npc["first_name"] != null && xml_npc["last_name"] != null && xml_npc["appearance"] != null && xml_npc["script"] != null)
                        {
                            string first_name = xml_npc["first_name"].InnerText;
                            string last_name = xml_npc["last_name"].InnerText;
                            string appearance = xml_npc["appearance"].InnerText;
                            string convo = xml_npc["convo"].InnerText;
                            string script = xml_npc["script"].InnerText;
                            Location location = this.LoadLocationXml(xml_npc);
                            if (first_name != null && last_name != null && appearance != null && script != null && location != null)
                            {
                                NPCharacter new_npc = new PyNPCharacter(first_name, last_name, appearance, convo, location, script);
                                NPCManager.Instance.AddNPC(new_npc);
                                //INPCModule npc_module = this.scene.RequestModuleInterface<INPCModule>();
                                //npc_module.CreateNPC(first_name, last_name, location.Vector, UUID.Zero, false, this.scene, null);
                            }
                            else
                            {
                                throw new Exception("Error loading NPC data. Missing an attribute");
                            }
                        }
                        else
                        {
                            throw new Exception("Error loading NPC data. Missing an attribute");
                        }
                    }
                }
            }
        }

        private void LoadLightSourcesXml(XmlElement npcs_element)
        {
            if (npcs_element != null)
            {
                foreach (XmlElement xml_light_source in npcs_element.ChildNodes)
                {
                    if (xml_light_source.LocalName.Equals("light_source"))
                    {
                        if (xml_light_source["x"] != null)
                        {
                            string x = xml_light_source["x"].InnerText;
                            Location location = this.LoadLocationXml(xml_light_source);
                            if (x != null && location != null)
                            {
                                LightSource light = new LightSource(location);
                                ILightSourceManager lightsource_manager = Managers.GetLightSourceManager(this.scene);
                                lightsource_manager.AddLightSource(light);
                            }
                            else
                            {
                                throw new Exception("Error loading LightSource data. Missing an attribute");
                            }
                        }
                        else
                        {
                            throw new Exception("Error loading LightSource data. Missing an attribute");
                        }
                    }
                }
            }
        }

        private Location LoadLocationXml(XmlElement xml_node)
        {
            string x_node = xml_node["x"].InnerText;
            string y_node = xml_node["y"].InnerText;
            string z_node = xml_node["z"].InnerText;
            string scene_node = xml_node["scene"].InnerText;
            if (x_node != null && y_node != null && z_node != null && scene_node != null)
            {
                float x, y, z;
                if (float.TryParse(x_node, out x) && float.TryParse(y_node, out y) && float.TryParse(z_node, out z))
                {
                    Scene scene = GDRM.GetSceneByName(scene_node);
                    if (scene != null)
                    {
						log.WarnFormat("X: {0} Y: {1} Z: {2} X: {3} Y: {4} Z: {5}", x_node, y_node, z_node , x, y, z);
                        return new Location(scene, x, y, z);
                    }
                    else
                    {
                        throw new Exception("[GDRM]: Error loading location data. Could not find scene name.");
                    }
                }
                else
                {
                    throw new Exception("[GDRM]: Error loading location data. Invalid co-ordinates");
                }
            }
            else
            {
                throw new Exception("[GDRM]: Error loading location data. Graph_node xml node missing.");
            }
        }

        private void AddXmlNode(XmlElement root, string tag_name, string content)
        {
            XmlDocument xml_doc = root.OwnerDocument;
            XmlElement xml_element = xml_doc.CreateElement(tag_name);
            xml_element.InnerText = content;
            root.AppendChild(xml_element);
        }

        #endregion
    }
}
