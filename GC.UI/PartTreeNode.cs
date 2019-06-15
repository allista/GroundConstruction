//   PartTreeNode.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2019 Allis Tauri
using System.Collections.Generic;
using AT_Utils.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GC.UI
{
    public interface IRecycleInfo
    {
        uint ID { get; }
        IEnumerable<IRecycleInfo> GetChildren();
        void Update(PartTreeNode display);
    }

    public class PartTreeNode : PanelledUI
    {
        IRecycleInfo partInfo;

        public Toggle subnodesToggle;
        public Text assemblyResourceInfo, constructionResourceInfo, requirementsInfo;
        public Toggle discardExcess;
        public Button recycleButton;

        public GameObject partTreeNodePrefab;
        public RectTransform subnodes;

        public void SetPartInfo(IRecycleInfo info)
        {
            showSubnodes(false);
            partInfo = info;
            partInfo.Update(this);
        }

        void Awake()
        {
            subnodesToggle.onValueChanged.AddListener(showSubnodes);
        }

        void showSubnodes(bool show)
        {
            if(show)
            {
                if(partInfo != null)
                {
                    foreach(var childInfo in partInfo.GetChildren())
                    {
                        var subnode = Object.Instantiate(partTreeNodePrefab, subnodes);
                        subnode.GetComponent<PartTreeNode>().SetPartInfo(childInfo);
                        subnode.SetActive(true);
                    }
                    subnodes.gameObject.SetActive(true);
                }
            }
            else
            {
                subnodes.gameObject.SetActive(false);
                for(var i = subnodes.childCount-1; i >= 0; i++)
                    Object.Destroy(subnodes.GetChild(i).gameObject);
            }
        }
    }
}
