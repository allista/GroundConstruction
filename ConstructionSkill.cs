//   PartConstructionSkill.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2017 Allis Tauri

using Experience;

namespace GroundConstruction
{
    public class ConstructionSkill : ExperienceEffect
    {
        public ConstructionSkill(ExperienceTrait parent) : base(parent) {}
        public ConstructionSkill(ExperienceTrait parent, float[] modifiers) : base(parent, modifiers) {}
        protected override float GetDefaultValue() { return 0f; }
        protected override string GetDescription()
        {
            return "Construct ships in the field";
        }
    }
}
