using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using GD.Command;
using GD.Time;
using OpenSim.Region.Framework.Interfaces;
using GD.Interfaces;

using Mono.Addins;

using Nini.Config;

namespace GD
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "InteractionManager")]
    class InteractionManager : INonSharedRegionModule, IInteractionManager
    {
        //private static InteractionManager _instance = new InteractionManager();
        //public static InteractionManager Instance { get { return _instance; } }

        public string Name { get { return "InteractionManager"; } }
        public Type ReplaceableInterface { get { return null; } }

        public bool Interactable { get; private set; }
        private UUID interactor = UUID.Zero;

        private Dictionary<SceneObjectGroup, NPCharacter> visualized_npcs = new Dictionary<SceneObjectGroup, NPCharacter>();
        private Dictionary<SceneObjectGroup, GraphNode> visualized_nodes = new Dictionary<SceneObjectGroup, GraphNode>();
        private Dictionary<GraphNode, SceneObjectGroup> visualized_nodes_reverse = new Dictionary<GraphNode, SceneObjectGroup>();
        private Dictionary<SceneObjectGroup, VisLink> visualized_links = new Dictionary<SceneObjectGroup, VisLink>();
        private Dictionary<VisLink, SceneObjectGroup> visualized_links_reverse = new Dictionary<VisLink, SceneObjectGroup>();

        private Dictionary<SceneObjectGroup, LightSource> visualized_light_sources = new Dictionary<SceneObjectGroup, LightSource>();

        private Dictionary<UUID, SceneObjectGroup> uuid_to_sog = new Dictionary<UUID, SceneObjectGroup>();

        private ILightSourceManager lightsource_manager;
        private ICommandManager command_manager;

        public InteractionManager()
        {
            
        }

        private Scene scene;
        public void AddRegion(Scene scene)
        {
            this.scene = scene;
            Managers.SetInteractionManager(scene, this);
        }

        public void RemoveRegion(Scene scene)
        {
            this.scene = null;
        }

        public void StartInteraction(UUID interactor)
        {
            if (this.Interactable) return;
            this.interactor = interactor;
            this.VisualizeAll();
            this.SetHooks(true);
            Interactable = true;
        }

        public void StopInteraction()
        {
            if (!this.Interactable) return;
            this.SetHooks(false);
            this.DevisualizeAll();
            this.interactor = UUID.Zero;
            Interactable = false;
        }

        #region visualization

        private void VisualizeAll()
        {
            this.VisualizeNPCs();
            this.VisualizeGraph();
            this.VisualizeLightSources();
        }

        private void VisualizeNPCs()
        {
            IEnumerable<NPCharacter> non_playing_characters = NPCManager.Instance.NPCs;
            lock (non_playing_characters)
            {
                lock (this.visualized_npcs)
                {
                    foreach (NPCharacter npc in non_playing_characters)
                    {
                        VisualizeNPC(npc);
                    }
                }
            }
        }

        private void VisualizeNPC(NPCharacter npc)
        {
            SceneObjectGroup visualized_npc = new SceneObjectGroup(this.interactor, npc.StartingLocation.Vector, PrimitiveBaseShape.CreateBox());
            this.visualized_npcs.Add(visualized_npc, npc);
            this.uuid_to_sog.Add(visualized_npc.UUID, visualized_npc);

            visualized_npc.RootPart.Scale = new Vector3(0.5f, 0.5f, 2f);
            visualized_npc.ScriptSetPhantomStatus(true);
            visualized_npc.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, new Vector3(255, 64, 0), null);
            visualized_npc.RootPart.SetText(npc.FirstName + " " + npc.LastName, Vector3.Zero, 1.0f);
            npc.StartingLocation.Scene.AddNewSceneObject(visualized_npc, false);
            visualized_npc.ScriptSetPhysicsStatus(true);
            this.SaveNotecard(this.NPCToNotecard(npc), visualized_npc);
        }

        private void VisualizeGraph()
        {
            lock (this.visualized_nodes)
            {
                foreach (GraphNode node in NavManager.Instance.Nodes)
                {
                    this.VisualizeNode(node);
                }
            }
        }

        private void VisualizeNode(GraphNode node) 
        {
            SceneObjectGroup visualized_node;
            if (this.visualized_nodes.ContainsValue(node))
            {
                this.visualized_nodes_reverse.TryGetValue(node, out visualized_node);
            }
            else
            {
                visualized_node = new SceneObjectGroup(this.interactor, node.Location.Vector, PrimitiveBaseShape.CreateSphere());
                visualized_node.Name = node.Name;
                this.visualized_nodes.Add(visualized_node, node);
                this.visualized_nodes_reverse.Add(node, visualized_node);
                this.uuid_to_sog.Add(visualized_node.UUID, visualized_node);


                visualized_node.RootPart.Scale = new Vector3(0.5f, 0.5f, 0.5f);
                visualized_node.ScriptSetPhantomStatus(true);
                visualized_node.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, new Vector3(0, 255, 0), null);
                node.Location.Scene.AddNewSceneObject(visualized_node, false);
                visualized_node.ScriptSetPhysicsStatus(true);
            }

            foreach (GraphNode neighbour in node.Neighbours)
            {
                this.VisualizeLink(node, neighbour);
            }
        }

        private void HighlightNode(GraphNode node, bool set_highlighted)
        {
            SceneObjectGroup sog;
            if (this.visualized_nodes_reverse.TryGetValue(node, out sog))
            {
                Vector3 colour = (set_highlighted) ? new Vector3(255, 0, 0) : new Vector3(0, 255, 0);
                sog.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, colour, null);
                sog.RootPart.SendFullUpdateToAllClients();
            }
        }

        private void VisualizeLink(GraphNode node_a, GraphNode node_b)
        {
            VisLink visual_link = new VisLink(node_a, node_b);
            VisLink visual_link1 = new VisLink(node_b, node_a);
            lock (this.visualized_links)
            {
                SceneObjectGroup visualized_link;
                Location mid_point = Location.GetMidpoint(node_a.Location, node_b.Location);

                if (this.visualized_links.Values.Contains(visual_link) || this.visualized_links.Values.Contains(visual_link1))
                {
                    this.visualized_links_reverse.TryGetValue(visual_link, out visualized_link);
                    visualized_link.UpdateGroupPosition(mid_point.Vector);
                }
                else
                {
                    visualized_link = new SceneObjectGroup(this.interactor, mid_point.Vector, PrimitiveBaseShape.CreateBox());
                    this.visualized_links.Add(visualized_link, visual_link);
                    this.visualized_links_reverse.Add(visual_link, visualized_link);
                    this.uuid_to_sog.Add(visualized_link.UUID, visualized_link);

                    visualized_link.ScriptSetPhantomStatus(true);
                    visualized_link.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, new Vector3(0, 0, 255), null);
                    mid_point.Scene.AddNewSceneObject(visualized_link, false);
                }
                visualized_link.RootPart.Scale = new Vector3(0.1f, 0.1f, Location.DistanceBetween(node_a.Location, node_b.Location));
                visualized_link.RootPart.UpdateRotation(Location.AngleBetween(node_a.Location, node_b.Location));
                visualized_link.ScheduleGroupForFullUpdate();
            }
        }

        private void DevisualizeAll()
        {
            this.DevisualizeGraph();
            this.DevisualizeNPCs();
            this.DevisualizeLightSources();

            this.visualized_npcs.Clear();
            this.visualized_nodes.Clear();
            this.visualized_nodes_reverse.Clear();
            this.visualized_links.Clear();
            this.visualized_links_reverse.Clear();
            this.visualized_light_sources.Clear();
            this.uuid_to_sog.Clear();
        }

        private void DevisualizeNPCs()
        {
            lock (this.visualized_npcs)
            {
                foreach (SceneObjectGroup visualized_npc in this.visualized_npcs.Keys)
                {
                    NPCharacter npc;
                    this.visualized_npcs.TryGetValue(visualized_npc, out npc);
                    Dictionary<String, String> notecard_contents = this.ReadNotecard(visualized_npc);
                    this.NotecardToNPC(notecard_contents, npc);

                    this.uuid_to_sog.Remove(visualized_npc.UUID);
                    this.RemoveSOG(visualized_npc);
                }
            }
        }

        private void DevisualizeGraph()
        {
            lock (this.visualized_nodes)
            {
                foreach (SceneObjectGroup visualized_node in this.visualized_nodes.Keys)
                {
                    this.uuid_to_sog.Remove(visualized_node.UUID);
                    this.RemoveSOG(visualized_node);
                    GraphNode node;
                    visualized_nodes.TryGetValue(visualized_node, out node);
                    node.Name = visualized_node.Name;
                }
            }
            lock (this.visualized_links)
            {
                foreach (SceneObjectGroup visualized_link in this.visualized_links.Keys)
                {
                    this.uuid_to_sog.Remove(visualized_link.UUID);
                    this.RemoveSOG(visualized_link);
                }
            }
        }

        private void VisualizeLightSources()
        {
            IEnumerable<LightSource> light_sources = Managers.GetLightSourceManager(this.scene).LightSources;
            lock (light_sources)
            {
                lock (this.visualized_light_sources)
                {
                    foreach (LightSource light_source in light_sources)
                    {
                        this.VisualizeLightSource(light_source);
                    }
                }
            }
        }

        private void VisualizeLightSource(LightSource light_source)
        {
            SceneObjectGroup visualized_light_source = new SceneObjectGroup(this.interactor, light_source.Location.Vector, PrimitiveBaseShape.CreateSphere());
            this.visualized_light_sources.Add(visualized_light_source, light_source);
            this.uuid_to_sog.Add(visualized_light_source.UUID, visualized_light_source);
            visualized_light_source.RootPart.Scale = new Vector3(0.1f, 0.1f, 0.1f);
            visualized_light_source.ScriptSetPhantomStatus(true);
            visualized_light_source.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, new Vector3(255, 255, 100), null);
            light_source.Location.Scene.AddNewSceneObject(visualized_light_source, false);
            visualized_light_source.ScriptSetPhysicsStatus(true);
        }

        private void DevisualizeLightSources()
        {
            lock (this.visualized_light_sources)
            {
                foreach (SceneObjectGroup visualized_light_source in this.visualized_light_sources.Keys)
                {
                    LightSource light_source;
                    this.visualized_light_sources.TryGetValue(visualized_light_source, out light_source);
                    this.uuid_to_sog.Remove(visualized_light_source.UUID);
                    //visualized_light_source.DeleteGroupFromScene(false);
                    this.RemoveSOG(visualized_light_source);
                }
            }
        }

        #endregion

        #region hooks

        private void SetHooks(bool set_hooks)
        {
            if (set_hooks)
            {
                scene.EventManager.OnObjectBeingRemovedFromScene += RemoveObjectHook;
                scene.EventManager.OnObjectAddedToScene += AddObjectHook;
                scene.EventManager.OnSceneGroupGrab += GrabObjectHook;
                scene.EventManager.OnSceneGroupMove += MoveObjectHook;
            }
            else
            {
                scene.EventManager.OnObjectBeingRemovedFromScene -= RemoveObjectHook;
                scene.EventManager.OnObjectAddedToScene -= AddObjectHook;
                scene.EventManager.OnSceneGroupGrab -= GrabObjectHook;
                scene.EventManager.OnSceneGroupMove -= MoveObjectHook;
            }
        }

        private bool MoveObjectHook(UUID uuid, Vector3 delta)
        {
            SceneObjectGroup sog;
            if (this.uuid_to_sog.TryGetValue(uuid, out sog))
            {
                if (this.visualized_npcs.ContainsKey(sog))
                {
                    NPCharacter npc;
                    this.visualized_npcs.TryGetValue(sog, out npc);
                    npc.StartingLocation.Vector = delta;
                    return true;
                }

                else if (this.visualized_nodes.ContainsKey(sog))
                {
                    GraphNode node;
                    this.visualized_nodes.TryGetValue(sog, out node);
                    node.Location.Vector = delta;
                    this.VisualizeNode(node);
                    return true;
                }

                else if (this.visualized_light_sources.ContainsKey(sog))
                {
                    LightSource light_source;
                    this.visualized_light_sources.TryGetValue(sog, out light_source);
                    light_source.Location.Vector = delta;
                    return true;
                }

                else if (this.visualized_links.ContainsKey(sog))
                {
                    return true;
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        private void RemoveObjectHook(SceneObjectGroup sog)
        {
            if (this.uuid_to_sog.TryGetValue(sog.UUID, out sog))
            {
                if (this.visualized_npcs.ContainsKey(sog))
                {
                    NPCharacter npc;
                    this.visualized_npcs.TryGetValue(sog, out npc);
                    NPCManager.Instance.RemoveNPC(npc);
                    this.visualized_npcs.Remove(sog);
                }

                else if (this.visualized_nodes.ContainsKey(sog))
                {
                    GraphNode node;
                    this.visualized_nodes.TryGetValue(sog, out node);
                    foreach (GraphNode neighbour in node.Neighbours.ToList())
                    {
                        VisLink link = new VisLink(node, neighbour);
                        SceneObjectGroup visualized_link;
                        this.visualized_links_reverse.TryGetValue(link, out visualized_link);
                        visualized_link.Scene.DeleteSceneObject(visualized_link, false);
                    }
                    NavManager.Instance.RemoveNode(node);
                    this.visualized_nodes.Remove(sog);
                    this.visualized_nodes_reverse.Remove(node);
                }

                else if (this.visualized_links.ContainsKey(sog))
                {
                    VisLink visual_link;
                    this.visualized_links.TryGetValue(sog, out visual_link);
                    NavManager.Instance.RemoveLink(visual_link.NodeA, visual_link.NodeB);
                    this.visualized_links.Remove(sog);
                    this.visualized_links_reverse.Remove(visual_link);
                }

                else if (this.visualized_light_sources.ContainsKey(sog))
                {
                    LightSource light_source;
                    this.visualized_light_sources.TryGetValue(sog, out light_source);
                    lightsource_manager.RemoveLightSource(light_source);
                    this.visualized_light_sources.Remove(sog);
                }
                this.uuid_to_sog.Remove(sog.UUID);
            }
        }

        //Object add_lock = new Object();
        private void AddObjectHook(SceneObjectGroup sog)
        {
            if (uuid_to_sog.ContainsKey(sog.UUID)) return;
            if (sog.OwnerID != this.interactor) return;
            if (sog.RootPart.Shape.ProfileShape == ProfileShape.Square)
            {
                Location location = new Location(sog.Scene, sog.AbsolutePosition);
                NPCharacter npc = new PyNPCharacter("Default", "Name", "default", "default", location, "null");
                NPCManager.Instance.AddNPC(npc);
                sog.Scene.DeleteSceneObject(sog, false);
                this.VisualizeNPC(npc);
            }
            else if (sog.RootPart.Shape.ProfileShape == ProfileShape.Circle)
            {
                Location location = new Location(sog.Scene, sog.AbsolutePosition);
                LightSource light_source = new LightSource(location);
                lightsource_manager.AddLightSource(light_source);
                sog.Scene.DeleteSceneObject(sog, false);
                this.VisualizeLightSource(light_source);
            }
            else if (sog.RootPart.Shape.ProfileShape == ProfileShape.HalfCircle)
            {
                Location location = new Location(sog.Scene, sog.AbsolutePosition);
                GraphNode new_node = new GraphNode(location);
                NavManager.Instance.AddNode(new_node);
                sog.Scene.DeleteSceneObject(sog, false);
                this.VisualizeNode(new_node);
            }
        }

        private GraphNode last_touched_node = null;
        private void GrabObjectHook(UUID uuid, Vector3 offset, UUID user_uuid)
        {
            if (offset.Length() != 0f) return;
            if (this.uuid_to_sog.ContainsKey(uuid) && this.interactor.Equals(user_uuid))
            {
                SceneObjectGroup sog;
                this.uuid_to_sog.TryGetValue(uuid, out sog);
                GraphNode node;
                if (this.visualized_nodes.TryGetValue(sog, out node))
                {
                    if (this.last_touched_node == null)
                    {
                        this.HighlightNode(node, true);
                        this.last_touched_node = node;
                    }
                    else if (this.last_touched_node == node)
                    {
                        this.HighlightNode(this.last_touched_node, false);
                        this.last_touched_node = null;
                    }
                    else
                    {
                        VisLink visual_link = new VisLink(node, this.last_touched_node);
                        if (this.visualized_links_reverse.ContainsKey(visual_link))
                        {
                            SceneObjectGroup visualized_link;
                            this.visualized_links_reverse.TryGetValue(visual_link, out visualized_link);
                            visualized_link.Scene.DeleteSceneObject(visualized_link, false);
                        }
                        else
                        {
                            NavManager.Instance.AddLink(node, this.last_touched_node);
                            this.VisualizeLink(node, this.last_touched_node);
                        }
                        this.HighlightNode(this.last_touched_node, false);
                        this.last_touched_node = null;
                    }
                }
            }
        }

        #endregion

        #region notecard manipulation

        private string notecard_name = "DATA";
        private void SaveNotecard(Dictionary<String, String> content, SceneObjectGroup sog)
        {
            string content_string = "";
            foreach (string key in content.Keys)
            {
                string value;
                content.TryGetValue(key, out value);
                content_string += key + ":" + value + "\n";
            }


            SceneObjectPart sop = sog.RootPart;

            // Create new asset
            AssetBase asset = new AssetBase(UUID.Random(), notecard_name, (sbyte)AssetType.Notecard, UUID.Random().ToString());
            asset.Description = "Do not copy, delete, or rename!";
            string data = "Linden text version 2\n{\nLLEmbeddedItems version 1\n{\ncount 0\n}\nText length " + content_string.Length.ToString() + "\n" + content_string + "}\n";

            asset.Data = Util.UTF8.GetBytes(data);
            sog.Scene.AssetService.Store(asset);

            // Create Task Entry
            TaskInventoryItem task_item = new TaskInventoryItem();

            task_item.ResetIDs(sop.UUID);
            task_item.ParentID = sop.UUID;
            task_item.CreationDate = (uint)Util.UnixTimeSinceEpoch();
            task_item.Name = asset.Name;
            task_item.Description = asset.Description;
            task_item.Type = (int)AssetType.Notecard;
            task_item.InvType = (int)InventoryType.Notecard;
            task_item.OwnerID = this.interactor;
            task_item.CreatorID = this.interactor;
            task_item.BasePermissions = (uint)OpenSim.Framework.PermissionMask.All;
			task_item.CurrentPermissions = (uint)OpenSim.Framework.PermissionMask.All;
			task_item.EveryonePermissions = (uint)OpenSim.Framework.PermissionMask.All;
			task_item.NextPermissions = (uint)OpenSim.Framework.PermissionMask.All;
            task_item.GroupID = sop.GroupID;
			task_item.GroupPermissions = (uint)OpenSim.Framework.PermissionMask.All;
            task_item.Flags = 0;
            task_item.PermsGranter = UUID.Zero;
            task_item.PermsMask = 0;
            task_item.AssetID = asset.FullID;
            sop.Inventory.AddInventoryItem(task_item, false);
        }

        private Dictionary<string, string> ReadNotecard(SceneObjectGroup sog)
        {
            List<TaskInventoryItem> task_items = sog.RootPart.Inventory.GetInventoryItems(notecard_name);
            if (task_items.Count != 1) return null;
            TaskInventoryItem notecard_task_item = task_items.First();
            AssetBase asset = sog.Scene.AssetService.Get(notecard_task_item.AssetID.ToString());
            if (asset == null) return null;
            if (asset.Type != (int) AssetType.Notecard) return null;
            string raw_data = Util.UTF8.GetString(asset.Data);
            List<string> lines = SLUtil.ParseNotecardToList(raw_data);

            Dictionary<string, string> notecard = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                string entry = line.Replace(" ", "");
                string[] entry_split = entry.Split(':');
                if (entry_split.Length == 2 && entry_split[0].Length > 0 && entry_split[1].Length > 0)
                {
                    notecard.Add(entry_split[0], entry_split[1]);
                }
            }
            return notecard;
        }

        private Dictionary<String, String> NPCToNotecard(NPCharacter npc)
        {
            Dictionary<string, string> notecard = new Dictionary<string, string>();
            notecard.Add("first_name", npc.FirstName);
            notecard.Add("last_name", npc.LastName);
            notecard.Add("appearance", npc.Appearance);
            notecard.Add("convo", npc.Convo);
            notecard.Add("script", ((PyNPCharacter)npc).ScriptName);
            return notecard;
        }

        private void NotecardToNPC(Dictionary<String, String> content, NPCharacter npc)
        {
            string first_name, last_name, appearance, script_name, convo;
            if (content.TryGetValue("first_name", out first_name)) npc.FirstName = first_name;
            if (content.TryGetValue("last_name", out last_name)) npc.LastName = last_name;
            if (content.TryGetValue("appearance", out appearance)) npc.Appearance = appearance;
            if (content.TryGetValue("convo", out convo)) npc.Convo = convo;
            if (content.TryGetValue("script", out script_name)) ((PyNPCharacter)npc).ScriptName = script_name;
        }

        #endregion

        private void RemoveSOG(SceneObjectGroup sog)
        {
            //new Thread(() => sog.Scene.DeleteSceneObject(sog, false)).Start();
            sog.Scene.DeleteSceneObject(sog, false);
        }

        private void EditCommandHandler(PCharacter character, string parameters)
        {
            if (!character.IsAdmin()) return;

            //if the command has been validly used
            if (parameters.Equals("on") || parameters.Equals("off"))
            {
                bool turn_on = (parameters == "on");

                //if the command is actually trying to *toggle* the interaction state
                if (turn_on != this.Interactable)
                {
                    if (turn_on)
                    {
                        NPCManager.Instance.DespawnAll();
                        this.StartInteraction(character.AgentId);
                    }
                    else
                    {
                        this.StopInteraction();
                        IGDRM gdrm = Managers.GetGDRM(this.scene);
                        gdrm.Save();
                        gdrm.Reload();
                    }
                    character.SendMessage("Editting has been turned " + parameters);
                }
                else
                {
                    character.SendMessage("Editting is already " + parameters);
                }
            }
            else
            {
                character.SendMessage("Command usage: /edit [on/off]");
            }
        }

        public void RegionLoaded(Scene scene)
        {
            lightsource_manager = Managers.GetLightSourceManager(this.scene);
            command_manager = Managers.GetCommandManager(this.scene);
            command_manager.RegisterCommand("edit", this.EditCommandHandler);
        }

        public void Close()
        {
        }

        public void Initialise(IConfigSource config)
        {

            Interactable = false;
        }
    }

    class VisLink
    {
        public GraphNode NodeA { get; private set; }
        public GraphNode NodeB { get; private set; }

        public VisLink(GraphNode node_a, GraphNode node_b)
        {
            this.NodeA = node_a;
            this.NodeB = node_b;
        }

        public override bool Equals(System.Object obj)
        {
            if (obj == null) return false;
            if (obj is VisLink)
            {
                VisLink link = (VisLink)obj;
                if (((link.NodeA.Equals(this.NodeA)) && (link.NodeB.Equals(this.NodeB))) || ((link.NodeA.Equals(this.NodeB)) && (link.NodeB.Equals(this.NodeA))))
                {
                    return true;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            return NodeA.GetHashCode() ^ NodeB.GetHashCode();
        }
    }
}
