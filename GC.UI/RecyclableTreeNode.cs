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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GC.UI
{
    public interface IRecyclable
    {
        uint ID { get; }
        string Name { get; }
        bool Valid { get; }
        void SetDisplay(RecyclableTreeNode display_node);
        void UpdateDisplay();
        bool HasChildren { get; }
        IEnumerable<IRecyclable> GetChildren();
        void Recycle(bool discard_excess_resources, Action<bool> on_finished);
        void OnPointerEnter();
        void OnPointerExit();
    }

    public class RecyclableTreeNode : PanelledUI
    {
        private IRecyclable info;
        private readonly Dictionary<uint, RecyclableTreeNode> children = new Dictionary<uint, RecyclableTreeNode>();

        public GameObject prefab;
        public RecyclerUI ui;
        public Toggle subnodesToggle;
        public OnHoverTrigger hoverTrigger;
        public Text nodeName, assemblyResourceInfo, constructionResourceInfo, requirementsInfo;
        public Toggle discardExcess;
        public Button recycleButton;

        public RectTransform subnodes;


        public void SetRecyclableInfo(IRecyclable recyclable_info)
        {
            hide_subnodes();
            info = recyclable_info;
            recyclable_info.SetDisplay(this);
        }

        private void Awake()
        {
            subnodesToggle.onValueChanged.AddListener(toggle_submodules);
            recycleButton.onClick.AddListener(recycle);
            hoverTrigger.onPointerEnterEvent.AddListener(onPointerEnter);
            hoverTrigger.onPointerExitEvent.AddListener(onPointerExit);
        }

        private void OnDestroy()
        {
            info?.SetDisplay(null);
            subnodesToggle.onValueChanged.RemoveListener(toggle_submodules);
            recycleButton.onClick.RemoveListener(recycle);
            hoverTrigger.onPointerEnterEvent.RemoveListener(onPointerEnter);
            hoverTrigger.onPointerExitEvent.RemoveListener(onPointerExit);
        }

        private RecyclableTreeNode add_subnode(IRecyclable child_info)
        {
            if(children.TryGetValue(child_info.ID, out var node))
                return node;
            var subnodeObj = Instantiate(prefab, subnodes);
            var subnode = subnodeObj.GetComponent<RecyclableTreeNode>();
            subnode.ui = ui;
            subnode.subnodesToggle.group = null;
            subnode.subnodesToggle.SetIsOnAndColorWithoutNotify(false);
            subnode.SetRecyclableInfo(child_info);
            children[child_info.ID] = subnode;
            subnodeObj.SetActive(true);
            return subnode;
        }

        private void remove_subnode(uint ID)
        {
            if(!children.TryGetValue(ID, out var node))
                return;
            Destroy(node.gameObject);
            children.Remove(ID);
        }

        private void toggle_submodules(bool show)
        {
            if(show)
                StartCoroutine(show_subnodes());
            else
                hide_subnodes();
        }

        private IEnumerator<YieldInstruction> show_subnodes()
        {
            if(info == null)
                yield break;
            subnodesToggle.interactable = false;
            foreach(var childInfo in info.GetChildren())
            {
                add_subnode(childInfo);
                yield return null;
            }
            subnodes.gameObject.SetActive(true);
            subnodesToggle.interactable = true;
        }

        private void hide_subnodes()
        {
            children.Clear();
            subnodes.gameObject.SetActive(false);
            for(var i = subnodes.childCount - 1; i >= 0; i--)
                Destroy(subnodes.GetChild(i).gameObject);
        }

        private static IEnumerator<YieldInstruction> find_children(
            IRecyclable recyclable,
            string named_like,
            HashSet<uint> show_subnode_ids
        )
        {
            var show = recyclable.Name.ToLowerInvariant().Contains(named_like);
            foreach(var child in recyclable.GetChildren())
            {
                var child_coro = find_children(child, named_like, show_subnode_ids);
                while(child_coro.MoveNext())
                    yield return child_coro.Current;
                show |= show_subnode_ids.Contains(child.ID);
            }
            if(show)
                show_subnode_ids.Add(recyclable.ID);
            yield return null;
        }

        private IEnumerator<YieldInstruction> show_filtered_subnodes(HashSet<uint> node_ids)
        {
            hide_subnodes();
            subnodesToggle.interactable = false;
            if(info == null || !node_ids.Contains(info.ID))
                yield break;
            var activate_subnodes = false;
            foreach(var childInfo in info.GetChildren())
            {
                if(!node_ids.Contains(childInfo.ID))
                    continue;
                activate_subnodes = true;
                var subnode = add_subnode(childInfo);
                var subnode_coro = subnode.show_filtered_subnodes(node_ids);
                while(subnode_coro.MoveNext())
                    yield return subnode_coro.Current;
            }
            if(activate_subnodes)
            {
                subnodesToggle.SetIsOnAndColorWithoutNotify(true);
                subnodes.gameObject.SetActive(true);
            }
            else if(info.HasChildren)
                subnodesToggle.interactable = true;
            yield return null;
        }

        public IEnumerator<YieldInstruction> FilterSubnodes(string named_like)
        {
            if(info == null)
                yield break;
            if(string.IsNullOrEmpty(named_like))
            {
                ClearFilter();
                yield break;
            }
            var node_ids = new HashSet<uint>();
            named_like = named_like.ToLowerInvariant();
            yield return StartCoroutine(find_children(info, named_like, node_ids));
            yield return StartCoroutine(show_filtered_subnodes(node_ids));
        }

        public void ClearFilter()
        {
            info?.UpdateDisplay();
            subnodesToggle.isOn = false;
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
                    add_subnode(childInfo);
                }
                foreach(var pid in children.Keys.ToList())
                {
                    if(!ids.Contains(pid))
                        remove_subnode(pid);
                }
                if(children.Count == 0)
                    subnodesToggle.isOn = false;
            }
            else
                subnodesToggle.isOn = false;
        }

        private void on_recycled(bool success)
        {
            if(!success || this == null || gameObject == null)
                return;
            Destroy(gameObject);
        }

        private void recycle()
        {
            if(info == null)
                return;
            ui.reportPane.Clear();
            info.Recycle(discardExcess.isOn, on_recycled);
        }

        private void onPointerEnter(PointerEventData eventData)
        {
            info?.OnPointerEnter();
        }

        private void onPointerExit(PointerEventData eventData)
        {
            info?.OnPointerExit();
        }
    }
}
