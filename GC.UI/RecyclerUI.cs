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
        public RectTransform rootsContainer;

        public ToggleGroup rootToggles;

        public GameObject recyclableTreeNodePrefab;

        public ReportPane reportPane;

        readonly Dictionary<uint, RecyclableTreeNode> rootNodes =
            new Dictionary<uint, RecyclableTreeNode>();

        protected override void Awake()
        {
            base.Awake();
            reportPane.SetActive(false);
        }

        public void AddRoot(IRecyclable rootInfo)
        {
            if(!rootNodes.ContainsKey(rootInfo.ID))
            {
                var newNodeObj = Instantiate(recyclableTreeNodePrefab, rootsContainer);
                var newNode = newNodeObj.GetComponent<RecyclableTreeNode>();
                newNode.ui = this;
                newNode.SetRecyclableInfo(rootInfo);
                newNode.subnodesToggle.group = rootToggles;
                newNodeObj.SetActive(true);
                rootNodes.Add(rootInfo.ID, newNode);
            }
        }

        void delete_node(RecyclableTreeNode node)
        {
            GameObject obj;
            (obj = node.gameObject).SetActive(false);
            Destroy(obj);
        }

        public void DeleteRoot(uint root_part_id)
        {
            if(rootNodes.TryGetValue(root_part_id, out var node))
            {
                delete_node(node);
                rootNodes.Remove(root_part_id);
            }
        }

        public void Clear()
        {
            foreach(var node in rootNodes.Values)
                delete_node(node);
            rootNodes.Clear();
        }
    }

    public class ReportPane : PanelledUI
    {
        public Text content;

        public void SetReport(string[] messages)
        {
            if(messages != null && messages.Length > 0)
            {
                content.text = string.Join("\n", messages);
                SetActive(true);
            }
            else
                Clear();
        }

        public void Clear()
        {
            SetActive(false);
            content.text = "";
        }
    }
}
