//   RecyclerUI.cs
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
    public class RecyclerUI : ScreenBoundRect
    {
        public RectTransform rootPartsContainer;
        public ToggleGroup rootPartToggles;
        public GameObject partTreeNodePrefab;

        readonly Dictionary<uint, PartTreeNode> rootNodes = new Dictionary<uint, PartTreeNode>();

        public void AddRootPart(IRecycleInfo rootPartInfo)
        {
            if(!rootNodes.ContainsKey(rootPartInfo.ID))
            {
                var newNodeObj = Object.Instantiate(partTreeNodePrefab, rootPartsContainer);
                var newNode = newNodeObj.GetComponent<PartTreeNode>();
                newNode.SetPartInfo(rootPartInfo);
                newNode.subnodesToggle.group = rootPartToggles;
                newNodeObj.SetActive(true);
            }
        }

        public void DeleteRootPart(IRecycleInfo rootPartInfo)
        {
            PartTreeNode node;
            if(rootNodes.TryGetValue(rootPartInfo.ID, out node))
            {
                node.gameObject.SetActive(false);
                Object.Destroy(node.gameObject);
            }
        }
    }
}
