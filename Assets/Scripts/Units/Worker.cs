using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(NavMeshAgent))]
public class Worker : MonoBehaviour, ISelectable, IMoveable
{
    [SerializeField] private DecalProjector selection;
    private NavMeshAgent agent;

    public void Deselect()
    {
        if (selection != null)
        {
            selection.gameObject.SetActive(false);
        }
        Bus<UnitDeselectedEvent>.Raise(new UnitDeselectedEvent(this));
    }

    public void Move(Vector3 destination)
    {
        agent.SetDestination(destination);
    }

    public void Select()
    {
        if (selection != null)
        {
            selection.gameObject.SetActive(true);
        }
        Bus<UnitSelectedEvent>.Raise(new UnitSelectedEvent(this));
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }
}
