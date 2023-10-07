using System;
using System.Timers;

namespace H3MP.src
{
    /// <summary>
    /// GRANDFATHER CLASS
    /// This class is intended to keep time outside the game's tick system, on its own thread.
    /// This is because Time.* from Unity sucks.
    /// </summary>
    static class Grandfather
    {
        public static Timer clockWork;
        public static event Action ClockTick;      // Event fires every 20ms by default. Set by server tickrate.
        public static event Action ClockTock;      // Event fires *roughly* every half-second. How close depends on tickrate.
        public static event Action ClockBell;      // Event fires every second.
        public static ulong clockTime = 0;         // Server: Milliseconds after init.
        public static ulong epochTime = 0;         // Server: The unix timestamp for when the timer starts.
        public static byte subSecondCounter = 0;   // A counter for tickrate/second.
        private static int tickRate = 50;           // The tick rate. Defaults to 50hz.
        /// <summary>
        /// InitClock
        /// This function initializes the base clockTick timer and sets epoch time.
        /// </summary>
        public static void InitClock()
        {
            Timer clockWork = new Timer() // 50hz clock ticks. TODO: Add config reference to set tickrate.
            {
                Interval = 20.00,
                AutoReset = true
            };
            clockWork.Enabled = true;
            clockWork.Elapsed += OnTick;
            epochTime = (ulong)DateTime.UtcNow.Subtract(new DateTime(1970,1,1)).Milliseconds; // Unix epoch time for when the server started. Can be overwritten on clients.
        }
        /// <summary>
        /// This function handles the main timekeeping logic - 
        /// </summary>
        /// <param name="clockTick"></param>
        /// <param name="e"></param>
        private static void OnTick(object clockTick, ElapsedEventArgs e)
        {
            clockTime += 20;
            subSecondCounter += 1;
            ClockTick();
            if (subSecondCounter >= tickRate) //creates an event every 1 second.
            {
                subSecondCounter = 0;
                ClockBell();
            }
            if (subSecondCounter == 1 || subSecondCounter == (int)tickRate/2) //Triggers on 0 and (roughly!) 500ms. 
            {
                ClockTock();
            }
        }

    }
}
