/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Items.AnimatorAudioStates
{
    using Opsive.UltimateCharacterController.Character;
    using UnityEngine;

    /// <summary>
    /// The RandomRecoil state will move from one state to another in a random order.
    /// </summary>
    [System.Serializable]
    public class RandomRecoil : RecoilAnimatorAudioStateSelector
    {
        public override bool IsArrayIndexes => true;

        private int m_CurrentIndex = -1;
        private int m_NextIndex = -1;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public RandomRecoil() : base() { }
        
        /// <summary>
        /// Overloaded constructor.
        /// </summary>
        /// <param name="blockedRecoilItemSubstateIndex">The blocked recoil item substate index.</param>
        public RandomRecoil(int blockedRecoilItemSubstateIndex) : base(blockedRecoilItemSubstateIndex) { }

        /// <summary>
        /// Initializes the selector.
        /// </summary>
        /// <param name="gameObject">The GameObject that the state belongs to.</param>
        /// <param name="characterLocomotion">The character that the state belongs to.</param>
        /// <param name="characterItem">The item that the state belongs to.</param>
        /// <param name="states">The states which are being selected.</param>
        /// <param name="count">The count of states to expect.</param>
        public override void Initialize(GameObject gameObject, UltimateCharacterLocomotion characterLocomotion, CharacterItem characterItem, AnimatorAudioStateSet.AnimatorAudioState[] states, int count)
        {
            base.Initialize(gameObject, characterLocomotion, characterItem, states, count);

            // Call next state so the index will be initialized to a random value.
            NextState();
        }

        /// <summary>
        /// Starts or stops the state selection.
        /// </summary>
        /// <param name="start">Is the object starting?</param>
        /// <param name="count">The count of states to expect.</param>
        public override void StartStopStateSelection(bool start, int count)
        {
            if (start) {
                if (m_NextIndex < 0) {
                    var size = m_States.Length;
                    if (size > 0) {
                        m_NextIndex = UnityEngine.Random.Range(0, size);
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
        /// Returns the next state index. -1 indicates this index is not set by the class.
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

            m_NextIndex = UnityEngine.Random.Range(0, size);

            return stateChange;
        }
    }
}