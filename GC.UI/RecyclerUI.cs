//   RecyclerUI.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2019 Allis Tauri

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AT_Utils.UI;

namespace GC.UI
{
    public class RecyclerUI : ScreenBoundRect
    {
        public float filterDelay = 0.5f;

        public RectTransform rootsContainer;

        public ToggleGroup rootToggles;

        public GameObject recyclableTreeNodePrefab;

        public ReportPane reportPane;

        public Button closeButton;

        public InputField filterInput;
        public Button clearFilterButton;
        public Colorizer filterColorizer;

        readonly Dictionary<uint, RecyclableTreeNode> rootNodes =
            new Dictionary<uint, RecyclableTreeNode>();

        protected override void Awake()
        {
            base.Awake();
            reportPane.SetActive(false);
            filterInput.onValueChanged.AddListener(on_filter_value_change);
            filterInput.onEndEdit.AddListener(on_filter_submit);
            clearFilterButton.onClick.AddListener(clear_filter);
        }

        private void OnDestroy()
        {
            filterInput.onValueChanged.RemoveListener(on_filter_value_change);
            filterInput.onEndEdit.RemoveListener(on_filter_submit);
            clearFilterButton.onClick.RemoveListener(clear_filter);
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

        private Coroutine nodes_filter;
        private string filter_string = "";

        private IEnumerator<object> filter_nodes(string named_like, float delay)
        {
            if(rootNodes.Count == 0)
                yield break;
            if(delay > 0)
                yield return new WaitForSecondsRealtime(delay);
            filterInput.textComponent.color = Colors.Active;
            foreach(var node in rootNodes)
            {
                var root_coro = node.Value.FilterSubnodes(named_like);
                while(root_coro.MoveNext())
                    yield return root_coro.Current;
            }
            filterInput.textComponent.color = Colors.Enabled;
            nodes_filter = null;
        }

        private void delayed_filter_nodes(string named_like, float delay)
        {
            if(named_like.Equals(filter_string, StringComparison.InvariantCultureIgnoreCase))
                return;
            filter_string = named_like;
            filterInput.textComponent.color = Colors.Neutral;
            if(nodes_filter != null)
                StopCoroutine(nodes_filter);
            nodes_filter = StartCoroutine(filter_nodes(named_like, delay));
        }

        private void on_filter_value_change(string new_filter_string) =>
            delayed_filter_nodes(new_filter_string, filterDelay);

        private void on_filter_submit(string new_filter_string) =>
            delayed_filter_nodes(new_filter_string, -1);

        private void clear_filter()
        {
            filter_string = "";
            filterInput.SetTextWithoutNotify("");
            filterInput.textComponent.color = Colors.Neutral;
            if(nodes_filter != null)
                StopCoroutine(nodes_filter);
            foreach(var node in rootNodes)
                node.Value.ClearFilter();
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
