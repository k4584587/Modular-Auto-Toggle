#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Hirami/Toggle/ToggleItem")]
public class ToggleItem: AvatarTagComponent
{
    public Texture2D _icon;

    [Serializable]
    public struct SetBlendShape
    {
        public SkinnedMeshRenderer SkinnedMesh;
        public string name;
        public int value;
    }

    [SerializeField] private List<SetBlendShape> _blendShapesToChange = new List<SetBlendShape>();
    [SerializeField] public GameObject targetGameObject; // SerializeField 추가
    public List<GameObject> targetGameObjects;

    public IEnumerable<SetBlendShape> BlendShapesToChange => _blendShapesToChange.Where(e => e.SkinnedMesh!= null);
}
#endif