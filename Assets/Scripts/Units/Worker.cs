using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(NavMeshAgent))]
public class Worker : MonoBehaviour, ISelectable
{
    [SerializeField] private Transform target;
    [SerializeField] private DecalProjector selection;
    private NavMeshAgent agent;

    public void Deselect()
    {
        selection.gameObject.SetActive(false);
    }

    public void Select()
    {
        selection.gameObject.SetActive(true);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    private void Update()
    {
        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }
}
