using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Framework;
using System.IO;
using OpenMetaverse.StructuredData;


namespace GD
{
    class AppearanceManager
    {
        private static AppearanceManager _instance = new AppearanceManager();
        public static AppearanceManager Instance { get { return _instance; } }


        private Dictionary<string, AvatarAppearance> appearances;
        public AvatarAppearance Default { get; private set; }

        private AppearanceManager()
        {
            this.appearances = new Dictionary<string, AvatarAppearance>();
            this.SetDefault();
        }

        public bool TryGet(string appearance_name, out AvatarAppearance appearance){
            if (this.appearances.TryGetValue(appearance_name, out appearance))
            {
                return true;
            }
            else
            {
                GDRM.log.WarnFormat("[AppearanceManager]: Appearance does not exist: '{0}'. Using default.", appearance_name);
                appearance = Default;
                return false;
            }
        }

        public void SetAppearanceDirectory(string appearance_directory)
        {
            this.appearances = new Dictionary<string, AvatarAppearance>();

            if(Directory.Exists(appearance_directory)){
                int attempts = 0;
                int success = 0;
                foreach(string file_name in Directory.GetFiles(appearance_directory, "*.xml")){
                    try
                    {
                        attempts += 1;
                        if (this.LoadAppearance(file_name)) success += 1;
                    }
                    catch (Exception e)
                    {
                        GDRM.log.ErrorFormat("[AppearanceManager]: Failed to load appearance '{0}': {1}", file_name, e.Message);
                    }
                }
                GDRM.log.InfoFormat("[AppearanceManager]: {0}/{1} appearances loaded.", success, attempts);
            }
            else
            {
                GDRM.log.ErrorFormat("[AppearanceManager]: Appearance directory does not exist: '{0}'", appearance_directory);
            }
            this.SetDefault();
        }

        private bool LoadAppearance(string file_location)
        {
            AvatarAppearance appearance = new AvatarAppearance();

            //load the file then deserialise it into an appearance.
            string file_contents = File.ReadAllText(file_location);

            if (file_contents != null)
            {
                appearance.Unpack((OSDMap)OSDParser.DeserializeLLSDXml(file_contents));

                //isolate the file's name (rather than location)
                string[] path_segments = file_location.Split(Path.DirectorySeparatorChar);
                string file_name = path_segments.Last();
                //remove .xml
                string appearance_name = file_name.Substring(0, file_name.Length - 4);

                appearances.Add(appearance_name, appearance);
                GDRM.log.DebugFormat("[AppearanceManager]: Appearance loaded: '{0}' as '{1}'", file_location, appearance_name);
                return true;
            }
            else
            {
                GDRM.log.ErrorFormat("[AppearanceManager]: Failed to load appearance '{0}': File is empty", file_location);
                return false;
            }
        }
        
        private void SetDefault()
        {
            AvatarAppearance default_appearance;
            if (!appearances.TryGetValue("default", out default_appearance))
            {
                default_appearance = new AvatarAppearance();
            }
            Default = default_appearance;
        }
    }
}
