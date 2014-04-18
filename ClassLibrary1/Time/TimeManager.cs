using System;
using System.Threading;
using GD.Command;
using OpenSim.Region.Framework.Interfaces;
using GD.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Nini.Config;

using Mono.Addins;

namespace GD.Time
{
    public delegate void TimeUpdate(DateTime new_time);

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "TimeManager")]
    public class TimeManager : INonSharedRegionModule, ITimeManager
    {
        public string Name { get { return "TimeManager"; } }
        public Type ReplaceableInterface { get { return null; } }

        public DateTime Time { get { return this.GetTime(); } set { this.SetTime(value); } }

        public event TimeUpdate OnTimeChange;

        private Timer timer = null;
        private int time_offset = 0;
        private int hours_per_sim_day = 8;


        //converts a time in the format 00:00 to 23:59 into a datatime object
        public static DateTime GetDateTimeByString(string time_string)
        {
            string[] time_string_split = time_string.Split(':');
            int hours, minutes;
            if (time_string_split.Length == 2 && int.TryParse(time_string_split[0], out hours) && int.TryParse(time_string_split[1], out minutes))
            {
                if (hours >= 0 && hours < 24 && minutes >= 0 && minutes < 60)
                {
                    return new DateTime(2000, 01, 01, hours, minutes, 0);
                }
            }
            throw new Exception("Invalid date. Must be in format: 00:00 to 23:59");
        }

        //updates the timer in order to sync with the current opensim time (eg after the time offset has been changed).
        private void SetTimer()
        {
            if (this.timer != null) this.timer.Dispose();
            //int milliseconds_in_sim_minute = ((hours_per_sim_day * 60) / 24) * 1000;
			int milliseconds_in_sim_second = ((hours_per_sim_day * 1000) / 24);
            this.timer = new Timer(UpdateTime, null, milliseconds_in_sim_second + 1000, milliseconds_in_sim_second);
        }

        //gets called every opensim minute to inform all 'time-change' listeners
        private void UpdateTime(object param)
        {
            if(this.OnTimeChange != null) this.OnTimeChange(this.GetTime());
        }

        private DateTime GetTime()
        {
            DateTime now = DateTime.Now;
            int now_int = (now.Hour * 60 * 60) + (now.Minute * 60) + now.Second;
            int sim_int = ((now_int % (60 * 60 * hours_per_sim_day)) * (24 / hours_per_sim_day) + time_offset) % (24 * 60 * 60);
            return new DateTime(2000, 01, 01, ((int)(sim_int / (60 * 60))) % 24, ((sim_int % (60 * 60)) / 60) % 60, sim_int % (60));
        }

        private void SetTime(DateTime new_time)
        {
            time_offset = 0;
            DateTime now = GetTime();
            int now_int = (now.Hour * 60 * 60) + (now.Minute * 60) + now.Second;
            int new_time_int = (new_time.Hour * 60 * 60) + (new_time.Minute * 60) + new_time.Second;
            time_offset = PosMod(new_time_int - now_int, 24 * 60 * 60);
            this.SetTimer();
            this.UpdateTime(null);
        }

        //gets the modulus of a number, but ensures the value is always positive (unlike the build in % function :( )
        private int PosMod(int value, int mod)
        {
            int new_value = value % mod;
            if (new_value < 0)
            {
                return new_value + mod;
            }
            else
            {
                return new_value;
            }
        }

        private void TimeCommandHandler(PCharacter character, string parameters)
        {
            if (!character.IsAdmin()) return;

            if (parameters == null || parameters.Equals(""))
            {
                DateTime now = this.Time;
                character.SendMessage("The time is: " + now.ToShortTimeString());
            }
            else
            {
                try
                {
                    this.Time = TimeManager.GetDateTimeByString(parameters);
                }
                catch (Exception e)
                {
                    character.SendMessage(e.Message);
                }
            }
        }


        public void RemoveRegion(Scene scene)
        {
        }


        public void AddRegion(Scene scene)
        {
            Managers.SetTimeManager(scene, this);
        }

        public void RegionLoaded(Scene scene)
        {
            ICommandManager command_manager = Managers.GetCommandManager(scene);
            command_manager.RegisterCommand("time", this.TimeCommandHandler);
            this.SetTimer();
        }

        public void Close()
        {
        }

        public void Initialise(IConfigSource config)
        {
        }
    }
}
