//   WorkshopBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AT_Utils;
using Experience;

namespace GroundConstruction
{
    public class WorkshopBase : PartModule
    {
        [KSPField(isPersistant = true)] public bool Working;
        [KSPField(isPersistant = true)] public double LastUpdateTime = -1;
        [KSPField(isPersistant = true)] public double EndUT = -1;
        public string ETA_Display { get; protected set; } = "Stalled...";

        protected float workforce = 0;
        protected float max_workforce = 0;
        public string Workforce_Display
        { get { return string.Format("Workforce: {0:F1}/{1:F1} SK", workforce, max_workforce); } }

        protected virtual void update_max_workforce()
        {
            max_workforce = part.CrewCapacity*5;
        }

        protected virtual void update_workforce<T>()
            where T : ExperienceEffect
        {
            workforce = 0;
            foreach(var kerbal in part.protoModuleCrew)
            {
                var worker = 0f;
                var trait = kerbal.experienceTrait;
                foreach(var effect in trait.Effects)
                {
                    if(effect is T)
                    { worker = 1; break; }
                }
                worker *= Mathf.Max(trait.CrewMemberExperienceLevel(), 0.5f);
                workforce += worker;
            }
        }

        protected virtual void start()
        {
            Working = true;
        }

        protected virtual void stop(bool reset = false)
        {
            Working = false;
            EndUT = -1;
            ETA_Display = "";
            LastUpdateTime = -1;
            TimeWarp.SetRate(0, false);
        }

        protected double get_delta_time()
        {
            if(Time.timeSinceLevelLoad < 1 || !FlightGlobals.ready) return -1;
            if(LastUpdateTime < 0)
            {
                LastUpdateTime = Planetarium.GetUniversalTime();
                return TimeWarp.fixedDeltaTime;
            }
            var time = Planetarium.GetUniversalTime();
            var dT = time - LastUpdateTime;
            LastUpdateTime = time;
            return dT;
        }
    }
}

