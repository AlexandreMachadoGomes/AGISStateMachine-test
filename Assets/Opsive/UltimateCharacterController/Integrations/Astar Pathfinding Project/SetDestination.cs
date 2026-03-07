using UnityEngine;
using Pathfinding;

public class SetDestination : MonoBehaviour {

    public GameObject m_Character;

    private IAstarAI m_AstarAI;

	private void Start ()
    {
        m_AstarAI = m_Character.GetComponent<IAstarAI>();
        m_AstarAI.destination = new Vector3(5, 0, 5);
	}
}
