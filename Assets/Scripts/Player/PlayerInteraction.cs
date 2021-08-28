﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float _detectionRadius;
    [SerializeField] private LayerMask _interactionLayers;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _leftStackPosition;
    [SerializeField] private Transform _rightStackPosition;

    [Header("Stumble Parameters")]
    [SerializeField] private int _chanceToStumblePerDish;
    [SerializeField] private float _stumbleCheckRate;

    [Header("Stack Rotation Parameters")]
    [SerializeField] private Vector3 _leftStackIdleRotation;
    [SerializeField] private Vector3 _rightStackIdleRotation;
    [SerializeField] private Vector3 _leftStackMovingRotation;
    [SerializeField] private Vector3 _rightStackMovingRotation;

    private List<DishType> _dishesCollected;
    private bool _isCleaning;
    private Vector3 _leftStackOffset = new Vector3(0f, 0.1f, 0f);
    private Vector3 _rightStackOffset = new Vector3(0f, 0.1f, 0f);
    private bool _alternateStack;

    private float _disabledTimeLeft;
    private bool _isDisable;

    public static Action<List<DishType>> OnPlayerStartedCleaning;
    public static Action OnPlayerStoppedCleaning;
    public static Action<int> OnStabilityCheckBegin;
    public static Action OnPlayerStumble;

    public bool IsInteracting => _isCleaning;
    public List<DishType> DishesCollected => _dishesCollected;

    private float _stabilityCheckTimer;

    private void OnEnable()
    {
        Sink.OnDishesCleaned += OnDishesCleaned;
        StabilityCheck.OnStabilityCompleted += OnStabilityCompleted;
    }

    private void OnDisable()
    {
        Sink.OnDishesCleaned -= OnDishesCleaned;
        StabilityCheck.OnStabilityCompleted -= OnStabilityCompleted;
    }

    private void Awake()
    {
        _dishesCollected = new List<DishType>();
    }

    private void Update()
    {
        if (_disabledTimeLeft > 0)
        {
            _disabledTimeLeft -= Time.deltaTime;
        }
        else
        {
            _isDisable = false;
        }

        if (_animator.GetFloat("Speed") == 0)
        {
            _rightStackPosition.localRotation = Quaternion.Euler(_rightStackIdleRotation);
            _leftStackPosition.localRotation = Quaternion.Euler(_leftStackIdleRotation);
        }
        else
        {
            _rightStackPosition.localRotation = Quaternion.Euler(_rightStackMovingRotation);
            _leftStackPosition.localRotation = Quaternion.Euler(_leftStackMovingRotation);
        }

        if (_dishesCollected.Count > 0)
        {
            _stabilityCheckTimer += Time.deltaTime;

            if (_stabilityCheckTimer >= _stumbleCheckRate)
            {
                _stabilityCheckTimer = 0;
                var rng = UnityEngine.Random.Range(0, 100);
                if (rng < _dishesCollected.Count * _chanceToStumblePerDish)
                {
                    OnStabilityCheckBegin?.Invoke(_dishesCollected.Count);
                }
            }
        }
        else
        {
            _stabilityCheckTimer = 0;
        }
    }

    public void OnInteract(InputAction.CallbackContext value)
    {
        if(_isDisable) { return; }

        if (value.started)
        {
            var hitColliders = Physics.OverlapSphere(transform.position,
                                _detectionRadius,
                                _interactionLayers,
                                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitColliders.Length; i++)
            {
                if (TryToCleanDishes(hitColliders[i])) { return; }
                if (TryToPickUpDish(hitColliders[i]))  { return; }
                if (TryToEatPowerUp(hitColliders[i]))  { return; }
            }
        }

        if (_isCleaning && value.canceled)
        {
            _isCleaning = false;
            _animator.SetBool("Clean", false);
            OnPlayerStoppedCleaning?.Invoke();
        }
    }

    private bool TryToCleanDishes(Collider collider)
    {
        if (_dishesCollected.Count == 0) { return false; }

        var sink = collider.GetComponent<Sink>();

        if (sink)
        {
            StartCleaningDishes();
            return true;
        }

        return false;
    }

    private void StartCleaningDishes()
    {
        _animator.SetBool("Clean", true);

        _isCleaning = true;
        OnPlayerStartedCleaning?.Invoke(_dishesCollected);
    }

    private bool TryToPickUpDish(Collider collider)
    {
        var enemyAi = collider.GetComponent<AIBehaviour>();

        if (enemyAi)
        {
            PickUpDish(enemyAi);
            return true;
        }

        return false;
    }

    private void PickUpDish(AIBehaviour enemyAi)
    {
        enemyAi.StopAllCoroutines();
        _dishesCollected.Add(enemyAi.dishType);

        var dish = Instantiate(enemyAi.dishType.collectedDish,
            _alternateStack ? _leftStackPosition : _rightStackPosition);

        _alternateStack = !_alternateStack;

        if (_alternateStack)
        {
            _leftStackOffset += new Vector3(0f, enemyAi.dishType.collectedDish.transform.GetChild(0).GetComponent<MeshRenderer>().bounds.size.y, 0f);//new Vector3(0.0f, 0.2f, 0.0f);
            dish.transform.localPosition += _leftStackOffset;
        }
        else
        {
            _rightStackOffset += new Vector3(0f, enemyAi.dishType.collectedDish.transform.GetChild(0).GetComponent<MeshRenderer>().bounds.size.y, 0f);//new Vector3(0.0f, 0.2f, 0.0f);
            dish.transform.localPosition += _rightStackOffset;
        }

        enemyAi.gameObject.SetActive(false);
    }

    private bool TryToEatPowerUp(Collider collider)
    {
        var powerUp = collider.GetComponent<PowerUp>();

        if(powerUp)
        {
            powerUp.ConsumePowerUp();
            return true;
        }

        return false;
    }

    private void OnDishesCleaned()
    {
        _animator.SetBool("Clean", false);
        DropDishes();
    }

    private void OnStabilityCompleted(bool status)
    {
        if (!status)
        {
            Stumble();
        }
    }

    private void DropDishes()
    {
        _leftStackOffset = new Vector3(0f, 0.1f, 0f);
        _rightStackOffset = new Vector3(0f, 0.1f, 0f);

        foreach (Transform child in _leftStackPosition)
        {
            child.gameObject.SetActive(false);
        }

        foreach (Transform child in _rightStackPosition)
        {
            child.gameObject.SetActive(false);
        }
    }

    public void Stumble()
    {
        _animator.SetBool("Stumble", true);
        DropDishes();
        _dishesCollected.Clear();
        OnPlayerStumble?.Invoke();
    }

    public void Ignite(float disabledTime)
    {
        // Fire particles
        _isDisable = true;
        _disabledTimeLeft = disabledTime;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
