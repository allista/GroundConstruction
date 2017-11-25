//   Work.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using AT_Utils;

namespace GroundConstruction
{
    /// <summary>
    /// Base abstract class for any work.
    /// </summary>
    public abstract class Work : ConfigNodeObject
    {
        [Persistent] public double TotalWork;
        public abstract void SetComplete(bool complete);
    }
}

