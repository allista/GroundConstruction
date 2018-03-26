//   JobBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

namespace GroundConstruction
{
    public abstract class JobBase : WorkBase
    {
        protected static Globals GLB { get { return Globals.Instance; } }

        [Persistent] public string Name;
        [Persistent] public int CurrentIndex = -1;

        public virtual bool Valid { get { return CurrentIndex >= 0; } }

        public abstract int StagesCount { get; }

        public abstract void NextStage();

        public abstract void SetStageComplete(int stage, bool complete);

        public static implicit operator bool(JobBase job)
        {
            return job != null && job.Valid;
        }
    }
}

