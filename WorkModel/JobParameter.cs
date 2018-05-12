//   JobParameter.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri
using AT_Utils;

namespace GroundConstruction
{
    public class JobParameter : ConfigNodeObject
    {
        [Persistent] public float Value;
        [Persistent] public FloatCurve Curve = new FloatCurve();

        public float Min => Curve.Curve.keys[0].value;
        public float Max => Curve.Curve.keys[Curve.Curve.length - 1].value;

        public JobParameter()
        {
            Curve.Add(0, 0, 0, 0);
        }

        public void Add(float frac, float value) => Curve.Add(frac, value, 0, 0);

        public void Update(float fraction) => Value = Curve.Evaluate(fraction);

        public static implicit operator float(JobParameter param) => param.Value;
    }
}