#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEngine;

namespace ToggleTool.Runtime
{
    //v1.0.71
    [DisallowMultipleComponent]
    public class ToggleItem : AvatarTagComponent
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
        public IEnumerable<SetBlendShape> BlendShapesToChange => _blendShapesToChange.Where(e => e.SkinnedMesh != null);
    }
}
#endif