using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 전용 NavMeshAgent 연동 이동 클래스
/// </summary>
public class EnemyMovement : UnitMovement
{
    private readonly NavMeshAgent _navAgent;
    public NavMeshAgent Agent => _navAgent;

    public EnemyMovement(Rigidbody rb, Transform transform, NavMeshAgent agent, LayerMask groundLayer) : base(rb, transform, groundLayer)
    {
        this._navAgent = agent;
        if(_navAgent != null)
        {
            _navAgent.updateRotation = false;
        }
    }

    public void MoveToDestination(Vector3 targetPosition, float speed)
    {
        if (_navAgent == null || !_navAgent.isOnNavMesh) return;

        _navAgent.isStopped = false;
        _navAgent.speed = speed;
        _navAgent.SetDestination(targetPosition);

        if(_navAgent.desiredVelocity.sqrMagnitude > 0.001f)
        {
            RotateTowards(_navAgent.desiredVelocity.normalized, 8.0f, Time.deltaTime);
        }
    }

    public override void StopMovement()
    {
        base.StopMovement();
        if (_navAgent != null && _navAgent.isOnNavMesh)
        {
            _navAgent.isStopped = true;
            _navAgent.velocity = Vector3.zero;
        }
    }

    public void FaceTarget(Vector3 targetPosition, float rotationSpeed, float deltaTime)
    {
        Vector3 direction = targetPosition - _transform.position;
        direction.y = 0f;

        if(direction.sqrMagnitude > 0.001f)
        {
            RotateTowards(direction.normalized, rotationSpeed, deltaTime);
        }
    }
}
