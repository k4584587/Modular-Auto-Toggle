#if UNITY_EDITOR
using nadena.dev.modular_avatar.core;
using UnityEngine;

//v1.0.71
namespace Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hirami/Toggle/DeleteToggle")]
    public class DeleteToggle : AvatarTagComponent
    {
        public Texture2D _icon;
        
    }
}
#endif