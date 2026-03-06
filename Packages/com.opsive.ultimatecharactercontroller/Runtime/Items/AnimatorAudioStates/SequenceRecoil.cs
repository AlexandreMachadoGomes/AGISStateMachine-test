/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Items.AnimatorAudioStates
{
    using Opsive.Shared.Utility;
    using UnityEngine;

    /// <summary>
    /// The RandomRecoil state will move from one state to another in a sequence order.
    /// </summary>
    [System.Serializable]
    public class SequenceRecoil : RecoilAnimatorAudioStateSelector
    {
        [Tooltip("Resets the index back to the start after the specified delay. Set to -1 to never reset.")]
        [SerializeField] protected float m_ResetDelay = -1;

        public float ResetDelay { get { return m_ResetDelay; } set { m_ResetDelay = value; } }

        private int m_CurrentIndex = -1;
        private int m_NextIndex = -1;
        private float m_LastUsedTime = -1;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SequenceRecoil() : base() { }

        /// <summary>
        /// Overloaded constructor.
        /// </summary>
        /// <param name="blockedRecoilItemSubstateIndex">The blocked recoil item substate index.</param>
        public SequenceRecoil(int blockedRecoilItemSubstateIndex) : base(blockedRecoilItemSubstateIndex) { }

        /// <summary>
        /// Starts or stops the state selection.
        /// </summary>
        /// <param name="start">Is the object starting?</param>
        /// <param name="count">The count of states to expect.</param>
        public override void StartStopStateSelection(bool start, int count)
        {
            if (start) {
                // The Sequence task can reset which index is returned if the next state is selected too slowly. 
                if (m_ResetDelay != -1 && m_LastUsedTime != -1 && m_LastUsedTime + m_ResetDelay < TimeUtility.Time) {
                    m_NextIndex = m_CurrentIndex = -1;
                }

                if (m_NextIndex < 0) {
                    var size = m_States.Length;
                    if (size > 0) {
                        m_NextIndex = (m_NextIndex + 1) % size;
                    }
                }
            }

            base.StartStopStateSelection(start, count);
        }

        /// <summary>
        /// Returns the current state index. -1 indicates this index is not set by the class.
        /// </summary>
        /// <returns>The current state index.</returns>
        public override int GetStateIndex()
        {
            return m_CurrentIndex;
        }

        /// <summary>
        /// </summary>
        /// <returns>The next state index.</returns>
        public override int GetNextStateIndex()
        {
            return m_NextIndex;
        }

        /// <summary>
        /// Set the new state index.
        /// </summary>
        /// <param name="stateIndex">The new state index.</param>
        public override void SetNextStateIndex(int stateIndex)
        {
            m_NextIndex = stateIndex;
        }

        /// <summary>
        /// Moves to the next state.
        /// </summary>
        /// <returns>Was the state changed successfully?</returns>
        public override bool NextState()
        {
            m_LastUsedTime = TimeUtility.Time;

            var size = m_States.Length;
            if (size == 0) {
                return false;
            }

            var count = 0;
            if (m_NextIndex < 0 || !IsStateValid(m_NextIndex) || !m_States[m_NextIndex].Enabled) {
            do {
                    m_NextIndex = (m_NextIndex + 1) % size;
                    count++;
                } while ((!IsStateValid(m_NextIndex) || !m_States[m_NextIndex].Enabled) && count <= size);
            }

            var stateChange = count <= size;
            if (stateChange) {
                ChangeStates(m_CurrentIndex, m_NextIndex);
            }

            m_CurrentIndex = m_NextIndex;

            m_NextIndex = (m_NextIndex + 1) % size;

            return stateChange;
        }
    }
}