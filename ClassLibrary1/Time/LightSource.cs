using System;
using System.Collections.Generic;
using System.Threading;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using OpenSim.Framework;

namespace GD.Time
{
    class LightSource
    {
        public Location Location {get; set;}
        private SceneObjectGroup light_representation;
        private Vector3 color = new Vector3(255,255,100);

        public LightSource(Location location)
        {
            this.Location = location;
            this.light_representation = new SceneObjectGroup(UUID.Zero, location.Vector, PrimitiveBaseShape.CreateSphere());
            this.light_representation.RootPart.Scale = new Vector3(0.01f, 0.01f, 0.01f);
            this.light_representation.ScriptSetPhantomStatus(true);
            this.light_representation.RootPart.SetFaceColorAlpha(SceneObjectPart.ALL_SIDES, new Vector3(255, 255, 100), 0.0);

            SceneObjectPart part = this.light_representation.RootPart;
            part.Shape.LightColorR = Util.Clip((float)color.X/255.0f, 0.0f, 1.0f);
            part.Shape.LightColorG = Util.Clip((float)color.Y/255.0f, 0.0f, 1.0f);
            part.Shape.LightColorB = Util.Clip((float)color.Z/255.0f, 0.0f, 1.0f);
            part.Shape.LightColorA = Util.Clamp((float)0, 0.0f, 1.0f);
            part.Shape.LightIntensity = 0.25f;
            part.Shape.LightRadius = 4.0f;
            part.Shape.LightFalloff = 2.0f;
            this.Location.Scene.AddNewSceneObject(this.light_representation, false);
            this.light_representation.ScheduleGroupForFullUpdate();
        }

        public void Destroy()
        {
            this.TurnLightOn(false);
            new Thread(() => this.light_representation.Scene.DeleteSceneObject(this.light_representation, false)).Start();
        }

        public void TurnLightOn(bool turn_on)
        {
            this.light_representation.RootPart.Shape.LightEntry = turn_on;
            this.light_representation.RootPart.ScheduleFullUpdate();
            if (turn_on)
            {
                this.light_representation.RootPart.AddNewParticleSystem(this.DefaultParticleSystem());
            }
            else
            {
                this.light_representation.RootPart.RemoveParticleSystem();
            }
            this.light_representation.RootPart.ParentGroup.HasGroupChanged = true;
            this.light_representation.RootPart.SendFullUpdateToAllClients();
        }



        private Primitive.ParticleSystem DefaultParticleSystem()
        {
            Primitive.ParticleSystem ps = new Primitive.ParticleSystem();
            ps.PartStartScaleX = 0.07f;
            ps.PartStartScaleY = 0.2f;
            ps.PartEndScaleX = 0.05f;
            ps.PartEndScaleY = 0.2f;

            ps.PartStartColor.R = 1.0f;
            ps.PartStartColor.G = 1.0f;
            ps.PartStartColor.B = 0.0f;
            ps.PartStartColor.A = 0.8f;

            ps.PartEndColor.R = 1.0f;
            ps.PartEndColor.G = 0.5f;
            ps.PartEndColor.B = 0.0f;
            ps.PartEndColor.A = 0.0f;

            ps.BurstPartCount = 8;
            ps.BurstRate = 0.12f;
            ps.PartMaxAge = 0.50f;
            ps.MaxAge = 0.0f;

            ps.Pattern = (Primitive.ParticleSystem.SourcePattern)8;
            ps.PartAcceleration.X = 0.0f;
            ps.PartAcceleration.Y = 0.0f;
            ps.PartAcceleration.Z = 0.5f;

            ps.BurstSpeedMin = 0.01f;
            ps.BurstSpeedMax = 0.1f;

            ps.InnerAngle = 1.0f * 0.017453292519943f;
            uint pdf = 0;
            pdf |= 1;
            pdf |= 2;
            pdf |= 256;
            pdf |= 32;
            pdf |= 8;
            pdf |= 4;
            ps.PartDataFlags = (Primitive.ParticleSystem.ParticleDataFlags)pdf;

            ps.CRC = 1;

            return ps;
        }
    }
}
