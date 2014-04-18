using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;

using Nini.Config;
using GD.Interfaces;

using Mono.Addins;

//[assembly: Addin("GroupDRegionModule", "0.1")]
//[assembly: AddinDependency("OpenSim", "0.5")]

namespace GD.Time
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LightSourceManager")]
    class LightSourceManager : ILightSourceManager, INonSharedRegionModule
    {
        public string Name { get { return "LightSourceManager"; } }
        public Type ReplaceableInterface { get { return null; } }

        public IEnumerable<LightSource> LightSources { get { return this.light_sources; } }
        private HashSet<LightSource> light_sources = new HashSet<LightSource>();

        private HashSet<Scene> scenes = new HashSet<Scene>();

        private bool lights_on = false;


        private Scene scene;
        public void AddRegion(Scene scene)
        {
            Managers.SetLightSourceManager(scene, this);
            this.scene = scene;
        }

        public void RemoveRegion(Scene scene)
        {
            this.scene = null;
        }

        public void AddLightSource(LightSource light_source)
        {
            lock (this.light_sources)
            {
                this.light_sources.Add(light_source);
                light_source.TurnLightOn(this.lights_on);
            }
        }

        public void RemoveLightSource(LightSource light_source)
        {
            lock (this.light_sources)
            {
                light_source.Destroy();
                this.light_sources.Remove(light_source);
            }
        }

        public void TurnLightsOn(bool turn_on)
        {
            lock (this.light_sources)
            {
                foreach (LightSource light_source in this.light_sources)
                {
                    light_source.TurnLightOn(turn_on);
                }
            }
            this.lights_on = turn_on;
        }

        private void TimeChanged(DateTime now)
        {
            if (this.lights_on && now.Hour < 20 && now.Hour >= 8)
            {
                GDRM.log.DebugFormat("[LightSourceManager]: Region '{0}'. {1} lights turned off", this.scene.RegionInfo.RegionName, this.light_sources.Count);
                this.TurnLightsOn(false);
            }
            else if (!this.lights_on && !(now.Hour < 20 && now.Hour >= 8))
            {
                GDRM.log.DebugFormat("[LightSourceManager]: Region '{0}'. {1} lights turned on", this.scene.RegionInfo.RegionName, this.light_sources.Count);
                this.TurnLightsOn(true);
            }
            this.UpdateSun(now);
        }

        private void UpdateSun(DateTime now)
        {
            double minutes_into_day = now.Minute + now.Hour * 60;
            double minutes_in_full_day = 24 * 60;
            double hours_through_day = (minutes_into_day / minutes_in_full_day) * 24;
            lock (this.scenes)
            {
                foreach (Scene scene in this.scenes)
                {
					scene.RegionInfo.RegionSettings.UseEstateSun = true;
					scene.RegionInfo.RegionSettings.SunPosition = ((float)hours_through_day - 6);
					scene.RegionInfo.RegionSettings.FixedSun = true;
					scene.RegionInfo.RegionSettings.Save();
                    scene.EventManager.TriggerEstateToolsSunUpdate(scene.RegionInfo.RegionHandle);
                }
            }
        }

        public void Clear()
        {
            foreach (LightSource light_source in this.light_sources.ToList())
            {
                this.RemoveLightSource(light_source);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            ITimeManager time_manager = Managers.GetTimeManager(this.scene);
            time_manager.OnTimeChange += this.TimeChanged;
        }

        public void Close()
        {
        }

        public void Initialise(IConfigSource config)
        {
        }
    }
}