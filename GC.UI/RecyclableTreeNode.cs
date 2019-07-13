//   PartTreeNode.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2019 Allis Tauri

using System;
using System.Collections.Generic;
using System.Linq;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GC.UI
{
    public interface IRecyclable
    {
        uint ID { get; }
        bool Valid { get; }
        void SetDisplay(RecyclableTreeNode display_node);
        IEnumerable<IRecyclable> GetChildren();
        void Recycle(bool discard_excess_resources, Action<bool> on_finished);
    }

    public class RecyclableTreeNode : PanelledUI
    {
        private IRecyclable info;
        private Dictionary<uint, GameObject> children = new Dictionary<uint, GameObject>();

        public RecyclerUI ui;
        public Toggle subnodesToggle;
        public Text nodeName, assemblyResourceInfo, constructionResourceInfo, requirementsInfo;
        public Toggle discardExcess;
        public Button recycleButton;

        public RectTransform subnodes;


        public void SetRecyclableInfo(IRecyclable recyclable_info)
        {
            show_subnodes(false);
            info = recyclable_info;
            recyclable_info.SetDisplay(this);
        }

        private void Awake()
        {
            subnodesToggle.onValueChanged.AddListener(show_subnodes);
            recycleButton.onClick.AddListener(recycle);
        }

        private void OnDestroy()
        {
            info?.SetDisplay(null);
        }

        private void add_subnode(IRecyclable child_info)
        {
            var subnodeObj = Instantiate(gameObject, subnodes);
            var subnode = subnodeObj.GetComponent<RecyclableTreeNode>();
            subnode.ui = ui;
            subnode.subnodesToggle.group = null;
            subnode.subnodesToggle.SetIsOnWithoutNotify(false);
            subnode.SetRecyclableInfo(child_info);
            children[child_info.ID] = subnodeObj;
            subnodeObj.SetActive(true);
        }

        private void show_subnodes(bool show)
        {
            if(show)
            {
                if(info != null)
                {
                    foreach(var childInfo in info.GetChildren())
                        add_subnode(childInfo);
                    subnodes.gameObject.SetActive(true);
                }
            }
            else
            {
                children.Clear();
                subnodes.gameObject.SetActive(false);
                for(var i = subnodes.childCount - 1; i >= 0; i--)
                    Destroy(subnodes.GetChild(i).gameObject);
            }
        }

        public void RefreshSubnodes()
        {
            if(!subnodes.gameObject.activeInHierarchy)
                return;
            if(info != null)
            {
                var ids = new HashSet<uint>();
                foreach(var childInfo in info.GetChildren())
                {
                    ids.Add(childInfo.ID);
                    if(!children.ContainsKey(childInfo.ID))
                        add_subnode(childInfo);
                }
                foreach(var pid in children.Keys.ToList())
                {
                    if(!ids.Contains(pid))
                        Destroy(children[pid]);
                }
                if(children.Count == 0)
                    subnodesToggle.isOn = false;
            }
            else
                subnodesToggle.isOn = false;
        }

        private void on_recycled(bool success)
        {
            if(success)
                Destroy(gameObject);
        }

        private void recycle()
        {
            if(info != null)
            {
                ui.reportPane.Clear();
                info.Recycle(discardExcess.isOn, on_recycled);
            }
        }
    }
}
