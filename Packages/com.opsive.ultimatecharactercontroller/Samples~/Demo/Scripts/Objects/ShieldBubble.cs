/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Demo.Objects
{
    using UnityEngine;

    /// <summary>
    /// The ShieldBubble will play an enlarging animation when the object spawns.
    /// </summary>
    public class ShieldBubble : MonoBehaviour
    {
        private Transform m_Transform;
        private Animation m_Animation;
        
        private Vector3 m_Scale;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        private void Awake()
        {
            m_Transform = GetComponent<Transform>();
            m_Animation = GetComponent<Animation>();
            
            m_Scale = m_Transform.localScale;
        }

        /// <summary>
        /// Reset the changed values.
        /// </summary>
        private void OnEnable()
        {
            m_Animation.Rewind();
            m_Animation.Play();
            m_Transform.localScale = m_Scale;
        }
    }
}