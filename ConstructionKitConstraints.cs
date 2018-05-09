using AT_Utils;
using System;
using UnityEngine;

namespace GroundConstruction
{
    public partial class ModuleConstructionKit
    {
        [KSPField(isPersistant = true)]
        public string KitType = "Box";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Constrain:")]
        [UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] { "None", "Length", "Width", "Height", "Len + Wid", "Len + Hgt", "Wid + Hgt" })]
        public string ConstrainBox = "None";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Constrain:")]
        [UI_ChooseOption(scene = UI_Scene.Editor, options = new string[] { "None", "Diameter", "Height" })]
        public string ConstrainCylinder = "None";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Length:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor, intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f }, incrementSlide = new float[] { 0.025f }, sigFigs = 3, unit = "m")]
        public float ConstrainLength = 1.6f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Width:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor, intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f }, incrementSlide = new float[] { 0.025f }, sigFigs = 3, unit = "m")]
        public float ConstrainWidth = 1.6f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Diameter:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor, intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f }, incrementSlide = new float[] { 0.025f }, sigFigs = 3, unit = "m")]
        public float ConstrainDiameter = 1.6f;

        [KSPField(isPersistant = true, guiActiveEditor = false, guiName = "Height:")]
        [UI_ScaleEdit(scene = UI_Scene.Editor, intervals = new float[] { 0.625f, 1.25f, 2.5f, 3.75f, 5.0f, 7.5f }, incrementSlide = new float[] { 0.025f }, sigFigs = 3, unit = "m")]
        public float ConstrainHeight = 1.25f;

        public void OnStartConstraints(StartState state)
        {
            Fields["ConstrainBox"].OnValueModified += (obj) => OnKitConstraintsChanged();
            Fields["ConstrainCylinder"].OnValueModified += (obj) => OnKitConstraintsChanged();
            Fields["ConstrainLength"].OnValueModified += (obj) => SetKitSize();
            Fields["ConstrainWidth"].OnValueModified += (obj) => SetKitSize();
            Fields["ConstrainDiameter"].OnValueModified += (obj) => SetKitSize();
            Fields["ConstrainHeight"].OnValueModified += (obj) => SetKitSize();

            OnKitTypeChanged();
        }

        public void OnKitTypeChanged()
        {
            switch (KitType)
            {
                case "Box":
                    Fields["ConstrainBox"].guiActiveEditor = kit.Valid;
                    Fields["ConstrainCylinder"].guiActiveEditor = false;
                    OnKitConstraintsChanged();
                    break;

                case "Cylinder":
                    Fields["ConstrainBox"].guiActiveEditor = false;
                    Fields["ConstrainCylinder"].guiActiveEditor = kit.Valid;
                    OnKitConstraintsChanged();
                    break;
            }
        }

        public void OnKitConstraintsChanged()
        {
            string Constraint = KitType == "Box" ? ConstrainBox : ConstrainCylinder;

            Fields["ConstrainLength"].guiActiveEditor = false;
            Fields["ConstrainWidth"].guiActiveEditor = false;
            Fields["ConstrainDiameter"].guiActiveEditor = false;
            Fields["ConstrainHeight"].guiActiveEditor = false;

            if (kit.Valid)
            {
                switch (Constraint)
                {
                    case "Length":
                        Fields["ConstrainLength"].guiActiveEditor = true;
                        break;

                    case "Width":
                        Fields["ConstrainWidth"].guiActiveEditor = true;
                        break;

                    case "Height":
                        Fields["ConstrainHeight"].guiActiveEditor = true;
                        break;

                    case "Diameter":
                        Fields["ConstrainDiameter"].guiActiveEditor = true;
                        break;

                    case "Len + Wid":
                        Fields["ConstrainLength"].guiActiveEditor = true;
                        Fields["ConstrainWidth"].guiActiveEditor = true;
                        break;

                    case "Len + Hgt":
                        Fields["ConstrainLength"].guiActiveEditor = true;
                        Fields["ConstrainHeight"].guiActiveEditor = true;
                        break;

                    case "Wid + Hgt":
                        Fields["ConstrainWidth"].guiActiveEditor = true;
                        Fields["ConstrainHeight"].guiActiveEditor = true;
                        break;
                }

                SetKitSize();
            }
        }

        public void SetKitSize()
        {
            if (kit.Valid)
            {
                var kitV = kit.Mass / GLB.VesselKitDensity;
                var Constraint = KitType == "Box" ? ConstrainBox : ConstrainCylinder;
                var Area = 0f;
                var SideLength = 0f;

                switch (Constraint)
                {
                    case "Length":
                        Area = kitV / ConstrainLength;
                        SideLength = (float)Math.Sqrt(Area);
                        Size = new Vector3(ConstrainLength, SideLength, SideLength);
                        break;

                    case "Width":
                        Area = kitV / ConstrainWidth;
                        SideLength = (float)Math.Sqrt(Area);
                        Size = new Vector3(SideLength, SideLength, ConstrainWidth);
                        break;

                    case "Height":
                        Area = kitV / ConstrainHeight;
                        SideLength = (float)(KitType == "Box" ? Math.Sqrt(Area) : Math.Sqrt(Area / Math.PI) * 2.0f);
                        Size = new Vector3(SideLength, ConstrainHeight, SideLength);
                        break;

                    case "Diameter":
                        SideLength = kitV / (float)(Math.PI * Math.Pow(ConstrainDiameter / 2f, 2));
                        Size = new Vector3(ConstrainDiameter, SideLength, ConstrainDiameter);
                        break;

                    case "Len + Wid":
                        SideLength = kitV / (ConstrainLength * ConstrainWidth);
                        Size = new Vector3(ConstrainLength, SideLength, ConstrainWidth);
                        break;

                    case "Len + Hgt":
                        SideLength = kitV / (ConstrainLength * ConstrainHeight);
                        Size = new Vector3(ConstrainLength, ConstrainHeight, SideLength);
                        break;

                    case "Wid + Hgt":
                        SideLength = kitV / (ConstrainWidth * ConstrainWidth);
                        Size = new Vector3(SideLength, ConstrainHeight, ConstrainWidth);
                        break;

                    default:
                        Size = OrigSize * Mathf.Pow(kitV / (OrigSize.x * OrigSize.y * OrigSize.z), 1 / 3f);
                        break;
                }

                Size = Size.ClampComponentsL(GLB.VesselKitMinSize);

                update_model(false);
            }
        }
    }
}
