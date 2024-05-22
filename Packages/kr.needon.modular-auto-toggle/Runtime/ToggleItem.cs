using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

//v1.0.68
[DisallowMultipleComponent]
[AddComponentMenu("Hirami/Toggle/ToggleItem")]
public class ToggleItem : MonoBehaviour
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