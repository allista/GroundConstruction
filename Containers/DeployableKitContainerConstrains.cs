using AT_Utils;
using UnityEngine;

namespace GroundConstruction
{
    public abstract partial class DeployableKitContainer
    {
        public const string CONST_NONE = "None";
        public const string CONST_BULKHEAD = "Bulkhead";
        public const string CONST_LENGTH = "Length";
        public const string CONST_WIDTH = "Width";
        public const string CONST_HEIGHT = "Height";
        public const string CONST_WH = "Wid - Hgt";
        public const string CONST_WL = "Wid - Len";
        public const string CONST_HL = "Hgt - Len";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Constrain:")]
        [UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] {
            CONST_NONE,
            CONST_BULKHEAD,
            CONST_LENGTH,
            CONST_WIDTH,
            CONST_HEIGHT,
            CONST_WH,
            CONST_WL,
            CONST_HL
        })]
        public string ConstraintType = "None";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Bulkhead:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor,
                      intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f },
                      incrementSlide = new float[] { 0.025f },
                      sigFigs = 3, unit = "m")]
        public float ConstrainBulkhead = 1.25f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Length:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor,
                      intervals = new float[] { 0.5f, 1, 2, 4, 6, 8, 10, 12 },
                      incrementSlide = new float[] { 0.025f },
                      sigFigs = 3, unit = "m")]
        public float ConstrainLength = 1.0f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Width:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor,
                      intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f },
                      incrementSlide = new float[] { 0.025f },
                      sigFigs = 3, unit = "m")]
        public float ConstrainWidth = 1.25f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Height:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor,
                      intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f },
                      incrementSlide = new float[] { 0.025f },
                      sigFigs = 3, unit = "m")]
        public float ConstrainHeight = 1.25f;

        void setup_constraint_fields()
        {
            Fields["ConstrainLength"].OnValueModified += (obj) => set_kit_size();
            Fields["ConstrainWidth"].OnValueModified += (obj) => set_kit_size();
            Fields["ConstrainHeight"].OnValueModified += (obj) => set_kit_size();
            Fields["ConstrainBulkhead"].OnValueModified += (obj) => set_kit_size();
            Fields["ConstraintType"].OnValueModified += (obj) => { on_constraint_type_change(); set_kit_size(); };
            update_constraint_controls();
        }

        void update_constraint_controls()
        {
            Fields["ConstraintType"].guiActiveEditor = kit.Valid;
            on_constraint_type_change();
        }

        void on_constraint_type_change()
        {
            Fields["ConstrainLength"].guiActiveEditor = false;
            Fields["ConstrainWidth"].guiActiveEditor = false;
            Fields["ConstrainHeight"].guiActiveEditor = false;
            Fields["ConstrainBulkhead"].guiActiveEditor = false;
            if(kit.Valid)
            {
                switch(ConstraintType)
                {
                case CONST_BULKHEAD:
                    Fields["ConstrainBulkhead"].guiActiveEditor = true;
                    break;

                case CONST_LENGTH:
                    Fields["ConstrainLength"].guiActiveEditor = true;
                    break;

                case CONST_WIDTH:
                    Fields["ConstrainWidth"].guiActiveEditor = true;
                    break;

                case CONST_HEIGHT:
                    Fields["ConstrainHeight"].guiActiveEditor = true;
                    break;

                case CONST_WL:
                    Fields["ConstrainLength"].guiActiveEditor = true;
                    Fields["ConstrainWidth"].guiActiveEditor = true;
                    break;

                case CONST_HL:
                    Fields["ConstrainLength"].guiActiveEditor = true;
                    Fields["ConstrainHeight"].guiActiveEditor = true;
                    break;

                case CONST_WH:
                    Fields["ConstrainWidth"].guiActiveEditor = true;
                    Fields["ConstrainHeight"].guiActiveEditor = true;
                    break;
                }
            }
        }

        void set_kit_size()
        {
            if(kit.Valid)
            {
                var kitV = kit.Mass / GLB.VesselKitDensity;
                var Area = 0f;
                var SideLength = 0f;
                switch(ConstraintType)
                {
                case CONST_BULKHEAD:
                    SideLength = kitV / (ConstrainBulkhead * ConstrainBulkhead);
                    Size = new Vector3(ConstrainBulkhead, SideLength, ConstrainBulkhead);
                    break;

                case CONST_LENGTH:
                    Area = kitV / ConstrainLength;
                    SideLength = Mathf.Sqrt(Area);
                    Size = new Vector3(SideLength, ConstrainLength, SideLength);
                    break;

                case CONST_WIDTH:
                    Area = kitV / ConstrainWidth;
                    SideLength = Mathf.Sqrt(Area);
                    Size = new Vector3(ConstrainWidth, SideLength, SideLength);
                    break;

                case CONST_HEIGHT:
                    Area = kitV / ConstrainHeight;
                    SideLength = Mathf.Sqrt(Area);
                    Size = new Vector3(SideLength, SideLength, ConstrainHeight);
                    break;

                case CONST_WL:
                    SideLength = kitV / (ConstrainWidth * ConstrainLength);
                    Size = new Vector3(ConstrainWidth, ConstrainLength, SideLength);
                    break;

                case CONST_HL:
                    SideLength = kitV / (ConstrainHeight * ConstrainLength);
                    Size = new Vector3(SideLength, ConstrainLength, ConstrainHeight);
                    break;

                case CONST_WH:
                    SideLength = kitV / (ConstrainWidth * ConstrainHeight);
                    Size = new Vector3(ConstrainWidth, SideLength, ConstrainHeight);
                    break;

                default:
                    Size = OrigSize * Mathf.Pow(kitV / (OrigSize.x * OrigSize.y * OrigSize.z), 1 / 3f);
                    break;
                }
                Size = Size.ClampComponentsL(MinSize);
                update_model(false);
            }
        }
    }
}
