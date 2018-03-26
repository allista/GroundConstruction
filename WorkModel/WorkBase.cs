//   WorkBase.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using AT_Utils;

namespace GroundConstruction
{
    public abstract class WorkBase : ConfigNodeObject
    {
        public abstract bool Complete { get; }

        public abstract void SetComplete(bool complete);

        public abstract double DoSomeWork(double work);

        public abstract double WorkLeft { get; }
    }
}

